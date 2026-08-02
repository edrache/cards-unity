using System;

namespace Loom.Core
{
    /// <summary>
    /// Represents a constant-tempo region beginning at an absolute musical tick.
    /// </summary>
    public readonly struct TempoSegment : IEquatable<TempoSegment>
    {
        private const double SecondsPerMinute = 60d;

        /// <summary>
        /// Creates a constant-tempo segment.
        /// </summary>
        /// <param name="startTick">The non-negative absolute tick at which the segment begins.</param>
        /// <param name="beatsPerMinute">
        /// The finite positive tempo in quarter-note beats per minute, with a representable beat duration.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="startTick"/> is negative or <paramref name="beatsPerMinute"/> is not
        /// finite and greater than zero or its seconds-per-beat duration cannot be represented.
        /// </exception>
        public TempoSegment(long startTick, double beatsPerMinute)
        {
            if (startTick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startTick),
                    startTick,
                    "Tempo segment start tick cannot be negative.");
            }

            if (!IsValidBeatsPerMinute(beatsPerMinute))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(beatsPerMinute),
                    beatsPerMinute,
                    "Tempo must be finite and positive with a representable seconds-per-beat duration.");
            }

            StartTick = startTick;
            BeatsPerMinute = beatsPerMinute;
        }

        public long StartTick { get; }

        public double BeatsPerMinute { get; }

        internal bool IsValid => StartTick >= 0 && IsValidBeatsPerMinute(BeatsPerMinute);

        public bool Equals(TempoSegment other)
        {
            return StartTick == other.StartTick && BeatsPerMinute.Equals(other.BeatsPerMinute);
        }

        public override bool Equals(object obj)
        {
            return obj is TempoSegment other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StartTick.GetHashCode() * 397) ^ BeatsPerMinute.GetHashCode();
            }
        }

        public static bool operator ==(TempoSegment left, TempoSegment right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TempoSegment left, TempoSegment right)
        {
            return !left.Equals(right);
        }

        private static bool IsValidBeatsPerMinute(double beatsPerMinute)
        {
            if (double.IsNaN(beatsPerMinute)
                || double.IsInfinity(beatsPerMinute)
                || beatsPerMinute <= 0d)
            {
                return false;
            }

            double secondsPerBeat = SecondsPerMinute / beatsPerMinute;
            return !double.IsInfinity(secondsPerBeat) && secondsPerBeat > 0d;
        }
    }
}
