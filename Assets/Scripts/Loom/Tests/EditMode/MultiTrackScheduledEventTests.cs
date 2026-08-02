using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class MultiTrackScheduledEventTests
    {
        [Test]
        public void PitchedEventRetainsTrackAndTypedPayload()
        {
            var noteEvent = new Core.NoteEvent(new Core.Note(60), 90, 12L, 24L);
            var scheduled = new Core.ScheduledNoteEvent(noteEvent, 7UL);

            Core.MultiTrackScheduledEvent trackEvent =
                Core.MultiTrackScheduledEvent.FromPitched(
                    new Core.TrackId(3UL),
                    scheduled);

            Assert.That(trackEvent.TrackId, Is.EqualTo(new Core.TrackId(3UL)));
            Assert.That(trackEvent.Kind, Is.EqualTo(Core.MultiTrackEventKind.Pitched));
            Assert.That(trackEvent.StartTick, Is.EqualTo(12L));
            Assert.That(trackEvent.TrackSequenceNumber, Is.EqualTo(7UL));
            Assert.That(trackEvent.TryGetPitchedEvent(out var result), Is.True);
            Assert.That(result, Is.EqualTo(scheduled));
            Assert.That(trackEvent.TryGetPercussionEvent(out _), Is.False);
        }

        [Test]
        public void PercussionEventRetainsFullSlotIdentity()
        {
            var noteEvent = new Core.PercussionNoteEvent(
                new Core.PercussionNote(ulong.MaxValue),
                100,
                18L,
                30L);
            var scheduled = new Core.ScheduledPercussionNoteEvent(noteEvent, 9UL);

            Core.MultiTrackScheduledEvent trackEvent =
                Core.MultiTrackScheduledEvent.FromPercussion(
                    new Core.TrackId(5UL),
                    scheduled);

            Assert.That(trackEvent.Kind, Is.EqualTo(Core.MultiTrackEventKind.Percussion));
            Assert.That(trackEvent.StartTick, Is.EqualTo(18L));
            Assert.That(trackEvent.TrackSequenceNumber, Is.EqualTo(9UL));
            Assert.That(trackEvent.TryGetPercussionEvent(out var result), Is.True);
            Assert.That(result, Is.EqualTo(scheduled));
            Assert.That(trackEvent.TryGetPitchedEvent(out _), Is.False);
        }
    }
}
