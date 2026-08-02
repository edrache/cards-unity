using System;

namespace Loom.Core
{
    /// <summary>
    /// Identifies one deterministic track and its stateless-random stream.
    /// </summary>
    public readonly struct TrackId : IEquatable<TrackId>
    {
        public TrackId(ulong value)
        {
            if (value == 0UL)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Track ID must be non-zero.");
            }

            Value = value;
        }

        public ulong Value { get; }

        public bool IsValid => Value != 0UL;

        public bool Equals(TrackId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is TrackId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return IsValid ? Value.ToString() : "Invalid";
        }

        public static bool operator ==(TrackId left, TrackId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TrackId left, TrackId right)
        {
            return !left.Equals(right);
        }
    }
}
