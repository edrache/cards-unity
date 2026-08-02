using System;

namespace Loom.Core
{
    /// <summary>
    /// Owns an immutable step sequence on a fixed subdivision of the 960-PPQN beat.
    /// </summary>
    public sealed class StepPattern
    {
        private readonly PatternStep[] steps;

        /// <summary>
        /// Creates a pattern by defensively copying every supplied step.
        /// </summary>
        public StepPattern(int stepsPerBeat, params PatternStep[] steps)
        {
            if (stepsPerBeat <= 0
                || MusicalTime.TicksPerBeat % stepsPerBeat != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stepsPerBeat),
                    stepsPerBeat,
                    "Steps per beat must be a positive divisor of the 960-PPQN beat.");
            }

            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            if (steps.Length == 0)
            {
                throw new ArgumentException(
                    "Step pattern must contain at least one step.",
                    nameof(steps));
            }

            StepsPerBeat = stepsPerBeat;
            TicksPerStep = MusicalTime.TicksPerBeat / stepsPerBeat;
            LengthTicks = checked((long)steps.Length * TicksPerStep);
            this.steps = (PatternStep[])steps.Clone();
            MinimumMicrotimingTickOffset = ValidateAndFindMinimumOffset(this.steps);
        }

        public int StepsPerBeat { get; }

        public int TicksPerStep { get; }

        public long LengthTicks { get; }

        public int Count => steps.Length;

        public int MinimumMicrotimingTickOffset { get; }

        public PatternStep this[int index]
        {
            get
            {
                if (index < 0 || index >= steps.Length)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(index),
                        index,
                        "Step index is outside the pattern.");
                }

                return steps[index];
            }
        }

        private int ValidateAndFindMinimumOffset(PatternStep[] ownedSteps)
        {
            int minimumOffset = 0;

            for (int index = 0; index < ownedSteps.Length; index++)
            {
                PatternStep step = ownedSteps[index];
                if (step.IsRest)
                {
                    continue;
                }

                if (step.MicrotimingTickOffset <= -TicksPerStep
                    || step.MicrotimingTickOffset >= TicksPerStep)
                {
                    throw new ArgumentException(
                        $"Step at index {index} has a microtiming offset outside its step interval.",
                        "steps");
                }

                if (step.RatchetCount > TicksPerStep)
                {
                    throw new ArgumentException(
                        $"Step at index {index} has more ratchets than distinct ticks in its step interval.",
                        "steps");
                }

                if (step.MicrotimingTickOffset < minimumOffset)
                {
                    minimumOffset = step.MicrotimingTickOffset;
                }
            }

            return minimumOffset;
        }
    }
}
