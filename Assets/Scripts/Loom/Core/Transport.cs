using System;

namespace Loom.Core
{
    public enum TransportState
    {
        Stopped = 0,
        Playing = 1,
        Paused = 2
    }

    [Flags]
    public enum TransportUpdateStatus
    {
        Completed = 0,
        BufferFull = 1,
        Underrun = 2
    }

    /// <summary>
    /// Owns deterministic sequencer lifecycle and one fixed scheduling buffer in tick time.
    /// </summary>
    public sealed class Transport
    {
        private readonly StepSequencer sequencer;
        private readonly SchedulingBuffer<ScheduledNoteEvent> schedulingBuffer;
        private long activeTargetTick;
        private long desiredTargetTick;
        private bool hasActiveTarget;

        public Transport(StepSequencer sequencer, int schedulingCapacity)
        {
            this.sequencer = sequencer
                ?? throw new ArgumentNullException(nameof(sequencer));
            schedulingBuffer =
                new SchedulingBuffer<ScheduledNoteEvent>(schedulingCapacity);
        }

        public TransportState State { get; private set; }

        public long CurrentTick { get; private set; }

        public long ScheduledThroughTick => sequencer.ScheduledThroughTick;

        public int PendingEventCount => schedulingBuffer.Count;

        public bool HasPendingWindow => hasActiveTarget || sequencer.HasPendingWindow;

        /// <summary>
        /// Starts from a logical tick and resets deterministic event sequence numbering.
        /// </summary>
        public void Start(long startTick = 0)
        {
            if (State != TransportState.Stopped)
            {
                throw new InvalidOperationException(
                    "Transport can start only from the stopped state.");
            }

            ValidateNonNegativeTick(startTick, nameof(startTick));
            ResetScheduling(startTick);
            State = TransportState.Playing;
        }

        /// <summary>
        /// Advances the logical playhead and fills the requested half-open lookahead window.
        /// </summary>
        public TransportUpdateStatus Update(
            long currentTick,
            long targetTickExclusive)
        {
            if (State != TransportState.Playing)
            {
                throw new InvalidOperationException(
                    "Transport can update only while playing.");
            }

            ValidateNonNegativeTick(currentTick, nameof(currentTick));
            ValidateNonNegativeTick(targetTickExclusive, nameof(targetTickExclusive));
            if (currentTick < CurrentTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentTick),
                    currentTick,
                    "Transport current tick cannot move backward.");
            }

            if (targetTickExclusive < currentTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetTickExclusive),
                    targetTickExclusive,
                    "Scheduling target cannot precede the current tick.");
            }

            if (targetTickExclusive < desiredTargetTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetTickExclusive),
                    targetTickExclusive,
                    "Scheduling target cannot move backward.");
            }

            bool isUnderrun = currentTick > sequencer.ScheduledThroughTick;
            CurrentTick = currentTick;
            desiredTargetTick = targetTickExclusive;

            while (true)
            {
                if (!hasActiveTarget)
                {
                    if (desiredTargetTick <= sequencer.ScheduledThroughTick)
                    {
                        return isUnderrun
                            ? TransportUpdateStatus.Underrun
                            : TransportUpdateStatus.Completed;
                    }

                    activeTargetTick = desiredTargetTick;
                    hasActiveTarget = true;
                }

                if (!sequencer.TryScheduleThrough(
                    activeTargetTick,
                    schedulingBuffer))
                {
                    TransportUpdateStatus status = TransportUpdateStatus.BufferFull;
                    return isUnderrun
                        ? status | TransportUpdateStatus.Underrun
                        : status;
                }

                hasActiveTarget = false;
            }
        }

        public void Pause(long resumeTick)
        {
            if (State != TransportState.Playing)
            {
                throw new InvalidOperationException(
                    "Transport can pause only while playing.");
            }

            ValidateResumeTick(resumeTick);
            ResetScheduling(resumeTick);
            State = TransportState.Paused;
        }

        public void Resume()
        {
            if (State != TransportState.Paused)
            {
                throw new InvalidOperationException(
                    "Transport can resume only from the paused state.");
            }

            State = TransportState.Playing;
        }

        /// <summary>
        /// Clears unsubmitted events at the current logical playhead without changing state.
        /// </summary>
        public void Panic()
        {
            Panic(CurrentTick);
        }

        /// <summary>
        /// Clears unsubmitted events and restarts deterministic generation from a supplied playhead.
        /// </summary>
        public void Panic(long currentTick)
        {
            ValidateNonNegativeTick(currentTick, nameof(currentTick));

            if (State == TransportState.Stopped)
            {
                ResetScheduling(0);
                return;
            }

            if (State == TransportState.Paused && currentTick != CurrentTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentTick),
                    currentTick,
                    "Paused transport panic tick must equal its held playhead.");
            }

            if (currentTick < CurrentTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentTick),
                    currentTick,
                    "Transport panic tick cannot move backward.");
            }

            ResetScheduling(currentTick);
        }

        public void Stop()
        {
            ResetScheduling(0);
            State = TransportState.Stopped;
        }

        public bool TryPeekScheduledEvent(out ScheduledNoteEvent scheduledEvent)
        {
            return schedulingBuffer.TryPeek(out scheduledEvent);
        }

        public bool TryDequeueScheduledEvent(out ScheduledNoteEvent scheduledEvent)
        {
            return schedulingBuffer.TryDequeue(out scheduledEvent);
        }

        private static void ValidateNonNegativeTick(long tick, string parameterName)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    tick,
                    "Transport tick cannot be negative.");
            }
        }

        private void ValidateResumeTick(long resumeTick)
        {
            ValidateNonNegativeTick(resumeTick, nameof(resumeTick));
            if (resumeTick < CurrentTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(resumeTick),
                    resumeTick,
                    "Resume tick cannot precede the current playhead.");
            }
        }

        private void ResetScheduling(long tick)
        {
            schedulingBuffer.Clear();
            sequencer.Reset(tick);
            CurrentTick = tick;
            activeTargetTick = tick;
            desiredTargetTick = tick;
            hasActiveTarget = false;
        }
    }
}
