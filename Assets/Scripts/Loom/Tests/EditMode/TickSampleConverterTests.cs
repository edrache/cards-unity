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
