using System;

namespace Loom.Core
{
    /// <summary>
    /// Owns one immutable cyclic sequence of harmony steps.
    /// </summary>
    public sealed class HarmonyPlan
    {
        private readonly HarmonyStep[] steps;
        private readonly long[] startTicks;

        public HarmonyPlan(params HarmonyStep[] steps)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            if (steps.Length == 0)
            {
                throw new ArgumentException(
                    "Harmony plan must contain at least one step.",
                    nameof(steps));
            }

            this.steps = (HarmonyStep[])steps.Clone();
            startTicks = new long[steps.Length];

            long totalDurationTicks = 0L;
            for (int index = 0; index < this.steps.Length; index++)
            {
                HarmonyStep step = this.steps[index];
                if (!step.IsValid)
                {
                    throw new ArgumentException(
                        $"Harmony step at index {index} is invalid.",
                        nameof(steps));
                }

                startTicks[index] = totalDurationTicks;
                if (step.DurationTicks > long.MaxValue - totalDurationTicks)
                {
                    throw new ArgumentException(
                        "Harmony plan duration exceeds Int64.MaxValue ticks.",
                        nameof(steps));
                }

                totalDurationTicks += step.DurationTicks;
            }

            TotalDurationTicks = totalDurationTicks;
        }

        public int Count => steps.Length;

        public long TotalDurationTicks { get; }

        public HarmonyStep this[int index]
        {
            get
            {
                if (index < 0 || index >= steps.Length)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(index),
                        index,
                        "Harmony step index is outside the plan.");
                }

                return steps[index];
            }
        }

        /// <summary>
        /// Resolves an absolute non-negative tick into the cyclic half-open harmony timeline.
        /// </summary>
        public HarmonyStep GetStepAtTick(long tick)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tick),
                    tick,
                    "Harmony lookup tick cannot be negative.");
            }

            long tickWithinCycle = tick % TotalDurationTicks;
            int low = 0;
            int high = startTicks.Length - 1;

            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                if (startTicks[middle] <= tickWithinCycle)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return steps[high];
        }
    }
}
