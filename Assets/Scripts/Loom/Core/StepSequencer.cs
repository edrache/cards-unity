using System;

namespace Loom.Core
{
    /// <summary>
    /// Expands one immutable step pattern into ordered note events for sequential half-open windows.
    /// </summary>
    public sealed class StepSequencer
    {
        private readonly StepPattern pattern;
        private readonly TriggerTemplate[] templates;
        private readonly ulong seed;
        private readonly ulong streamId;

        private long eventCycle;
        private int templateIndex;
        private long pendingTargetTick;
        private ulong nextSequenceNumber;
        private bool hasPendingTarget;
        private bool isSequenceExhausted;
        private bool isTimelineExhausted;

        /// <summary>
        /// Precompiles every playable ratchet trigger and its stable final-onset ordering.
        /// </summary>
        public StepSequencer(StepPattern pattern, ulong seed, ulong streamId)
        {
            this.pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
            this.seed = seed;
            this.streamId = streamId;
            templates = BuildAndSortTemplates(pattern);
        }

        /// <summary>
        /// Gets the exclusive tick through which a complete window has been generated.
        /// </summary>
        public long ScheduledThroughTick { get; private set; }

        public bool HasPendingWindow => hasPendingTarget;

        /// <summary>
        /// Generates every event before <paramref name="targetTickExclusive"/> that has not been generated
        /// previously. A false result means the destination is full and the exact cursor is retained.
        /// </summary>
        public bool TryScheduleThrough(
            long targetTickExclusive,
            SchedulingBuffer<ScheduledNoteEvent> destination)
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

            if (templates.Length == 0)
            {
                CompletePendingWindow();
                return true;
            }

            while (TryGetCurrentEventStartTick(out long eventStartTick))
            {
                if (eventStartTick >= pendingTargetTick)
                {
                    CompletePendingWindow();
                    return true;
                }

                TriggerTemplate template = templates[templateIndex];
                if (!TryGetSourceBaseTick(template, out long sourceBaseTick)
                    || !ShouldTrigger(template.Step, sourceBaseTick))
                {
                    AdvanceTemplateCursor();
                    continue;
                }

                if (template.Step.DurationTicks > long.MaxValue - eventStartTick)
                {
                    throw new OverflowException(
                        "Scheduled note end exceeds the Int64 tick range.");
                }

                if (isSequenceExhausted)
                {
                    throw new OverflowException(
                        "Scheduled event sequence number exceeds the UInt64 range.");
                }

                template.Step.TryGetNote(out Note note);
                var noteEvent = new NoteEvent(
                    note,
                    template.Step.Velocity,
                    eventStartTick,
                    template.Step.DurationTicks);
                var scheduledEvent = new ScheduledNoteEvent(
                    noteEvent,
                    nextSequenceNumber);

                if (!destination.TryEnqueue(scheduledEvent))
                {
                    return false;
                }

                if (nextSequenceNumber == ulong.MaxValue)
                {
                    isSequenceExhausted = true;
                }
                else
                {
                    nextSequenceNumber++;
                }

                AdvanceTemplateCursor();
            }

            CompletePendingWindow();
            return true;
        }

        /// <summary>
        /// Returns generation to tick zero. The caller must separately clear any destination buffer.
        /// </summary>
        public void Reset()
        {
            Reset(0);
        }

        /// <summary>
        /// Returns generation to the first final onset at or after a non-negative tick.
        /// The caller must separately clear any destination buffer.
        /// </summary>
        public void Reset(long startTick)
        {
            if (startTick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startTick),
                    startTick,
                    "Sequencer reset tick cannot be negative.");
            }

            eventCycle = startTick / pattern.LengthTicks;
            long onsetWithinCycle = startTick % pattern.LengthTicks;
            templateIndex = FindFirstTemplateAtOrAfter(onsetWithinCycle);
            isTimelineExhausted = false;
            if (templates.Length > 0 && templateIndex == templates.Length)
            {
                if (eventCycle == long.MaxValue)
                {
                    isTimelineExhausted = true;
                }
                else
                {
                    eventCycle++;
                }

                templateIndex = 0;
            }

            pendingTargetTick = startTick;
            nextSequenceNumber = 0UL;
            hasPendingTarget = false;
            isSequenceExhausted = false;
            ScheduledThroughTick = startTick;
        }

        private int FindFirstTemplateAtOrAfter(long onsetWithinCycle)
        {
            int low = 0;
            int high = templates.Length;

            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                if (templates[middle].NormalizedOnsetTick < onsetWithinCycle)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        private static TriggerTemplate[] BuildAndSortTemplates(StepPattern sourcePattern)
        {
            int templateCount = CountTemplates(sourcePattern);
            var builtTemplates = new TriggerTemplate[templateCount];
            int destinationIndex = 0;

            for (int stepIndex = 0; stepIndex < sourcePattern.Count; stepIndex++)
            {
                PatternStep step = sourcePattern[stepIndex];
                if (step.IsRest)
                {
                    continue;
                }

                for (int ratchetIndex = 0;
                    ratchetIndex < step.RatchetCount;
                    ratchetIndex++)
                {
                    long ratchetOffset = (long)ratchetIndex
                        * sourcePattern.TicksPerStep
                        / step.RatchetCount;
                    long rawOnset = checked(
                        ((long)stepIndex * sourcePattern.TicksPerStep)
                        + step.MicrotimingTickOffset
                        + ratchetOffset);
                    long cycleShift = FloorDivide(
                        rawOnset,
                        sourcePattern.LengthTicks);
                    long normalizedOnset = rawOnset
                        - (cycleShift * sourcePattern.LengthTicks);

                    builtTemplates[destinationIndex] = new TriggerTemplate(
                        step,
                        stepIndex,
                        ratchetIndex,
                        normalizedOnset,
                        cycleShift);
                    destinationIndex++;
                }
            }

            StableInsertionSort(builtTemplates);
            return builtTemplates;
        }

        private static int CountTemplates(StepPattern sourcePattern)
        {
            long count = 0L;

            for (int index = 0; index < sourcePattern.Count; index++)
            {
                PatternStep step = sourcePattern[index];
                if (!step.IsRest)
                {
                    count = checked(count + step.RatchetCount);
                }
            }

            if (count > int.MaxValue)
            {
                throw new OverflowException(
                    "Pattern contains more ratchet triggers than one array can own.");
            }

            return (int)count;
        }

        private static long FloorDivide(long dividend, long divisor)
        {
            long quotient = dividend / divisor;
            long remainder = dividend % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        private static void StableInsertionSort(TriggerTemplate[] values)
        {
            for (int index = 1; index < values.Length; index++)
            {
                TriggerTemplate value = values[index];
                int insertionIndex = index;

                while (insertionIndex > 0
                    && CompareTemplates(values[insertionIndex - 1], value) > 0)
                {
                    values[insertionIndex] = values[insertionIndex - 1];
                    insertionIndex--;
                }

                values[insertionIndex] = value;
            }
        }

        private static int CompareTemplates(TriggerTemplate left, TriggerTemplate right)
        {
            int comparison = left.NormalizedOnsetTick.CompareTo(
                right.NormalizedOnsetTick);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = right.SourceCycleShift.CompareTo(left.SourceCycleShift);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.StepIndex.CompareTo(right.StepIndex);
            return comparison != 0
                ? comparison
                : left.RatchetIndex.CompareTo(right.RatchetIndex);
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

        private bool TryGetCurrentEventStartTick(out long eventStartTick)
        {
            if (isTimelineExhausted)
            {
                eventStartTick = 0;
                return false;
            }

            TriggerTemplate template = templates[templateIndex];
            if (eventCycle > (long.MaxValue - template.NormalizedOnsetTick)
                / pattern.LengthTicks)
            {
                eventStartTick = 0;
                return false;
            }

            eventStartTick = (eventCycle * pattern.LengthTicks)
                + template.NormalizedOnsetTick;
            return true;
        }

        private bool TryGetSourceBaseTick(
            TriggerTemplate template,
            out long sourceBaseTick)
        {
            long sourceCycle;

            try
            {
                sourceCycle = checked(eventCycle - template.SourceCycleShift);
            }
            catch (OverflowException)
            {
                sourceBaseTick = 0;
                return false;
            }

            if (sourceCycle < 0)
            {
                sourceBaseTick = 0;
                return false;
            }

            long stepOffset = (long)template.StepIndex * pattern.TicksPerStep;
            if (sourceCycle > (long.MaxValue - stepOffset) / pattern.LengthTicks)
            {
                sourceBaseTick = 0;
                return false;
            }

            sourceBaseTick = (sourceCycle * pattern.LengthTicks) + stepOffset;
            return true;
        }

        private bool ShouldTrigger(PatternStep step, long sourceBaseTick)
        {
            if (step.Probability <= 0f)
            {
                return false;
            }

            if (step.Probability >= 1f)
            {
                return true;
            }

            ulong hash = StatelessRng.Hash(
                seed,
                streamId,
                sourceBaseTick,
                RandomSlot.StepTrigger);
            return StatelessRng.Float01(hash) < step.Probability;
        }

        private void AdvanceTemplateCursor()
        {
            templateIndex++;
            if (templateIndex < templates.Length)
            {
                return;
            }

            templateIndex = 0;
            if (eventCycle == long.MaxValue)
            {
                isTimelineExhausted = true;
            }
            else
            {
                eventCycle++;
            }
        }

        private void CompletePendingWindow()
        {
            ScheduledThroughTick = pendingTargetTick;
            hasPendingTarget = false;
        }

        private readonly struct TriggerTemplate
        {
            public TriggerTemplate(
                PatternStep step,
                int stepIndex,
                int ratchetIndex,
                long normalizedOnsetTick,
                long sourceCycleShift)
            {
                Step = step;
                StepIndex = stepIndex;
                RatchetIndex = ratchetIndex;
                NormalizedOnsetTick = normalizedOnsetTick;
                SourceCycleShift = sourceCycleShift;
            }

            public PatternStep Step { get; }

            public int StepIndex { get; }

            public int RatchetIndex { get; }

            public long NormalizedOnsetTick { get; }

            public long SourceCycleShift { get; }
        }
    }
}
