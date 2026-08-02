using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class TempoMapTests
    {
        [Test]
        public void DefaultTempoMapStartsAtTickZeroAtOneHundredTwentyBeatsPerMinute()
        {
            Core.TempoMap map = Core.TempoMap.Default;
            Core.TempoSegment segment = map[0];

            Assert.That(Core.TempoMap.DefaultBeatsPerMinute, Is.EqualTo(120d));
            Assert.That(map.Count, Is.EqualTo(1));
            Assert.That(segment.StartTick, Is.Zero);
            Assert.That(segment.BeatsPerMinute, Is.EqualTo(120d));
            Assert.That(map.GetSegmentAtTick(0), Is.EqualTo(segment));
            Assert.That(map.GetSegmentAtTick(1_000_000), Is.EqualTo(segment));
            Assert.That(map.GetSegmentAtTick(long.MaxValue), Is.EqualTo(segment));
        }

        [Test]
        public void TempoMapRejectsNullSegments()
        {
            Assert.Throws<ArgumentNullException>(
                () => new Core.TempoMap((Core.TempoSegment[])null));
        }

        [Test]
        public void TempoMapRejectsEmptySegments()
        {
            Assert.Throws<ArgumentException>(() => new Core.TempoMap());
        }

        [TestCase(1)]
        [TestCase(long.MaxValue)]
        public void TempoMapRequiresFirstSegmentAtTickZero(long firstStartTick)
        {
            Core.TempoSegment first = new Core.TempoSegment(firstStartTick, 120d);

            Assert.Throws<ArgumentException>(() => new Core.TempoMap(first));
        }

        [Test]
        public void TempoMapRequiresStrictlyIncreasingSegmentTicks()
        {
            Core.TempoSegment first = new Core.TempoSegment(0, 120d);
            Core.TempoSegment later = new Core.TempoSegment(1_920, 90d);
            Core.TempoSegment earlier = new Core.TempoSegment(960, 100d);

            Assert.Throws<ArgumentException>(() => new Core.TempoMap(first, later, earlier));
        }

        [Test]
        public void TempoMapRejectsDuplicateSegmentTicks()
        {
            Core.TempoSegment first = new Core.TempoSegment(0, 120d);
            Core.TempoSegment duplicateStart = new Core.TempoSegment(0, 90d);
            Core.TempoSegment second = new Core.TempoSegment(960, 90d);
            Core.TempoSegment duplicateSecond = new Core.TempoSegment(960, 100d);

            Assert.Throws<ArgumentException>(() => new Core.TempoMap(first, duplicateStart));
            Assert.Throws<ArgumentException>(
                () => new Core.TempoMap(first, second, duplicateSecond));
        }

        [Test]
        public void TempoMapRejectsDefaultTempoSegments()
        {
            Core.TempoSegment valid = new Core.TempoSegment(0, 120d);

            Assert.Throws<ArgumentException>(() => new Core.TempoMap(default(Core.TempoSegment)));
            Assert.Throws<ArgumentException>(() => new Core.TempoMap(valid, default));
        }

        [TestCase(0, 0, 120d)]
        [TestCase(1, 0, 120d)]
        [TestCase(959, 0, 120d)]
        [TestCase(960, 960, 90d)]
        [TestCase(961, 960, 90d)]
        [TestCase(3_839, 960, 90d)]
        [TestCase(3_840, 3_840, 150d)]
        [TestCase(3_841, 3_840, 150d)]
        [TestCase(long.MaxValue, 3_840, 150d)]
        public void TempoMapResolvesSegmentsAcrossBoundaries(
            long tick,
            long expectedStartTick,
            double expectedBeatsPerMinute)
        {
            var map = new Core.TempoMap(
                new Core.TempoSegment(0, 120d),
                new Core.TempoSegment(960, 90d),
                new Core.TempoSegment(3_840, 150d));

            Core.TempoSegment segment = map.GetSegmentAtTick(tick);

            Assert.That(segment.StartTick, Is.EqualTo(expectedStartTick));
            Assert.That(segment.BeatsPerMinute, Is.EqualTo(expectedBeatsPerMinute));
        }

        [Test]
        public void TempoMapResolvesSegmentStartingAtInt64Maximum()
        {
            Core.TempoSegment first = new Core.TempoSegment(0, 120d);
            Core.TempoSegment last = new Core.TempoSegment(long.MaxValue, 60d);
            var map = new Core.TempoMap(first, last);

            Assert.That(map.GetSegmentAtTick(long.MaxValue - 1), Is.EqualTo(first));
            Assert.That(map.GetSegmentAtTick(long.MaxValue), Is.EqualTo(last));
        }

        [TestCase(-1)]
        [TestCase(long.MinValue)]
        public void TempoMapRejectsNegativeLookupTicks(long tick)
        {
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => Core.TempoMap.Default.GetSegmentAtTick(tick));

            Assert.That(exception.ParamName, Is.EqualTo("tick"));
        }

        [Test]
        public void TempoMapDefensivelyCopiesInputSegments()
        {
            Core.TempoSegment expectedFirst = new Core.TempoSegment(0, 120d);
            Core.TempoSegment expectedSecond = new Core.TempoSegment(960, 90d);
            Core.TempoSegment[] source = { expectedFirst, expectedSecond };
            var map = new Core.TempoMap(source);

            source[0] = new Core.TempoSegment(0, 60d);
            source[1] = new Core.TempoSegment(1_920, 180d);

            Assert.That(map.Count, Is.EqualTo(2));
            Assert.That(map[0], Is.EqualTo(expectedFirst));
            Assert.That(map[1], Is.EqualTo(expectedSecond));
            Assert.That(map.GetSegmentAtTick(1_000), Is.EqualTo(expectedSecond));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void TempoMapRejectsIndexesOutsideOwnedSegments(int index)
        {
            var map = new Core.TempoMap(
                new Core.TempoSegment(0, 120d),
                new Core.TempoSegment(960, 90d));

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => _ = map[index]);

            Assert.That(exception.ParamName, Is.EqualTo("index"));
        }
    }
}
