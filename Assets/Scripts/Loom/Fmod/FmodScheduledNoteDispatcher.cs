using System;
using Loom.Core;

namespace Loom.Fmod
{
    /// <summary>
    /// Submits sequenced note events to FMOD without removing a buffer item before success.
    /// </summary>
    public sealed class FmodScheduledNoteDispatcher
    {
        private const ulong SubmissionHeadroomBufferCount = 2UL;

        private readonly IFmodScheduledNoteClockMapper clockMapper;
        private readonly IFmodScheduledInstrument instrument;
        private readonly ulong minimumLeadSampleFrames;

        /// <summary>
        /// Creates a dispatcher with one required runtime DSP buffer plus one buffer of
        /// submission headroom before the native voice validates its start clock.
        /// </summary>
        public FmodScheduledNoteDispatcher(
            FMOD.System coreSystem,
            FmodTickDspClockMapper clockMapper,
            FmodOscillatorInstrument instrument)
        {
            if (!coreSystem.hasHandle())
            {
                throw new ArgumentException(
                    "FMOD Core system must have a valid handle.",
                    nameof(coreSystem));
            }

            this.clockMapper = clockMapper
                ?? throw new ArgumentNullException(nameof(clockMapper));
            this.instrument = instrument
                ?? throw new ArgumentNullException(nameof(instrument));

            FmodResult.Ensure(
                coreSystem.getDSPBufferSize(out uint bufferLength, out _),
                "FMOD.System.getDSPBufferSize(scheduled dispatcher)");
            if (bufferLength == 0U)
            {
                throw new InvalidOperationException(
                    "FMOD returned a zero DSP buffer length.");
            }

            minimumLeadSampleFrames = checked(
                bufferLength * SubmissionHeadroomBufferCount);
        }

        internal FmodScheduledNoteDispatcher(
            IFmodScheduledNoteClockMapper clockMapper,
            IFmodScheduledInstrument instrument,
            ulong minimumLeadSampleFrames)
        {
            this.clockMapper = clockMapper
                ?? throw new ArgumentNullException(nameof(clockMapper));
            this.instrument = instrument
                ?? throw new ArgumentNullException(nameof(instrument));
            if (minimumLeadSampleFrames == 0UL)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumLeadSampleFrames),
                    minimumLeadSampleFrames,
                    "Minimum scheduling lead must be positive.");
            }

            this.minimumLeadSampleFrames = minimumLeadSampleFrames;
        }

        /// <summary>
        /// Dispatches up to <paramref name="maxEvents"/> buffered events in FIFO order.
        /// </summary>
        /// <returns>The number of events successfully submitted and removed.</returns>
        public int DispatchAvailable(
            SchedulingBuffer<ScheduledNoteEvent> source,
            int maxEvents,
            out int lateEventCount,
            out int plateauDurationCount)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (maxEvents <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxEvents),
                    maxEvents,
                    "Maximum dispatch count must be positive.");
            }

            lateEventCount = 0;
            plateauDurationCount = 0;
            if (source.IsEmpty)
            {
                return 0;
            }

            ulong currentDspClock = clockMapper.GetCurrentDspClock();
            ulong earliestStartDspClock = checked(
                currentDspClock + minimumLeadSampleFrames);
            int dispatchedCount = 0;

            while (dispatchedCount < maxEvents
                && source.TryPeek(out ScheduledNoteEvent scheduledEvent))
            {
                DispatchEvent(
                    scheduledEvent,
                    earliestStartDspClock,
                    ref lateEventCount,
                    ref plateauDurationCount);

                if (!source.TryDequeue(out ScheduledNoteEvent dequeuedEvent)
                    || dequeuedEvent != scheduledEvent)
                {
                    throw new InvalidOperationException(
                        "Scheduling buffer ownership changed during FMOD dispatch.");
                }

                dispatchedCount++;
            }

            return dispatchedCount;
        }

        /// <summary>
        /// Dispatches directly from a Core transport without exposing its owned buffer.
        /// </summary>
        public int DispatchAvailable(
            Transport source,
            int maxEvents,
            out int lateEventCount,
            out int plateauDurationCount)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (maxEvents <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxEvents),
                    maxEvents,
                    "Maximum dispatch count must be positive.");
            }

            if (source.PendingEventCount == 0)
            {
                lateEventCount = 0;
                plateauDurationCount = 0;
                return 0;
            }

            return DispatchAvailable(
                source,
                maxEvents,
                clockMapper.GetCurrentDspClock(),
                out lateEventCount,
                out plateauDurationCount);
        }

        /// <summary>
        /// Dispatches from a Core transport using the caller's frame-consistent clock snapshot.
        /// </summary>
        public int DispatchAvailable(
            Transport source,
            int maxEvents,
            ulong currentDspClock,
            out int lateEventCount,
            out int plateauDurationCount)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (maxEvents <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxEvents),
                    maxEvents,
                    "Maximum dispatch count must be positive.");
            }

            lateEventCount = 0;
            plateauDurationCount = 0;
            if (source.PendingEventCount == 0)
            {
                return 0;
            }

            ulong earliestStartDspClock = checked(
                currentDspClock + minimumLeadSampleFrames);
            int dispatchedCount = 0;

            while (dispatchedCount < maxEvents
                && source.TryPeekScheduledEvent(
                    out ScheduledNoteEvent scheduledEvent))
            {
                DispatchEvent(
                    scheduledEvent,
                    earliestStartDspClock,
                    ref lateEventCount,
                    ref plateauDurationCount);

                if (!source.TryDequeueScheduledEvent(
                    out ScheduledNoteEvent dequeuedEvent)
                    || dequeuedEvent != scheduledEvent)
                {
                    throw new InvalidOperationException(
                        "Transport ownership changed during FMOD dispatch.");
                }

                dispatchedCount++;
            }

            return dispatchedCount;
        }

        private void DispatchEvent(
            ScheduledNoteEvent scheduledEvent,
            ulong earliestStartDspClock,
            ref int lateEventCount,
            ref int plateauDurationCount)
        {
            NoteEvent noteEvent = scheduledEvent.NoteEvent;
            ulong mappedStartDspClock = clockMapper.ToDspClock(
                noteEvent.StartTick);
            ulong mappedEndDspClock = clockMapper.ToDspClock(
                noteEvent.EndTick);
            if (mappedEndDspClock < mappedStartDspClock)
            {
                throw new InvalidOperationException(
                    "Mapped note end clock precedes its start clock.");
            }

            ulong durationSampleFrames = mappedEndDspClock - mappedStartDspClock;
            if (durationSampleFrames == 0UL)
            {
                durationSampleFrames = 1UL;
                plateauDurationCount++;
            }

            ulong effectiveStartDspClock = mappedStartDspClock;
            bool wasLate = effectiveStartDspClock < earliestStartDspClock;
            if (wasLate)
            {
                effectiveStartDspClock = earliestStartDspClock;
            }

            ulong effectiveEndDspClock = checked(
                effectiveStartDspClock + durationSampleFrames);
            FmodScheduledNoteSubmission submission = instrument.ScheduleNote(
                noteEvent,
                effectiveStartDspClock,
                effectiveEndDspClock);
            if (wasLate || submission.WasStartAdjusted)
            {
                lateEventCount++;
            }
        }
    }
}
