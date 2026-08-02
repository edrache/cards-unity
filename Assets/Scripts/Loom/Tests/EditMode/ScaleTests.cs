using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class ScaleTests
    {
        [Test]
        public void ConstructorRejectsNullOrEmptyIntervals()
        {
            Assert.Throws<ArgumentNullException>(() => new Core.Scale(null));
            Assert.Throws<ArgumentException>(() => new Core.Scale());
        }

        [Test]
        public void ConstructorRequiresTonicAsFirstInterval()
        {
            Assert.Throws<ArgumentException>(() => new Core.Scale(1, 3, 5));
        }

        [TestCase(-1)]
        [TestCase(12)]
        public void ConstructorRejectsIntervalsOutsideOneOctave(int interval)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.Scale(0, interval));
        }

        [TestCase(0, 2, 2)]
        [TestCase(0, 4, 2)]
        public void ConstructorRejectsDuplicateOrDescendingIntervals(
            int first,
            int second,
            int third)
        {
            Assert.Throws<ArgumentException>(
                () => new Core.Scale(first, second, third));
        }

        [Test]
        public void ScaleOwnsADefensiveCopyOfIntervals()
        {
            var intervals = new[] { 0, 2, 4, 5, 7, 9, 11 };
            var scale = new Core.Scale(intervals);

            intervals[1] = 1;

            Assert.That(scale.Count, Is.EqualTo(7));
            Assert.That(scale[1], Is.EqualTo(2));
        }

        [Test]
        public void IndexerRejectsIndicesOutsideScale()
        {
            var scale = new Core.Scale(0, 2, 4);

            Assert.Throws<ArgumentOutOfRangeException>(() => _ = scale[-1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = scale[3]);
        }

        [Test]
        public void MajorScaleResolvesAscendingDegreesFromTonic()
        {
            Core.Scale scale = CreateMajorScale();
            var actual = new int[8];

            for (int degree = 0; degree < actual.Length; degree++)
            {
                actual[degree] = scale.Resolve(
                    new Core.Note(60),
                    new Core.ScaleDegreePitch(degree)).MidiNumber;
            }

            Assert.That(actual, Is.EqualTo(new[] { 60, 62, 64, 65, 67, 69, 71, 72 }));
        }

        [TestCase(-1, 59)]
        [TestCase(-7, 48)]
        [TestCase(-8, 47)]
        [TestCase(7, 72)]
        [TestCase(14, 84)]
        public void ResolutionWrapsSignedDegreesAcrossOctaves(int degree, int expectedMidi)
        {
            Core.Note note = CreateMajorScale().Resolve(
                new Core.Note(60),
                new Core.ScaleDegreePitch(degree));

            Assert.That(note.MidiNumber, Is.EqualTo(expectedMidi));
        }

        [TestCase(-1, 1, 71)]
        [TestCase(0, -1, 48)]
        [TestCase(7, -1, 60)]
        public void OctaveOffsetCombinesWithDegreeWrapping(
            int degree,
            int octaveOffset,
            int expectedMidi)
        {
            Core.Note note = CreateMajorScale().Resolve(
                new Core.Note(60),
                new Core.ScaleDegreePitch(degree, octaveOffset));

            Assert.That(note.MidiNumber, Is.EqualTo(expectedMidi));
        }

        [Test]
        public void SingleDegreeScaleResolvesSuccessiveOctaves()
        {
            var scale = new Core.Scale(0);

            Assert.That(
                scale.Resolve(new Core.Note(60), new Core.ScaleDegreePitch(-1)).MidiNumber,
                Is.EqualTo(48));
            Assert.That(
                scale.Resolve(new Core.Note(60), new Core.ScaleDegreePitch(1)).MidiNumber,
                Is.EqualTo(72));
        }

        [Test]
        public void ChromaticScaleResolvesEverySemitone()
        {
            var scale = new Core.Scale(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);

            for (int degree = 0; degree <= 12; degree++)
            {
                Assert.That(
                    scale.Resolve(new Core.Note(60), new Core.ScaleDegreePitch(degree))
                        .MidiNumber,
                    Is.EqualTo(60 + degree));
            }
        }

        [Test]
        public void ResolveAcceptsMidiBoundaryNotes()
        {
            var scale = new Core.Scale(0, 11);

            Assert.That(
                scale.Resolve(new Core.Note(0), default).MidiNumber,
                Is.EqualTo(Core.Note.MinMidiNumber));
            Assert.That(
                scale.Resolve(new Core.Note(116), new Core.ScaleDegreePitch(1)).MidiNumber,
                Is.EqualTo(Core.Note.MaxMidiNumber));
        }

        [TestCase(0, -1)]
        [TestCase(127, 1)]
        public void ResolveRejectsResultsOutsideMidiRange(int tonicMidi, int degree)
        {
            var scale = new Core.Scale(0);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => scale.Resolve(
                    new Core.Note(tonicMidi),
                    new Core.ScaleDegreePitch(degree)));
        }

        [Test]
        public void ResolveRejectsExtremePitchWithoutArithmeticOverflow()
        {
            Core.Scale scale = CreateMajorScale();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => scale.Resolve(
                    new Core.Note(60),
                    new Core.ScaleDegreePitch(int.MinValue, int.MinValue)));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => scale.Resolve(
                    new Core.Note(60),
                    new Core.ScaleDegreePitch(int.MaxValue, int.MaxValue)));
        }

        [Test]
        public void ResolutionIsIndependentOfEvaluationHistory()
        {
            Core.Scale scale = CreateMajorScale();
            var pitch = new Core.ScaleDegreePitch(9, -1);
            Core.Note expected = scale.Resolve(new Core.Note(60), pitch);

            scale.Resolve(new Core.Note(60), new Core.ScaleDegreePitch(0));
            scale.Resolve(new Core.Note(48), new Core.ScaleDegreePitch(6));

            Assert.That(scale.Resolve(new Core.Note(60), pitch), Is.EqualTo(expected));
        }

        [Test]
        public void ResolvedScalePitchFlowsThroughExistingSequencerBoundary()
        {
            Core.Note resolvedNote = CreateMajorScale().Resolve(
                new Core.Note(60),
                new Core.ScaleDegreePitch(2));
            var pattern = new Core.StepPattern(
                4,
                new Core.PatternStep(resolvedNote, 100, 120L));
            var sequencer = new Core.StepSequencer(pattern, 0UL, 0UL);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(1);

            Assert.That(sequencer.TryScheduleThrough(1L, buffer), Is.True);
            Assert.That(buffer.TryDequeue(out Core.ScheduledNoteEvent scheduled), Is.True);
            Assert.That(scheduled.NoteEvent.Note.MidiNumber, Is.EqualTo(64));
        }

        [Test]
        public void ResolutionHotPathAllocatesNoManagedMemoryAfterWarmup()
        {
            Core.Scale scale = CreateMajorScale();
            var tonic = new Core.Note(60);
            var pitch = new Core.ScaleDegreePitch(3);
            scale.Resolve(tonic, pitch);
            long before = GC.GetAllocatedBytesForCurrentThread();
            int checksum = 0;

            for (int iteration = 0; iteration < 10_000; iteration++)
            {
                checksum += scale.Resolve(tonic, pitch).MidiNumber;
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(checksum, Is.EqualTo(650_000));
            Assert.That(allocatedBytes, Is.Zero);
        }

        private static Core.Scale CreateMajorScale()
        {
            return new Core.Scale(0, 2, 4, 5, 7, 9, 11);
        }
    }
}
