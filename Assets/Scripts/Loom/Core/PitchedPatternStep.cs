using System;

namespace Loom.Core
{
    /// <summary>
    /// Stores one immutable scale-degree pitch or rest before harmony-aware timeline expansion.
    /// </summary>
    public readonly struct PitchedPatternStep : IEquatable<PitchedPatternStep>
    {
        public const int MinVelocity = NoteEvent.MinVelocity;
        public const int MaxVelocity = NoteEvent.MaxVelocity;
        public const int MinRatchetCount = 1;
        public const int MaxRatchetCount = byte.MaxValue;

        private readonly ScaleDegreePitch pitch;
        private readonly bool hasPitch;

        /// <summary>
        /// Creates a playable pitched step. Probability is evaluated once per base-step occurrence.
        /// </summary>
        public PitchedPatternStep(
            ScaleDegreePitch pitch,
            int velocity,
            long durationTicks,
            float probability = 1f,
            int ratchetCount = 1,
            int microtimingTickOffset = 0)
        {
            if (velocity < MinVelocity || velocity > MaxVelocity)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(velocity),
                    velocity,
                    $"Velocity must be between {MinVelocity} and {MaxVelocity}.");
            }

            if (durationTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationTicks),
                    durationTicks,
                    "Step duration must be positive.");
            }

            if (float.IsNaN(probability)
                || float.IsInfinity(probability)
                || probability < 0f
                || probability > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(probability),
                    probability,
                    "Step probability must be finite and normalized.");
            }

            if (ratchetCount < MinRatchetCount || ratchetCount > MaxRatchetCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ratchetCount),
                    ratchetCount,
                    $"Ratchet count must be between {MinRatchetCount} and {MaxRatchetCount}.");
            }

            this.pitch = pitch;
            hasPitch = true;
            Velocity = velocity;
            DurationTicks = durationTicks;
            Probability = probability == 0f ? 0f : probability;
            RatchetCount = ratchetCount;
            MicrotimingTickOffset = microtimingTickOffset;
        }

        /// <summary>
        /// Gets the canonical rest value. The default <see cref="PitchedPatternStep"/> is also a rest.
        /// </summary>
        public static PitchedPatternStep Rest => default;

        public bool IsRest => !hasPitch;

        public int Velocity { get; }

        public long DurationTicks { get; }

        public float Probability { get; }

        public int RatchetCount { get; }

        public int MicrotimingTickOffset { get; }

        public bool TryGetPitch(out ScaleDegreePitch scaleDegreePitch)
        {
            scaleDegreePitch = pitch;
            return hasPitch;
        }

        public bool Equals(PitchedPatternStep other)
        {
            return pitch.Equals(other.pitch)
                && hasPitch == other.hasPitch
                && Velocity == other.Velocity
                && DurationTicks == other.DurationTicks
                && Probability.Equals(other.Probability)
                && RatchetCount == other.RatchetCount
                && MicrotimingTickOffset == other.MicrotimingTickOffset;
        }

        public override bool Equals(object obj)
        {
            return obj is PitchedPatternStep other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = pitch.GetHashCode();
                hashCode = (hashCode * 397) ^ hasPitch.GetHashCode();
                hashCode = (hashCode * 397) ^ Velocity;
                hashCode = (hashCode * 397) ^ DurationTicks.GetHashCode();
                hashCode = (hashCode * 397) ^ Probability.GetHashCode();
                hashCode = (hashCode * 397) ^ RatchetCount;
                return (hashCode * 397) ^ MicrotimingTickOffset;
            }
        }

        public static bool operator ==(PitchedPatternStep left, PitchedPatternStep right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PitchedPatternStep left, PitchedPatternStep right)
        {
            return !left.Equals(right);
        }
    }
}
