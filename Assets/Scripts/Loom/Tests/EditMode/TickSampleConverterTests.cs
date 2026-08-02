using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class TickSampleConverterTests
    {
        [TestCase(0, 0UL)]
        [TestCase(1, 25UL)]
        [TestCase(959, 23_975UL)]
        [TestCase(960, 24_000UL)]
        [TestCase(3_840, 96_000UL)]
        public void ConverterMapsDefaultTempoTicksToFortyEightKilohertzFrames(
            long tick,
            ulong expectedSampleFrame)
        {
            var converter = new Core.TickSampleConverter(Core.TempoMap.Default, 48_000);

            ulong sampleFrame = converter.ToSampleFrame(tick);

            Assert.That(sampleFrame, Is.EqualTo(expectedSampleFrame));
        }

        [TestCase(0, 0UL)]
        [TestCase(959, 23_975UL)]
        [TestCase(960, 24_000UL)]
        [TestCase(961, 24_050UL)]
        [TestCase(1_919, 71_950UL)]
        [TestCase(1_920, 72_000UL)]
        [TestCase(1_921, 72_013UL)]
        [TestCase(2_880, 84_000UL)]
        public void ConverterAccumulatesTempoRegionsAcrossBoundaries(
            long tick,
            ulong expectedSampleFrame)
        {
            var tempoMap = new Core.TempoMap(
                new Core.TempoSegment(0, 120d),
                new Core.TempoSegment(960, 60d),
                new Core.TempoSegment(1_920, 240d));
            var converter = new Core.TickSampleConverter(tempoMap, 48_000);

            ulong sampleFrame = converter.ToSampleFrame(tick);

            Assert.That(sampleFrame, Is.EqualTo(expectedSampleFrame));
        }

        [TestCase(44_100, 22_050UL)]
        [TestCase(48_000, 24_000UL)]
        public void ConverterUsesValidatedRuntimeSampleRate(
            int sampleRate,
            ulong expectedQuarterNoteFrames)
        {
            var converter = new Core.TickSampleConverter(Core.TempoMap.Default, sampleRate);

            Assert.That(converter.SampleRate, Is.EqualTo(sampleRate));
            Assert.That(
                converter.ToSampleFrame(Core.MusicalTime.TicksPerQuarterNote),
                Is.EqualTo(expectedQuarterNoteFrames));
        }

        [Test]
        public void ConverterRoundsFractionalFramesAtTheFinalPosition()
        {
            var converter = new Core.TickSampleConverter(Core.TempoMap.Default, 44_100);

            Assert.That(converter.ToSampleFrame(15), Is.EqualTo(345UL));
            Assert.That(converter.ToSampleFrame(16), Is.EqualTo(368UL));
            Assert.That(converter.ToSampleFrame(17), Is.EqualTo(390UL));
        }

        [Test]
        public void ConverterRoundsMidpointsAwayFromZero()
        {
            var converter = new Core.TickSampleConverter(Core.TempoMap.Default, 1);

            Assert.That(converter.ToSampleFrame(959), Is.Zero);
            Assert.That(converter.ToSampleFrame(960), Is.EqualTo(1UL));
        }

        [Test]
        public void ConverterRoundsOnlyOnceAcrossTempoSegments()
        {
            var tempoMap = new Core.TempoMap(
                new Core.TempoSegment(0, 120d),
                new Core.TempoSegment(960, 120d));
            var converter = new Core.TickSampleConverter(tempoMap, 1);

            Assert.That(converter.ToSampleFrame(1_920), Is.EqualTo(1UL));
        }

        [TestCase(0UL, 0)]
        [TestCase(24UL, 0)]
        [TestCase(25UL, 1)]
        [TestCase(23_999UL, 959)]
        [TestCase(24_000UL, 960)]
        public void InverseConverterFindsTheLastTickAtOrBeforeAFrame(
            ulong sampleFrame,
            long expectedTick)
        {
            var converter = new Core.TickSampleConverter(Core.TempoMap.Default, 48_000);

            Assert.That(
                converter.ToTickAtOrBeforeSampleFrame(sampleFrame),
                Is.EqualTo(expectedTick));
        }

        [TestCase(0UL, 959)]
        [TestCase(1UL, 2_879)]
        public void InverseConverterSelectsTheLastTickOnARoundingPlateau(
            ulong sampleFrame,
            long expectedTick)
        {
            var converter = new Core.TickSampleConverter(Core.TempoMap.Default, 1);

            Assert.That(
                converter.ToTickAtOrBeforeSampleFrame(sampleFrame),
                Is.EqualTo(expectedTick));
        }

        [TestCase(23_999UL, 959)]
        [TestCase(24_000UL, 960)]
        [TestCase(24_049UL, 960)]
        [TestCase(24_050UL, 961)]
        [TestCase(71_999UL, 1_919)]
        [TestCase(72_000UL, 1_920)]
        public void InverseConverterResolvesTempoBoundaries(
            ulong sampleFrame,
            long expectedTick)
        {
            var tempoMap = new Core.TempoMap(
                new Core.TempoSegment(0, 120d),
                new Core.TempoSegment(960, 60d),
                new Core.TempoSegment(1_920, 240d));
            var converter = new Core.TickSampleConverter(tempoMap, 48_000);

            Assert.That(
                converter.ToTickAtOrBeforeSampleFrame(sampleFrame),
                Is.EqualTo(expectedTick));
        }

        [Test]
        public void InverseConverterSaturatesAtInt64MaximumWhenItQualifies()
        {
            var converter = new Core.TickSampleConverter(Core.TempoMap.Default, 1);

            Assert.That(
                converter.ToTickAtOrBeforeSampleFrame(ulong.MaxValue),
                Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void InverseConverterStopsAtTheLastRepresentableTick()
        {
            var converter = new Core.TickSampleConverter(Core.TempoMap.Default, 48_000);

            long tick = converter.ToTickAtOrBeforeSampleFrame(ulong.MaxValue);

            Assert.That(converter.ToSampleFrame(tick), Is.LessThanOrEqualTo(ulong.MaxValue));
            Assert.Throws<OverflowException>(() => converter.ToSampleFrame(tick + 1));
        }

        [Test]
        public void InverseConverterMatchesAForwardProjectionOracle()
        {
            var tempoMap = new Core.TempoMap(
                new Core.TempoSegment(0, 137d),
                new Core.TempoSegment(960, 83d));
            var converter = new Core.TickSampleConverter(tempoMap, 97);

            for (ulong sampleFrame = 0; sampleFrame < 200UL; sampleFrame++)
            {
                long expectedTick = 0;
                while (converter.ToSampleFrame(expectedTick + 1) <= sampleFrame)
                {
                    expectedTick++;
                }

                Assert.That(
                    converter.ToTickAtOrBeforeSampleFrame(sampleFrame),
                    Is.EqualTo(expectedTick),
                    $"Unexpected inverse tick for sample frame {sampleFrame}.");
            }
        }

        [Test]
        public void RepeatedInverseConversionsDoNotAllocate()
        {
            var converter = new Core.TickSampleConverter(Core.TempoMap.Default, 48_000);
            long checksum = converter.ToTickAtOrBeforeSampleFrame(1_000_000UL);

            long beforeBytes = GC.GetAllocatedBytesForCurrentThread();

            for (ulong index = 0; index < 10_000UL; index++)
            {
                checksum ^= converter.ToTickAtOrBeforeSampleFrame(
                    1_000_000UL + index);
            }

            long afterBytes = GC.GetAllocatedBytesForCurrentThread();

            Assert.That(checksum, Is.Not.EqualTo(long.MinValue));
            Assert.That(afterBytes - beforeBytes, Is.Zero);
        }

        [Test]
        public void ConverterDoesNotAccumulateDriftOverTenMinutes()
        {
            var converter = new Core.TickSampleConverter(Core.TempoMap.Default, 48_000);
            long tenMinutesAtDefaultTempo = 1_152_000;

            Assert.That(
                converter.ToSampleFrame(tenMinutesAtDefaultTempo),
                Is.EqualTo(28_800_000UL));
        }

        [Test]
        public void ConverterRejectsNullTempoMap()
        {
            Assert.Throws<ArgumentNullException>(
                () => new Core.TickSampleConverter(null, 48_000));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ConverterRejectsNonPositiveSampleRate(int sampleRate)
        {
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.TickSampleConverter(Core.TempoMap.Default, sampleRate));

            Assert.That(exception.ParamName, Is.EqualTo("sampleRate"));
        }

        [TestCase(-1)]
        [TestCase(long.MinValue)]
        public void ConverterRejectsNegativeTicks(long tick)
        {
            var converter = new Core.TickSampleConverter(Core.TempoMap.Default, 48_000);

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => converter.ToSampleFrame(tick));

            Assert.That(exception.ParamName, Is.EqualTo("tick"));
        }

        [Test]
        public void ConverterResolvesInt64MaximumWhenFramesRemainRepresentable()
        {
            var converter = new Core.TickSampleConverter(Core.TempoMap.Default, 1);

            Assert.That(
                converter.ToSampleFrame(long.MaxValue),
                Is.EqualTo(4_803_839_602_528_529UL));
        }

        [Test]
        public void ConverterRejectsInt64MaximumWhenFramesExceedUInt64()
        {
            var converter = new Core.TickSampleConverter(Core.TempoMap.Default, 48_000);

            Assert.Throws<OverflowException>(() => converter.ToSampleFrame(long.MaxValue));
        }

        [Test]
        public void ConverterRejectsTempoWhoseSamplePositionExceedsUInt64()
        {
            var tempoMap = new Core.TempoMap(new Core.TempoSegment(0, 1e-300));
            var converter = new Core.TickSampleConverter(tempoMap, 48_000);

            Assert.Throws<OverflowException>(() => converter.ToSampleFrame(1));
        }
    }
}
