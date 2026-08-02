using System;

namespace Loom.Core
{
    /// <summary>
    /// Stores one diatonic chord root for a positive number of musical ticks.
    /// </summary>
    public readonly struct HarmonyStep : IEquatable<HarmonyStep>
    {
        public HarmonyStep(int rootScaleDegree, long durationTicks)
        {
            if (durationTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationTicks),
                    durationTicks,
                    "Harmony step duration must be positive.");
            }

            RootScaleDegree = rootScaleDegree;
            DurationTicks = durationTicks;
        }

        public int RootScaleDegree { get; }

        public long DurationTicks { get; }

        internal bool IsValid => DurationTicks > 0;

        public bool Equals(HarmonyStep other)
        {
            return RootScaleDegree == other.RootScaleDegree
                && DurationTicks == other.DurationTicks;
        }

        public override bool Equals(object obj)
        {
            return obj is HarmonyStep other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (RootScaleDegree * 397) ^ DurationTicks.GetHashCode();
            }
        }

        public static bool operator ==(HarmonyStep left, HarmonyStep right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HarmonyStep left, HarmonyStep right)
        {
            return !left.Equals(right);
        }
    }
}
