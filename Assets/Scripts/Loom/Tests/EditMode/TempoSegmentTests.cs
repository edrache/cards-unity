using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class TempoSegmentTests
    {
        [Test]
        public void TempoSegmentExposesImmutableStartTickAndTempo()
        {
            Core.TempoSegment segment = new Core.TempoSegment(3_840, 120d);

            Assert.That(segment.StartTick, Is.EqualTo(3_840));
            Assert.That(segment.BeatsPerMinute, Is.EqualTo(120d));
        }

        [TestCase(0)]
        [TestCase(long.MaxValue)]
        public void TempoSegmentAcceptsNonNegativeStartTickBoundaries(long startTick)
        {
            Core.TempoSegment segment = new Core.TempoSegment(startTick, 120d);

            Assert.That(segment.StartTick, Is.EqualTo(startTick));
        }

        [TestCase(-1)]
        [TestCase(long.MinValue)]
        public void TempoSegmentRejectsNegativeStartTicks(long startTick)
        {
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.TempoSegment(startTick, 120d));

            Assert.That(exception.ParamName, Is.EqualTo("startTick"));
        }

        [TestCase(1e-300)]
        [TestCase(120d)]
        [TestCase(double.MaxValue)]
        public void TempoSegmentAcceptsFinitePositiveTempo(double beatsPerMinute)
        {
            Core.TempoSegment segment = new Core.TempoSegment(0, beatsPerMinute);

            Assert.That(segment.BeatsPerMinute, Is.EqualTo(beatsPerMinute));
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(double.Epsilon)]
        public void TempoSegmentRejectsNonPositiveOrNonFiniteTempo(double beatsPerMinute)
        {
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.TempoSegment(0, beatsPerMinute));

            Assert.That(exception.ParamName, Is.EqualTo("beatsPerMinute"));
        }

        [Test]
        public void EqualTempoSegmentsHaveValueSemantics()
        {
            Core.TempoSegment first = new Core.TempoSegment(960, 90d);
            Core.TempoSegment second = new Core.TempoSegment(960, 90d);
            Core.TempoSegment differentTick = new Core.TempoSegment(961, 90d);
            Core.TempoSegment differentTempo = new Core.TempoSegment(960, 91d);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first == second, Is.True);
            Assert.That(first != differentTick, Is.True);
            Assert.That(first != differentTempo, Is.True);
        }
    }
}
