using System;

namespace Loom.Core
{
    /// <summary>
    /// Identifies a zero-based scale degree plus an additional octave offset.
    /// </summary>
    public readonly struct ScaleDegreePitch : IEquatable<ScaleDegreePitch>
    {
        public ScaleDegreePitch(int degree, int octaveOffset = 0)
        {
            Degree = degree;
            OctaveOffset = octaveOffset;
        }

        public int Degree { get; }

        public int OctaveOffset { get; }

        public bool Equals(ScaleDegreePitch other)
        {
            return Degree == other.Degree
                && OctaveOffset == other.OctaveOffset;
        }

        public override bool Equals(object obj)
        {
            return obj is ScaleDegreePitch other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Degree * 397) ^ OctaveOffset;
            }
        }

        public override string ToString()
        {
            return $"Degree {Degree}, octave offset {OctaveOffset}";
        }

        public static bool operator ==(ScaleDegreePitch left, ScaleDegreePitch right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ScaleDegreePitch left, ScaleDegreePitch right)
        {
            return !left.Equals(right);
        }
    }
}
