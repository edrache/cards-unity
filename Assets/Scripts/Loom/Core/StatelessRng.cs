using System;

namespace Loom.Core
{
    /// <summary>
    /// Derives deterministic non-cryptographic values from stable musical inputs without mutable state.
    /// </summary>
    public static class StatelessRng
    {
        private const ulong StreamMultiplier = 0x9E3779B97F4A7C15UL;
        private const ulong TickMultiplier = 0xBF58476D1CE4E5B9UL;
        private const ulong SlotMultiplier = 0x94D049BB133111EBUL;
        private const float UnitFloatScale = 1f / 16_777_216f;

        /// <summary>
        /// Hashes four ordered lanes: seed, stream identifier, non-negative absolute tick, and named slot.
        /// </summary>
        /// <remarks>
        /// Every input is serialized as an unsigned 64-bit lane. Tick uses its non-negative integer value,
        /// and slot uses its frozen unsigned identifier. Arithmetic intentionally wraps modulo 2^64.
        /// </remarks>
        public static ulong Hash(
            ulong seed,
            ulong streamId,
            long tick,
            RandomSlot slot)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tick),
                    tick,
                    "Random hash tick cannot be negative.");
            }

            ValidateSlot(slot);

            unchecked
            {
                ulong value = seed;
                value = Mix(value ^ (streamId * StreamMultiplier));
                value = Mix(value ^ ((ulong)tick * TickMultiplier));
                value = Mix(value ^ ((ulong)slot * SlotMultiplier));
                return value;
            }
        }

        /// <summary>
        /// Converts the high 24 hash bits to an exactly representable single-precision value in [0, 1).
        /// </summary>
        public static float Float01(ulong hash)
        {
            return (hash >> 40) * UnitFloatScale;
        }

        private static ulong Mix(ulong value)
        {
            unchecked
            {
                value = (value ^ (value >> 30)) * TickMultiplier;
                value = (value ^ (value >> 27)) * SlotMultiplier;
                return value ^ (value >> 31);
            }
        }

        private static void ValidateSlot(RandomSlot slot)
        {
            switch (slot)
            {
                case RandomSlot.StepTrigger:
                case RandomSlot.StepVelocity:
                case RandomSlot.StepDuration:
                case RandomSlot.StepRatchetCount:
                case RandomSlot.StepMicrotiming:
                    return;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(slot),
                        slot,
                        "Random slot must be a defined stable decision domain.");
            }
        }
    }
}
