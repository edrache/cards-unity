using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class PercussionNoteTests
    {
        [Test]
        public void DefaultPercussionNoteIsInvalidAndZeroIsRejected()
        {
            Core.PercussionNote note = default;

            Assert.That(note.IsValid, Is.False);
            Assert.That(note.SampleSlotId, Is.Zero);
            Assert.That(note.ToString(), Is.EqualTo("Invalid"));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.PercussionNote(0UL));
        }

        [Test]
        public void PercussionNoteAcceptsCompleteNonZeroSlotRange()
        {
            var firstSlot = new Core.PercussionNote(1UL);
            var finalSlot = new Core.PercussionNote(ulong.MaxValue);

            Assert.That(firstSlot.IsValid, Is.True);
            Assert.That(firstSlot.SampleSlotId, Is.EqualTo(1UL));
            Assert.That(finalSlot.IsValid, Is.True);
            Assert.That(finalSlot.SampleSlotId, Is.EqualTo(ulong.MaxValue));
        }

        [Test]
        public void PercussionNoteUsesStableValueSemantics()
        {
            var first = new Core.PercussionNote(42UL);
            var equal = new Core.PercussionNote(42UL);
            var different = new Core.PercussionNote(43UL);

            Assert.That(first, Is.EqualTo(equal));
            Assert.That(first.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(first == equal, Is.True);
            Assert.That(first != different, Is.True);
            Assert.That(first.ToString(), Is.EqualTo("42"));
        }

        [Test]
        public void PercussionSlotIdentityIsNotBoundedByMidiPitchRange()
        {
            ulong sampleSlotId = (ulong)Core.Note.MaxMidiNumber + 1UL;
            var note = new Core.PercussionNote(sampleSlotId);

            Assert.That(note.SampleSlotId, Is.EqualTo(128UL));
        }
    }
}
