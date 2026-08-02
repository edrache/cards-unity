using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class MusicalTimeTests
    {
        [Test]
        public void MusicalTimeUsesFourFourAtNineHundredSixtyTicksPerQuarterNote()
        {
            Assert.That(Core.MusicalTime.TicksPerQuarterNote, Is.EqualTo(960));
            Assert.That(Core.MusicalTime.TimeSignatureNumerator, Is.EqualTo(4));
            Assert.That(Core.MusicalTime.TimeSignatureDenominator, Is.EqualTo(4));
            Assert.That(Core.MusicalTime.TicksPerBeat, Is.EqualTo(960));
            Assert.That(Core.MusicalTime.TicksPerBar, Is.EqualTo(3_840));
        }

        [Test]
        public void MusicalTimeAcceptsSessionStart()
        {
            Core.MusicalTime time = new Core.MusicalTime(0);

            Assert.That(time.Tick, Is.Zero);
            Assert.That(time.BarIndex, Is.Zero);
            Assert.That(time.BeatIndex, Is.Zero);
            Assert.That(time.TickInBeat, Is.Zero);
        }

        [TestCase(-1)]
        [TestCase(long.MinValue)]
        public void MusicalTimeRejectsNegativeTicks(long tick)
        {
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.MusicalTime(tick));

            Assert.That(exception.ParamName, Is.EqualTo("tick"));
        }

        [TestCase(959, 0, 0, 959)]
        [TestCase(960, 0, 1, 0)]
        [TestCase(961, 0, 1, 1)]
        [TestCase(1_919, 0, 1, 959)]
        [TestCase(1_920, 0, 2, 0)]
        [TestCase(2_879, 0, 2, 959)]
        [TestCase(2_880, 0, 3, 0)]
        [TestCase(3_839, 0, 3, 959)]
        [TestCase(3_840, 1, 0, 0)]
        [TestCase(3_841, 1, 0, 1)]
        public void MusicalTimeDecomposesQuarterNoteAndBarBoundaries(
            long tick,
            long expectedBarIndex,
            int expectedBeatIndex,
            int expectedTickInBeat)
        {
            Core.MusicalTime time = new Core.MusicalTime(tick);

            Assert.That(time.BarIndex, Is.EqualTo(expectedBarIndex));
            Assert.That(time.BeatIndex, Is.EqualTo(expectedBeatIndex));
            Assert.That(time.TickInBeat, Is.EqualTo(expectedTickInBeat));
        }

        [Test]
        public void MusicalTimeDecomposesLongSessionsWithoutPrecisionLoss()
        {
            Core.MusicalTime time = new Core.MusicalTime(3_840_001_937);

            Assert.That(time.BarIndex, Is.EqualTo(1_000_000));
            Assert.That(time.BeatIndex, Is.EqualTo(2));
            Assert.That(time.TickInBeat, Is.EqualTo(17));
        }

        [Test]
        public void MusicalTimeDecomposesInt64MaximumWithoutOverflow()
        {
            Core.MusicalTime time = new Core.MusicalTime(long.MaxValue);

            Assert.That(time.Tick, Is.EqualTo(long.MaxValue));
            Assert.That(time.BarIndex, Is.EqualTo(2_401_919_801_264_264));
            Assert.That(time.BeatIndex, Is.EqualTo(2));
            Assert.That(time.TickInBeat, Is.EqualTo(127));
        }

        [Test]
        public void DefaultMusicalTimeRepresentsSessionStart()
        {
            Core.MusicalTime defaultTime = default;
            Core.MusicalTime sessionStart = new Core.MusicalTime(0);

            Assert.That(defaultTime, Is.EqualTo(sessionStart));
            Assert.That(defaultTime.BarIndex, Is.Zero);
            Assert.That(defaultTime.BeatIndex, Is.Zero);
            Assert.That(defaultTime.TickInBeat, Is.Zero);
        }

        [Test]
        public void EqualMusicalTimesHaveValueSemantics()
        {
            Core.MusicalTime first = new Core.MusicalTime(12_345);
            Core.MusicalTime second = new Core.MusicalTime(12_345);
            Core.MusicalTime different = new Core.MusicalTime(12_346);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first == second, Is.True);
            Assert.That(first != different, Is.True);
        }
    }
}
