using System;

namespace Loom.Core
{
    /// <summary>
    /// Represents an absolute, non-negative position on the fixed 960 PPQN, 4/4 musical timeline.
    /// </summary>
    public readonly struct MusicalTime : IEquatable<MusicalTime>
    {
        public const int TicksPerQuarterNote = 960;
        public const int TimeSignatureNumerator = 4;
        public const int TimeSignatureDenominator = 4;
        public const int TicksPerBeat = TicksPerQuarterNote * 4 / TimeSignatureDenominator;
        public const int TicksPerBar = TicksPerBeat * TimeSignatureNumerator;

        /// <summary>
        /// Creates a musical-time position from an absolute session tick.
        /// </summary>
        /// <param name="tick">The absolute tick, from zero through <see cref="long.MaxValue"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tick"/> is negative.</exception>
        public MusicalTime(long tick)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tick),
                    tick,
                    "Musical time tick cannot be negative.");
            }

            Tick = tick;
        }

        public long Tick { get; }

        /// <summary>
        /// Gets the zero-based bar index.
        /// </summary>
        public long BarIndex => Tick / TicksPerBar;

        /// <summary>
        /// Gets the zero-based beat index within the current bar.
        /// </summary>
        public int BeatIndex => (int)((Tick % TicksPerBar) / TicksPerBeat);

        /// <summary>
        /// Gets the zero-based tick offset within the current beat.
        /// </summary>
        public int TickInBeat => (int)(Tick % TicksPerBeat);

        public bool Equals(MusicalTime other)
        {
            return Tick == other.Tick;
        }

        public override bool Equals(object obj)
        {
            return obj is MusicalTime other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Tick.GetHashCode();
        }

        public override string ToString()
        {
            return Tick.ToString();
        }

        public static bool operator ==(MusicalTime left, MusicalTime right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MusicalTime left, MusicalTime right)
        {
            return !left.Equals(right);
        }
    }
}
