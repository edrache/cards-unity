using System;

namespace Loom.Core
{
    /// <summary>
    /// Identifies one pattern independently from the track that currently owns it.
    /// </summary>
    public readonly struct PatternId : IEquatable<PatternId>
    {
        public PatternId(ulong value)
        {
            if (value == 0UL)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Pattern ID must be non-zero.");
            }

            Value = value;
        }

        public ulong Value { get; }

        public bool IsValid => Value != 0UL;

        public bool Equals(PatternId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is PatternId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return IsValid ? Value.ToString() : "Invalid";
        }

        public static bool operator ==(PatternId left, PatternId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PatternId left, PatternId right)
        {
            return !left.Equals(right);
        }
    }
}
