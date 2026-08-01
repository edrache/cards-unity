using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class NoteContractTests
    {
        [TestCase(Core.Note.MinMidiNumber)]
        [TestCase(Core.Note.MaxMidiNumber)]
        public void NoteAcceptsMidiBoundaries(int midiNumber)
        {
            Core.Note note = new Core.Note(midiNumber);

            Assert.That(note.MidiNumber, Is.EqualTo(midiNumber));
        }

        [TestCase(Core.Note.MinMidiNumber - 1)]
        [TestCase(Core.Note.MaxMidiNumber + 1)]
        public void NoteRejectsMidiNumbersOutsideRange(int midiNumber)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Core.Note(midiNumber));
        }

        [Test]
        public void EqualNotesHaveValueSemantics()
        {
            Core.Note first = new Core.Note(60);
            Core.Note second = new Core.Note(60);
            Core.Note different = new Core.Note(61);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first == second, Is.True);
            Assert.That(first != different, Is.True);
        }

        [TestCase(21, 27.5)]
        [TestCase(57, 220.0)]
        [TestCase(Core.Note.ConcertAMidiNumber, Core.Note.ConcertAFrequencyHz)]
        [TestCase(60, 261.6255653005986)]
        public void NoteConvertsReferenceMidiNumbersToFrequency(
            int midiNumber,
            double expectedFrequencyHz)
        {
            Core.Note note = new Core.Note(midiNumber);

            Assert.That(
                note.FrequencyHz,
                Is.EqualTo(expectedFrequencyHz).Within(expectedFrequencyHz * 1e-12));
        }

        [TestCase(Core.Note.MinMidiNumber, 8.175798915643707)]
        [TestCase(Core.Note.MaxMidiNumber, 12543.853951415975)]
        public void NoteConvertsMidiRangeEndpointsToFrequency(
            int midiNumber,
            double expectedFrequencyHz)
        {
            Core.Note note = new Core.Note(midiNumber);

            Assert.That(
                note.FrequencyHz,
                Is.EqualTo(expectedFrequencyHz).Within(expectedFrequencyHz * 1e-12));
        }

        [Test]
        public void FrequencyIncreasesAcrossTheMidiRange()
        {
            double previousFrequencyHz = new Core.Note(Core.Note.MinMidiNumber).FrequencyHz;

            for (int midiNumber = Core.Note.MinMidiNumber + 1;
                midiNumber <= Core.Note.MaxMidiNumber;
                midiNumber++)
            {
                double frequencyHz = new Core.Note(midiNumber).FrequencyHz;

                Assert.That(frequencyHz, Is.GreaterThan(previousFrequencyHz));
                previousFrequencyHz = frequencyHz;
            }
        }

        [Test]
        public void EveryTwelveSemitonesDoubleFrequency()
        {
            for (int midiNumber = Core.Note.MinMidiNumber;
                midiNumber <= Core.Note.MaxMidiNumber - 12;
                midiNumber++)
            {
                double lowerFrequencyHz = new Core.Note(midiNumber).FrequencyHz;
                double upperFrequencyHz = new Core.Note(midiNumber + 12).FrequencyHz;

                Assert.That(
                    upperFrequencyHz,
                    Is.EqualTo(lowerFrequencyHz * 2.0).Within(lowerFrequencyHz * 2e-12));
            }
        }

        [Test]
        public void NoteEventExposesAnImmutableTickInterval()
        {
            Core.Note note = new Core.Note(64);
            Core.NoteEvent noteEvent = new Core.NoteEvent(note, 96, 1_920, 480);

            Assert.That(noteEvent.Note, Is.EqualTo(note));
            Assert.That(noteEvent.Velocity, Is.EqualTo(96));
            Assert.That(noteEvent.StartTick, Is.EqualTo(1_920));
            Assert.That(noteEvent.DurationTicks, Is.EqualTo(480));
            Assert.That(noteEvent.EndTick, Is.EqualTo(2_400));
        }

        [TestCase(Core.NoteEvent.MinVelocity - 1)]
        [TestCase(Core.NoteEvent.MaxVelocity + 1)]
        public void NoteEventRejectsVelocityOutsidePlayableRange(int velocity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.NoteEvent(new Core.Note(60), velocity, 0, 1));
        }

        [Test]
        public void NoteEventRejectsInvalidTickIntervals()
        {
            Core.Note note = new Core.Note(60);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.NoteEvent(note, 100, -1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.NoteEvent(note, 100, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.NoteEvent(note, 100, long.MaxValue, 1));
        }

        [Test]
        public void EqualNoteEventsHaveValueSemantics()
        {
            Core.NoteEvent first = new Core.NoteEvent(new Core.Note(60), 100, 960, 240);
            Core.NoteEvent second = new Core.NoteEvent(new Core.Note(60), 100, 960, 240);
            Core.NoteEvent different = new Core.NoteEvent(new Core.Note(60), 100, 960, 241);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first == second, Is.True);
            Assert.That(first != different, Is.True);
        }
    }
}
