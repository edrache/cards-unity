using System;

namespace Loom.Core
{
    /// <summary>
    /// Converts absolute musical ticks into absolute sample-frame positions for one runtime sample rate.
    /// </summary>
    public sealed class TickSampleConverter
    {
        private const double SecondsPerMinute = 60d;
        private const double SampleFrameOverflowThreshold = 18_446_744_073_709_551_616d;

        private readonly TempoMap tempoMap;

        /// <summary>
        /// Creates a converter for an immutable tempo map and a fixed runtime sample rate.
        /// </summary>
        /// <param name="tempoMap">The immutable tempo map used for every conversion.</param>
        /// <param name="sampleRate">The positive runtime sample rate in frames per second.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tempoMap"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="sampleRate"/> is not positive.
        /// </exception>
        public TickSampleConverter(TempoMap tempoMap, int sampleRate)
        {
            this.tempoMap = tempoMap ?? throw new ArgumentNullException(nameof(tempoMap));

            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleRate),
                    sampleRate,
                    "Sample rate must be positive.");
            }

            SampleRate = sampleRate;
        }

        public int SampleRate { get; }

        /// <summary>
        /// Converts a non-negative absolute tick by summing exact tempo regions and rounding the final position.
        /// </summary>
        /// <param name="tick">The non-negative absolute musical tick to convert.</param>
        /// <returns>
        /// The nearest absolute sample frame, with midpoint values rounded away from zero.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tick"/> is negative.</exception>
        /// <exception cref="OverflowException">
        /// Thrown when the absolute sample-frame position cannot be represented by <see cref="ulong"/>.
        /// </exception>
        public ulong ToSampleFrame(long tick)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tick),
                    tick,
                    "Sample-frame conversion tick cannot be negative.");
            }

            double exactFrames = 0d;

            for (int index = 0; index < tempoMap.Count; index++)
            {
                TempoSegment segment = tempoMap[index];
                long segmentEndTick = tick;

                if (index + 1 < tempoMap.Count)
                {
                    long nextStartTick = tempoMap[index + 1].StartTick;
                    if (nextStartTick <= tick)
                    {
                        segmentEndTick = nextStartTick;
                    }
                }

                long segmentTickCount = segmentEndTick - segment.StartTick;
                double secondsPerTick = SecondsPerMinute
                    / segment.BeatsPerMinute
                    / MusicalTime.TicksPerQuarterNote;
                exactFrames += segmentTickCount * secondsPerTick * SampleRate;

                EnsureSampleFrameRange(exactFrames);

                if (segmentEndTick == tick)
                {
                    double roundedFrames = Math.Round(
                        exactFrames,
                        MidpointRounding.AwayFromZero);
                    EnsureSampleFrameRange(roundedFrames);
                    return (ulong)roundedFrames;
                }
            }

            throw new InvalidOperationException("Tempo map did not resolve the requested tick.");
        }

        private static void EnsureSampleFrameRange(double sampleFrames)
        {
            if (double.IsNaN(sampleFrames)
                || double.IsInfinity(sampleFrames)
                || sampleFrames < 0d
                || sampleFrames >= SampleFrameOverflowThreshold)
            {
                throw new OverflowException(
                    "Absolute sample-frame position exceeds the UInt64 range.");
            }
        }
    }
}
