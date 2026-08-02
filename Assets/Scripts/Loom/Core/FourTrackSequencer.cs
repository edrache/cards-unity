using System;

namespace Loom.Core
{
    /// <summary>
    /// Coordinates drums, bass, lead, and pad through one canonical deterministic event stream.
    /// </summary>
    public sealed class FourTrackSequencer
    {
        private readonly TrackId drumsTrackId;
        private readonly TrackId bassTrackId;
        private readonly TrackId leadTrackId;
        private readonly TrackId padTrackId;
        private readonly PercussionStepSequencer drumsSequencer;
        private readonly PitchedStepSequencer bassSequencer;
        private readonly PitchedStepSequencer leadSequencer;
        private readonly PitchedStepSequencer padSequencer;
        private readonly SchedulingBuffer<ScheduledPercussionNoteEvent> drumsBuffer;
        private readonly SchedulingBuffer<ScheduledNoteEvent> bassBuffer;
        private readonly SchedulingBuffer<ScheduledNoteEvent> leadBuffer;
        private readonly SchedulingBuffer<ScheduledNoteEvent> padBuffer;

        private long pendingTargetTick;
        private bool hasPendingTarget;

        public FourTrackSequencer(
            TrackId drumsTrackId,
            PercussionStepSequencer drumsSequencer,
            TrackId bassTrackId,
            PitchedStepSequencer bassSequencer,
            TrackId leadTrackId,
            PitchedStepSequencer leadSequencer,
            TrackId padTrackId,
            PitchedStepSequencer padSequencer,
            int perTrackBufferCapacity = 64)
        {
            ValidateDistinctTrackIds(
                drumsTrackId,
                bassTrackId,
                leadTrackId,
                padTrackId);

            this.drumsTrackId = drumsTrackId;
            this.bassTrackId = bassTrackId;
            this.leadTrackId = leadTrackId;
            this.padTrackId = padTrackId;
            this.drumsSequencer = drumsSequencer
                ?? throw new ArgumentNullException(nameof(drumsSequencer));
            this.bassSequencer = bassSequencer
                ?? throw new ArgumentNullException(nameof(bassSequencer));
            this.leadSequencer = leadSequencer
                ?? throw new ArgumentNullException(nameof(leadSequencer));
            this.padSequencer = padSequencer
                ?? throw new ArgumentNullException(nameof(padSequencer));
            drumsBuffer = new SchedulingBuffer<ScheduledPercussionNoteEvent>(
                perTrackBufferCapacity);
            bassBuffer = new SchedulingBuffer<ScheduledNoteEvent>(perTrackBufferCapacity);
            leadBuffer = new SchedulingBuffer<ScheduledNoteEvent>(perTrackBufferCapacity);
            padBuffer = new SchedulingBuffer<ScheduledNoteEvent>(perTrackBufferCapacity);
        }

        public long ScheduledThroughTick { get; private set; }

        public bool HasPendingWindow => hasPendingTarget;

        public bool TryScheduleThrough(
            long targetTickExclusive,
            SchedulingBuffer<MultiTrackScheduledEvent> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (targetTickExclusive < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetTickExclusive),
                    targetTickExclusive,
                    "Scheduling target cannot be negative.");
            }

            ValidateOrBeginWindow(targetTickExclusive);
            if (!hasPendingTarget)
            {
                return true;
            }

            while (true)
            {
                FillEmptyTrackBuffers();
                if (!TrySelectNextEvent(out MultiTrackScheduledEvent nextEvent))
                {
                    CompletePendingWindow();
                    return true;
                }

                if (!destination.TryEnqueue(nextEvent))
                {
                    return false;
                }

                DequeueSelectedTrack(nextEvent.TrackId);
            }
        }

        public void Reset()
        {
            Reset(0L);
        }

        public void Reset(long startTick)
        {
            if (startTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startTick), startTick, null);
            }

            drumsSequencer.Reset(startTick);
            bassSequencer.Reset(startTick);
            leadSequencer.Reset(startTick);
            padSequencer.Reset(startTick);
            drumsBuffer.Clear();
            bassBuffer.Clear();
            leadBuffer.Clear();
            padBuffer.Clear();
            ScheduledThroughTick = startTick;
            pendingTargetTick = startTick;
            hasPendingTarget = false;
        }

        private static void ValidateDistinctTrackIds(
            TrackId drums,
            TrackId bass,
            TrackId lead,
            TrackId pad)
        {
            if (!drums.IsValid || !bass.IsValid || !lead.IsValid || !pad.IsValid)
            {
                throw new ArgumentException("Every coordinated track ID must be valid.");
            }

            if (drums == bass || drums == lead || drums == pad
                || bass == lead || bass == pad || lead == pad)
            {
                throw new ArgumentException("Coordinated track IDs must be distinct.");
            }
        }

        private void ValidateOrBeginWindow(long targetTickExclusive)
        {
            if (hasPendingTarget)
            {
                if (targetTickExclusive != pendingTargetTick)
                {
                    throw new InvalidOperationException(
                        "A partially generated window must be completed with the same target tick.");
                }

                return;
            }

            if (targetTickExclusive < ScheduledThroughTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetTickExclusive),
                    targetTickExclusive,
                    "Scheduling target cannot move behind the completed watermark.");
            }

            if (targetTickExclusive == ScheduledThroughTick)
            {
                return;
            }

            pendingTargetTick = targetTickExclusive;
            hasPendingTarget = true;
        }

        private void FillEmptyTrackBuffers()
        {
            if (drumsBuffer.IsEmpty
                && (drumsSequencer.HasPendingWindow
                    || drumsSequencer.ScheduledThroughTick < pendingTargetTick))
            {
                drumsSequencer.TryScheduleThrough(pendingTargetTick, drumsBuffer);
            }

            FillEmptyPitchedBuffer(bassSequencer, bassBuffer);
            FillEmptyPitchedBuffer(leadSequencer, leadBuffer);
            FillEmptyPitchedBuffer(padSequencer, padBuffer);
        }

        private void FillEmptyPitchedBuffer(
            PitchedStepSequencer sequencer,
            SchedulingBuffer<ScheduledNoteEvent> buffer)
        {
            if (buffer.IsEmpty
                && (sequencer.HasPendingWindow
                    || sequencer.ScheduledThroughTick < pendingTargetTick))
            {
                sequencer.TryScheduleThrough(pendingTargetTick, buffer);
            }
        }

        private bool TrySelectNextEvent(out MultiTrackScheduledEvent selected)
        {
            selected = default;
            bool hasSelected = false;

            if (drumsBuffer.TryPeek(out ScheduledPercussionNoteEvent drumsEvent))
            {
                selected = MultiTrackScheduledEvent.FromPercussion(
                    drumsTrackId,
                    drumsEvent);
                hasSelected = true;
            }

            ConsiderPitched(bassTrackId, bassBuffer, ref selected, ref hasSelected);
            ConsiderPitched(leadTrackId, leadBuffer, ref selected, ref hasSelected);
            ConsiderPitched(padTrackId, padBuffer, ref selected, ref hasSelected);
            return hasSelected;
        }

        private static void ConsiderPitched(
            TrackId trackId,
            SchedulingBuffer<ScheduledNoteEvent> buffer,
            ref MultiTrackScheduledEvent selected,
            ref bool hasSelected)
        {
            if (!buffer.TryPeek(out ScheduledNoteEvent scheduledEvent))
            {
                return;
            }

            var candidate = MultiTrackScheduledEvent.FromPitched(trackId, scheduledEvent);
            if (!hasSelected || Compare(candidate, selected) < 0)
            {
                selected = candidate;
                hasSelected = true;
            }
        }

        private static int Compare(
            MultiTrackScheduledEvent left,
            MultiTrackScheduledEvent right)
        {
            int comparison = left.StartTick.CompareTo(right.StartTick);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.TrackId.Value.CompareTo(right.TrackId.Value);
            return comparison != 0
                ? comparison
                : left.TrackSequenceNumber.CompareTo(right.TrackSequenceNumber);
        }

        private void DequeueSelectedTrack(TrackId trackId)
        {
            if (trackId == drumsTrackId)
            {
                drumsBuffer.TryDequeue(out _);
            }
            else if (trackId == bassTrackId)
            {
                bassBuffer.TryDequeue(out _);
            }
            else if (trackId == leadTrackId)
            {
                leadBuffer.TryDequeue(out _);
            }
            else
            {
                padBuffer.TryDequeue(out _);
            }
        }

        private void CompletePendingWindow()
        {
            ScheduledThroughTick = pendingTargetTick;
            hasPendingTarget = false;
        }
    }
}
