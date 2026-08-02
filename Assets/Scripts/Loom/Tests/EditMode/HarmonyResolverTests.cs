using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class HarmonyResolverTests
    {
        [Test]
        public void ConstructorRejectsMissingScaleOrHarmonyPlan()
        {
            var scale = new Core.Scale(0, 2, 4, 5, 7, 9, 11);
            var plan = new Core.HarmonyPlan(new Core.HarmonyStep(0, 960L));

            Assert.Throws<ArgumentNullException>(
                () => new Core.HarmonyResolver(null, new Core.Note(60), plan));
            Assert.Throws<ArgumentNullException>(
                () => new Core.HarmonyResolver(scale, new Core.Note(60), null));
        }

        [Test]
        public void LeadPreservesFreeScaleDegreeResolution()
        {
            Core.HarmonyResolver resolver = CreateResolver();

            Core.Note note = resolver.Resolve(
                new Core.ScaleDegreePitch(3),
                960L,
                Core.TrackRole.Lead);

            Assert.That(note.MidiNumber, Is.EqualTo(65));
        }

        [TestCase(0L, 0, 60)]
        [TestCase(959L, 1, 72)]
        [TestCase(960L, 0, 67)]
        [TestCase(1_920L, -1, 48)]
        public void BassUsesActiveHarmonyRootAndRequestedOctave(
            long tick,
            int octaveOffset,
            int expectedMidi)
        {
            Core.Note note = CreateResolver().Resolve(
                new Core.ScaleDegreePitch(99, octaveOffset),
                tick,
                Core.TrackRole.Bass);

            Assert.That(note.MidiNumber, Is.EqualTo(expectedMidi));
        }

        [TestCase(0, 60)]
        [TestCase(1, 64)]
        [TestCase(2, 67)]
        [TestCase(3, 72)]
        [TestCase(-1, 55)]
        public void PadMapsSignedDegreesToDiatonicTriadTones(
            int degree,
            int expectedMidi)
        {
            Core.Note note = CreateResolver().Resolve(
                new Core.ScaleDegreePitch(degree),
                0L,
                Core.TrackRole.Pad);

            Assert.That(note.MidiNumber, Is.EqualTo(expectedMidi));
        }

        [Test]
        public void ResolutionUsesExactHarmonyBoundaryAndCyclesPlan()
        {
            Core.HarmonyResolver resolver = CreateResolver();

            Assert.That(
                resolver.Resolve(default, 959L, Core.TrackRole.Bass).MidiNumber,
                Is.EqualTo(60));
            Assert.That(
                resolver.Resolve(default, 960L, Core.TrackRole.Bass).MidiNumber,
                Is.EqualTo(67));
            Assert.That(
                resolver.Resolve(default, 1_920L, Core.TrackRole.Bass).MidiNumber,
                Is.EqualTo(60));
        }

        [Test]
        public void ResolutionRejectsUndefinedTrackRoles()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateResolver().Resolve(default, 0L, default));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateResolver().Resolve(
                    default,
                    0L,
                    (Core.TrackRole)int.MaxValue));
        }

        [Test]
        public void ResolutionReportsControlledPitchRangeFailures()
        {
            var scale = new Core.Scale(0, 2, 4, 5, 7, 9, 11);
            var plan = new Core.HarmonyPlan(
                new Core.HarmonyStep(int.MaxValue, 1L));
            var resolver = new Core.HarmonyResolver(scale, new Core.Note(60), plan);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => resolver.Resolve(default, 0L, Core.TrackRole.Bass));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => resolver.Resolve(
                    new Core.ScaleDegreePitch(int.MaxValue),
                    0L,
                    Core.TrackRole.Pad));
        }

        [Test]
        public void ResolutionIsIndependentOfEvaluationHistory()
        {
            Core.HarmonyResolver resolver = CreateResolver();
            var pitch = new Core.ScaleDegreePitch(2, -1);
            Core.Note expected = resolver.Resolve(pitch, 960L, Core.TrackRole.Pad);

            resolver.Resolve(default, 0L, Core.TrackRole.Bass);
            resolver.Resolve(new Core.ScaleDegreePitch(6), 0L, Core.TrackRole.Lead);

            Assert.That(resolver.Resolve(pitch, 960L, Core.TrackRole.Pad), Is.EqualTo(expected));
        }

        [Test]
        public void ResolvedHarmonyPitchFlowsThroughExistingSequencerBoundary()
        {
            Core.Note resolvedNote = CreateResolver().Resolve(
                new Core.ScaleDegreePitch(1),
                960L,
                Core.TrackRole.Pad);
            var pattern = new Core.StepPattern(
                4,
                new Core.PatternStep(resolvedNote, 100, 120L));
            var sequencer = new Core.StepSequencer(pattern, 0UL, 0UL);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(1);

            Assert.That(sequencer.TryScheduleThrough(1L, buffer), Is.True);
            Assert.That(buffer.TryDequeue(out Core.ScheduledNoteEvent scheduled), Is.True);
            Assert.That(scheduled.NoteEvent.Note, Is.EqualTo(resolvedNote));
        }

        [Test]
        public void ResolutionHotPathAllocatesNoManagedMemoryAfterWarmup()
        {
            Core.HarmonyResolver resolver = CreateResolver();
            var pitch = new Core.ScaleDegreePitch(1);
            resolver.Resolve(pitch, 0L, Core.TrackRole.Pad);
            long before = GC.GetAllocatedBytesForCurrentThread();
            int checksum = 0;

            for (int iteration = 0; iteration < 10_000; iteration++)
            {
                checksum += resolver.Resolve(
                    pitch,
                    iteration % 1_920,
                    Core.TrackRole.Pad).MidiNumber;
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(checksum, Is.EqualTo(673_600));
            Assert.That(allocatedBytes, Is.Zero);
        }

        private static Core.HarmonyResolver CreateResolver()
        {
            var scale = new Core.Scale(0, 2, 4, 5, 7, 9, 11);
            var plan = new Core.HarmonyPlan(
                new Core.HarmonyStep(0, 960L),
                new Core.HarmonyStep(4, 960L));
            return new Core.HarmonyResolver(scale, new Core.Note(60), plan);
        }
    }
}
