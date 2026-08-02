using System;

namespace Loom.Core
{
    /// <summary>
    /// Owns an immutable, strictly ordered set of constant-tempo segments.
    /// </summary>
    public sealed class TempoMap
    {
        public const double DefaultBeatsPerMinute = 120d;

        private readonly TempoSegment[] segments;

        /// <summary>
        /// Creates a tempo map by defensively copying the supplied segments.
        /// </summary>
        /// <param name="segments">
        /// One or more valid segments, beginning at tick zero and ordered by strictly increasing start tick.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="segments"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the collection is empty, contains an invalid segment, does not begin at tick zero, or is
        /// not strictly ordered.
        /// </exception>
        public TempoMap(params TempoSegment[] segments)
        {
            if (segments == null)
            {
                throw new ArgumentNullException(nameof(segments));
            }

            if (segments.Length == 0)
            {
                throw new ArgumentException("Tempo map must contain at least one segment.", nameof(segments));
            }

            this.segments = (TempoSegment[])segments.Clone();
            ValidateSegments(this.segments);
        }

        public static TempoMap Default { get; } = new TempoMap(
            new TempoSegment(0, DefaultBeatsPerMinute));

        public int Count => segments.Length;

        public TempoSegment this[int index]
        {
            get
            {
                if (index < 0 || index >= segments.Length)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(index),
                        index,
                        "Tempo segment index is outside the map.");
                }

                return segments[index];
            }
        }

        /// <summary>
        /// Gets the segment whose half-open region contains the specified absolute tick.
        /// </summary>
        /// <param name="tick">The non-negative absolute musical tick to resolve.</param>
        /// <returns>The last segment whose start tick is less than or equal to <paramref name="tick"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tick"/> is negative.</exception>
        public TempoSegment GetSegmentAtTick(long tick)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tick),
                    tick,
                    "Tempo lookup tick cannot be negative.");
            }

            int low = 0;
            int high = segments.Length - 1;

            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                if (segments[middle].StartTick <= tick)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return segments[high];
        }

        private static void ValidateSegments(TempoSegment[] ownedSegments)
        {
            for (int index = 0; index < ownedSegments.Length; index++)
            {
                TempoSegment segment = ownedSegments[index];
                if (!segment.IsValid)
                {
                    throw new ArgumentException(
                        $"Tempo segment at index {index} is invalid.",
                        "segments");
                }

                if (index == 0 && segment.StartTick != 0)
                {
                    throw new ArgumentException(
                        "Tempo map must begin with a segment at tick zero.",
                        "segments");
                }

                if (index > 0 && segment.StartTick <= ownedSegments[index - 1].StartTick)
                {
                    throw new ArgumentException(
                        "Tempo segment start ticks must be strictly increasing.",
                        "segments");
                }
            }
        }
    }
}
