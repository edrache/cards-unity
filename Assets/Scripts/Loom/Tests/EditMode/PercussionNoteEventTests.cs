using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class PercussionNoteEventTests
    {
        [Test]
        public void ConstructorPreservesTheExplicitSlotAndImmutableTickInterval()
        {
            var note = new Core.PercussionNote(128UL);
            var noteEvent = new Core.PercussionNoteEvent(note, 96, 1_920L, 480L);

            Assert.That(noteEvent.Note, Is.EqualTo(note));
            Assert.That(noteEvent.Velocity, Is.EqualTo(96));
            Assert.That(noteEvent.StartTick, Is.EqualTo(1_920L));
            Assert.That(noteEvent.DurationTicks, Is.EqualTo(480L));
            Assert.That(noteEvent.EndTick, Is.EqualTo(2_400L));
        }

        [Test]
        public void ConstructorRejectsAnInvalidPercussionSlot()
        {
            Assert.Throws<ArgumentException>(
                () => new Core.PercussionNoteEvent(default, 100, 0L, 1L));
        }

        [TestCase(Core.PercussionNoteEvent.MinVelocity - 1)]
        [TestCase(Core.PercussionNoteEvent.MaxVelocity + 1)]
        public void ConstructorRejectsVelocityOutsidePlayableRange(int velocity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.PercussionNoteEvent(new Core.PercussionNote(1UL), velocity, 0L, 1L));
        }

        [Test]
        public void ConstructorRejectsInvalidTickIntervals()
        {
            var note = new Core.PercussionNote(1UL);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.PercussionNoteEvent(note, 100, -1L, 1L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.PercussionNoteEvent(note, 100, 0L, 0L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.PercussionNoteEvent(note, 100, long.MaxValue, 1L));
        }

        [Test]
        public void EqualEventsHaveValueSemantics()
        {
            var first = new Core.PercussionNoteEvent(
                new Core.PercussionNote(7UL),
                100,
                960L,
                240L);
            var equal = new Core.PercussionNoteEvent(
                new Core.PercussionNote(7UL),
                100,
                960L,
                240L);
            var different = new Core.PercussionNoteEvent(
                new Core.PercussionNote(8UL),
                100,
                960L,
                240L);

            Assert.That(first, Is.EqualTo(equal));
            Assert.That(first.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(first == equal, Is.True);
            Assert.That(first != different, Is.True);
        }
    }
}
