using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class PitchedStepSequencerTests
    {
        [Test]
        public void ConstructorRejectsMissingDependenciesOrInvalidRole()
        {
            Core.HarmonyResolver resolver = CreateResolver();
            var pattern = new Core.PitchedStepPattern(
                4,
                new Core.PitchedPatternStep(default, 100, 120L));

            Assert.Throws<ArgumentNullException>(
                () => new Core.PitchedStepSequencer(
                    null,
                    resolver,
                    Core.TrackRole.Lead,
                    0UL,
                    0UL));
            Assert.Throws<ArgumentNullException>(
                () => new Core.PitchedStepSequencer(
                    pattern,
                    null,
                    Core.TrackRole.Lead,
                    0UL,
                    0UL));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.PitchedStepSequencer(
                    pattern,
                    resolver,
                    default,
                    0UL,
                    0UL));
        }

        [TestCase(Core.TrackRole.Bass, 99, 60)]
        [TestCase(Core.TrackRole.Lead, 3, 65)]
        [TestCase(Core.TrackRole.Pad, 1, 64)]
        public void SequencerUsesTheSelectedHarmonyPolicy(
            Core.TrackRole role,
            int degree,
            int expectedMidi)
        {
            var pattern = new Core.PitchedStepPattern(
                4,
                new Core.PitchedPatternStep(
                    new Core.ScaleDegreePitch(degree),
                    100,
                    120L));
            var sequencer = new Core.PitchedStepSequencer(
                pattern,
                CreateResolver(),
                role,
                0UL,
                0UL);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(1);

            Assert.That(sequencer.TryScheduleThrough(1L, buffer), Is.True);
            Assert.That(buffer.TryDequeue(out Core.ScheduledNoteEvent scheduled), Is.True);
            Assert.That(scheduled.NoteEvent.Note.MidiNumber, Is.EqualTo(expectedMidi));
        }

        [Test]
        public void HarmonyResolutionUsesEachRatchetsFinalOnsetTick()
        {
            var pattern = new Core.PitchedStepPattern(
                4,
                new Core.PitchedPatternStep(
                    new Core.ScaleDegreePitch(99),
                    100,
                    120L,
                    ratchetCount: 2));
            var sequencer = new Core.PitchedStepSequencer(
                pattern,
                CreateBoundaryResolver(),
                Core.TrackRole.Bass,
                0UL,
                0UL);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(2);

            Assert.That(sequencer.TryScheduleThrough(121L, buffer), Is.True);
            List<Core.ScheduledNoteEvent> events = Drain(buffer);

            Assert.That(GetStartTicks(events), Is.EqualTo(new[] { 0L, 120L }));
            Assert.That(events[0].NoteEvent.Note.MidiNumber, Is.EqualTo(60));
            Assert.That(events[1].NoteEvent.Note.MidiNumber, Is.EqualTo(67));
        }

        [Test]
        public void SequencerSkipsRestsAndPreservesResolvedEventFields()
        {
            var pattern = new Core.PitchedStepPattern(
                4,
                new Core.PitchedPatternStep(
                    new Core.ScaleDegreePitch(2),
                    87,
                    180L),
                Core.PitchedPatternStep.Rest);
            var sequencer = new Core.PitchedStepSequencer(
                pattern,
                CreateResolver(),
                Core.TrackRole.Lead,
                0UL,
                0UL);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(2);

            Assert.That(sequencer.TryScheduleThrough(480L, buffer), Is.True);
            Assert.That(buffer.TryDequeue(out Core.ScheduledNoteEvent scheduled), Is.True);
            Assert.That(buffer.IsEmpty, Is.True);
            Assert.That(scheduled.NoteEvent.Note.MidiNumber, Is.EqualTo(64));
            Assert.That(scheduled.NoteEvent.Velocity, Is.EqualTo(87));
            Assert.That(scheduled.StartTick, Is.Zero);
            Assert.That(scheduled.NoteEvent.DurationTicks, Is.EqualTo(180L));
            Assert.That(scheduled.SequenceNumber, Is.Zero);
        }

        [Test]
        public void FixedSeedMatchesExistingProbabilitySelection()
        {
            var sequencer = CreateQuarterStepSequencer(0.5f);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(8);

            Assert.That(sequencer.TryScheduleThrough(1_920L, buffer), Is.True);

            Assert.That(
                GetStartTicks(Drain(buffer)),
                Is.EqualTo(new[] { 0L, 240L, 1_200L, 1_440L }));
        }

        [Test]
        public void AdjacentWindowsMatchOneLargeWindow()
        {
            long[] windowEnds = { 113L, 240L, 511L, 959L, 1_201L, 1_603L, 1_920L };
            Core.PitchedStepSequencer oneShot = CreateQuarterStepSequencer(0.5f);
            Core.PitchedStepSequencer partitioned = CreateQuarterStepSequencer(0.5f);
            var oneShotBuffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(8);
            var partitionedBuffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(8);
            var partitionedEvents = new List<Core.ScheduledNoteEvent>();

            Assert.That(oneShot.TryScheduleThrough(1_920L, oneShotBuffer), Is.True);
            List<Core.ScheduledNoteEvent> oneShotEvents = Drain(oneShotBuffer);
            for (int index = 0; index < windowEnds.Length; index++)
            {
                Assert.That(
                    partitioned.TryScheduleThrough(windowEnds[index], partitionedBuffer),
                    Is.True);
                partitionedEvents.AddRange(Drain(partitionedBuffer));
            }

            Assert.That(partitionedEvents, Is.EqualTo(oneShotEvents));
        }

        [Test]
        public void CapacityOneBackpressureResumesWithoutLossOrDuplication()
        {
            var pattern = new Core.PitchedStepPattern(
                4,
                new Core.PitchedPatternStep(
                    default,
                    100,
                    120L,
                    ratchetCount: 3));
            var sequencer = new Core.PitchedStepSequencer(
                pattern,
                CreateResolver(),
                Core.TrackRole.Lead,
                0UL,
                0UL);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(1);
            var events = new List<Core.ScheduledNoteEvent>();

            while (!sequencer.TryScheduleThrough(480L, buffer))
            {
                events.AddRange(Drain(buffer));
            }

            events.AddRange(Drain(buffer));

            Assert.That(
                GetStartTicks(events),
                Is.EqualTo(new[] { 0L, 80L, 160L, 240L, 320L, 400L }));
            for (int index = 0; index < events.Count; index++)
            {
                Assert.That(events[index].SequenceNumber, Is.EqualTo((ulong)index));
            }
        }

        [Test]
        public void ResetReplaysTheSameResolvedStream()
        {
            Core.PitchedStepSequencer sequencer = CreateQuarterStepSequencer(0.5f);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(8);

            sequencer.TryScheduleThrough(1_920L, buffer);
            List<Core.ScheduledNoteEvent> first = Drain(buffer);
            sequencer.Reset();
            sequencer.TryScheduleThrough(1_920L, buffer);
            List<Core.ScheduledNoteEvent> replay = Drain(buffer);

            Assert.That(replay, Is.EqualTo(first));
        }

        [Test]
        public void EventEndOverflowRetainsThePartialWindow()
        {
            var pattern = new Core.PitchedStepPattern(
                4,
                new Core.PitchedPatternStep(default, 100, long.MaxValue));
            var sequencer = new Core.PitchedStepSequencer(
                pattern,
                CreateResolver(),
                Core.TrackRole.Lead,
                0UL,
                0UL);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(2);

            Assert.Throws<OverflowException>(
                () => sequencer.TryScheduleThrough(241L, buffer));
            Assert.That(sequencer.HasPendingWindow, Is.True);
            Assert.That(sequencer.ScheduledThroughTick, Is.Zero);
            Assert.That(buffer.Count, Is.EqualTo(1));
        }

        [Test]
        public void SchedulingHotPathAllocatesNoManagedMemoryAfterInitialization()
        {
            Core.PitchedStepSequencer sequencer = CreateQuarterStepSequencer(1f);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(1);
            sequencer.TryScheduleThrough(1L, buffer);
            buffer.TryDequeue(out _);
            sequencer.Reset();

            long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            long checksum = 0L;
            bool allOperationsSucceeded = true;

            for (int iteration = 0; iteration < 10_000; iteration++)
            {
                allOperationsSucceeded &= sequencer.TryScheduleThrough(1L, buffer);
                allOperationsSucceeded &= buffer.TryDequeue(
                    out Core.ScheduledNoteEvent scheduledEvent);
                checksum += scheduledEvent.NoteEvent.Note.MidiNumber;
                sequencer.Reset();
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;

            Assert.That(allOperationsSucceeded, Is.True);
            Assert.That(checksum, Is.EqualTo(600_000L));
            Assert.That(allocatedBytes, Is.Zero);
        }

        private static Core.PitchedStepSequencer CreateQuarterStepSequencer(float probability)
        {
            var pattern = new Core.PitchedStepPattern(
                4,
                new Core.PitchedPatternStep(
                    default,
                    100,
                    120L,
                    probability));
            return new Core.PitchedStepSequencer(
                pattern,
                CreateResolver(),
                Core.TrackRole.Lead,
                0UL,
                0UL);
        }

        private static Core.HarmonyResolver CreateResolver()
        {
            var scale = new Core.Scale(0, 2, 4, 5, 7, 9, 11);
            var plan = new Core.HarmonyPlan(
                new Core.HarmonyStep(0, 960L));
            return new Core.HarmonyResolver(scale, new Core.Note(60), plan);
        }

        private static Core.HarmonyResolver CreateBoundaryResolver()
        {
            var scale = new Core.Scale(0, 2, 4, 5, 7, 9, 11);
            var plan = new Core.HarmonyPlan(
                new Core.HarmonyStep(0, 100L),
                new Core.HarmonyStep(4, 100L));
            return new Core.HarmonyResolver(scale, new Core.Note(60), plan);
        }

        private static List<Core.ScheduledNoteEvent> Drain(
            Core.SchedulingBuffer<Core.ScheduledNoteEvent> buffer)
        {
            var events = new List<Core.ScheduledNoteEvent>(buffer.Count);
            while (buffer.TryDequeue(out Core.ScheduledNoteEvent scheduledEvent))
            {
                events.Add(scheduledEvent);
            }

            return events;
        }

        private static long[] GetStartTicks(List<Core.ScheduledNoteEvent> events)
        {
            var ticks = new long[events.Count];
            for (int index = 0; index < events.Count; index++)
            {
                ticks[index] = events[index].StartTick;
            }

            return ticks;
        }
    }
}
