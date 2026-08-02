using System;

namespace Loom.Core
{
    /// <summary>
    /// Stores one immutable percussion-note or rest step before timeline expansion.
    /// </summary>
    public readonly struct PercussionPatternStep : IEquatable<PercussionPatternStep>
    {
        public const int MinVelocity = PercussionNoteEvent.MinVelocity;
        public const int MaxVelocity = PercussionNoteEvent.MaxVelocity;
        public const int MinRatchetCount = 1;
        public const int MaxRatchetCount = byte.MaxValue;

        private readonly PercussionNote note;
        private readonly bool hasNote;

        /// <summary>
        /// Creates a playable step. Probability is evaluated once per base-step occurrence.
        /// </summary>
        public PercussionPatternStep(
            PercussionNote note,
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

            this.note = note;
            hasNote = true;
            Velocity = velocity;
            DurationTicks = durationTicks;
            Probability = probability == 0f ? 0f : probability;
            RatchetCount = ratchetCount;
            MicrotimingTickOffset = microtimingTickOffset;
        }

        /// <summary>
        /// Gets the canonical rest value. The default <see cref="PercussionPatternStep"/> is also a rest.
        /// </summary>
        public static PercussionPatternStep Rest => default;

        public bool IsRest => !hasNote;

        public int Velocity { get; }

        public long DurationTicks { get; }

        public float Probability { get; }

        public int RatchetCount { get; }

        public int MicrotimingTickOffset { get; }

        public bool TryGetNote(out PercussionNote resolvedNote)
        {
            resolvedNote = note;
            return hasNote;
        }

        public bool Equals(PercussionPatternStep other)
        {
            return note.Equals(other.note)
                && hasNote == other.hasNote
                && Velocity == other.Velocity
                && DurationTicks == other.DurationTicks
                && Probability.Equals(other.Probability)
                && RatchetCount == other.RatchetCount
                && MicrotimingTickOffset == other.MicrotimingTickOffset;
        }

        public override bool Equals(object obj)
        {
            return obj is PercussionPatternStep other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = note.GetHashCode();
                hashCode = (hashCode * 397) ^ hasNote.GetHashCode();
                hashCode = (hashCode * 397) ^ Velocity;
                hashCode = (hashCode * 397) ^ DurationTicks.GetHashCode();
                hashCode = (hashCode * 397) ^ Probability.GetHashCode();
                hashCode = (hashCode * 397) ^ RatchetCount;
                return (hashCode * 397) ^ MicrotimingTickOffset;
            }
        }

        public static bool operator ==(PercussionPatternStep left, PercussionPatternStep right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PercussionPatternStep left, PercussionPatternStep right)
        {
            return !left.Equals(right);
        }
    }
}
