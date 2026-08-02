using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class ScheduledPercussionNoteEventTests
    {
        [Test]
        public void ConstructorPreservesLogicalEventAndSequenceNumber()
        {
            var noteEvent = new Core.PercussionNoteEvent(new Core.PercussionNote(60), 100, 240L, 120L);
            var scheduledEvent = new Core.ScheduledPercussionNoteEvent(noteEvent, 42UL);

            Assert.That(scheduledEvent.PercussionNoteEvent, Is.EqualTo(noteEvent));
            Assert.That(scheduledEvent.StartTick, Is.EqualTo(240L));
            Assert.That(scheduledEvent.SequenceNumber, Is.EqualTo(42UL));
        }

        [Test]
        public void ConstructorRejectsDefaultLogicalEvent()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new Core.ScheduledPercussionNoteEvent(default, 0UL));

            Assert.That(exception.ParamName, Is.EqualTo("noteEvent"));
        }

        [Test]
        public void ValueEqualityIncludesLogicalEventAndSequenceNumber()
        {
            var noteEvent = new Core.PercussionNoteEvent(new Core.PercussionNote(60), 100, 240L, 120L);
            var left = new Core.ScheduledPercussionNoteEvent(noteEvent, 42UL);
            var equal = new Core.ScheduledPercussionNoteEvent(noteEvent, 42UL);
            var different = new Core.ScheduledPercussionNoteEvent(noteEvent, 43UL);

            Assert.That(left, Is.EqualTo(equal));
            Assert.That(left.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(left == equal, Is.True);
            Assert.That(left != different, Is.True);
        }
    }
}
