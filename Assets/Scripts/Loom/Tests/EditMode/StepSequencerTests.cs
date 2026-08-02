using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class StepSequencerTests
    {
        [Test]
        public void ConstructorRejectsNullPattern()
        {
            Assert.Throws<ArgumentNullException>(
                () => new Core.StepSequencer(null, 0UL, 0UL));
        }

        [Test]
        public void SequencerEmitsPlayableStepsAndSkipsRests()
        {
            var pattern = new Core.StepPattern(
                4,
                new Core.PatternStep(new Core.Note(60), 100, 120L),
                Core.PatternStep.Rest,
                new Core.PatternStep(new Core.Note(64), 90, 180L),
                Core.PatternStep.Rest);
            var sequencer = new Core.StepSequencer(pattern, 0UL, 0UL);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(4);

            Assert.That(sequencer.TryScheduleThrough(960L, buffer), Is.True);
            List<Core.ScheduledNoteEvent> events = Drain(buffer);

            Assert.That(events.Count, Is.EqualTo(2));
            AssertEvent(events[0], 60, 100, 0L, 120L, 0UL);
            AssertEvent(events[1], 64, 90, 480L, 180L, 1UL);
            Assert.That(sequencer.ScheduledThroughTick, Is.EqualTo(960L));
        }

        [Test]
        public void WindowIncludesItsStartAndExcludesItsEnd()
        {
            var sequencer = CreateQuarterStepSequencer(probability: 1f);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(4);

            Assert.That(sequencer.TryScheduleThrough(240L, buffer), Is.True);
            List<Core.ScheduledNoteEvent> firstWindow = Drain(buffer);
            Assert.That(firstWindow.Count, Is.EqualTo(1));
            Assert.That(firstWindow[0].StartTick, Is.Zero);

            Assert.That(sequencer.TryScheduleThrough(241L, buffer), Is.True);
            List<Core.ScheduledNoteEvent> secondWindow = Drain(buffer);
            Assert.That(secondWindow.Count, Is.EqualTo(1));
            Assert.That(secondWindow[0].StartTick, Is.EqualTo(240L));
        }

        [Test]
        public void RepeatingACompletedWindowDoesNotDuplicateEvents()
        {
            var sequencer = CreateQuarterStepSequencer(probability: 1f);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(4);

            Assert.That(sequencer.TryScheduleThrough(480L, buffer), Is.True);
            Assert.That(Drain(buffer).Count, Is.EqualTo(2));
            Assert.That(sequencer.TryScheduleThrough(480L, buffer), Is.True);
            Assert.That(buffer.IsEmpty, Is.True);
        }

        [Test]
        public void AllRestPatternAdvancesWatermarkWithoutEvents()
        {
            var pattern = new Core.StepPattern(
                4,
                Core.PatternStep.Rest,
                Core.PatternStep.Rest);
            var sequencer = new Core.StepSequencer(pattern, 0UL, 0UL);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(1);

            Assert.That(sequencer.TryScheduleThrough(10_000L, buffer), Is.True);
            Assert.That(sequencer.ScheduledThroughTick, Is.EqualTo(10_000L));
            Assert.That(buffer.IsEmpty, Is.True);
        }

        [Test]
        public void ZeroProbabilityNeverEmitsAndOneAlwaysEmits()
        {
            var zeroSequencer = CreateQuarterStepSequencer(probability: 0f);
            var oneSequencer = CreateQuarterStepSequencer(probability: 1f);
            var zeroBuffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(8);
            var oneBuffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(8);

            Assert.That(zeroSequencer.TryScheduleThrough(960L, zeroBuffer), Is.True);
            Assert.That(oneSequencer.TryScheduleThrough(960L, oneBuffer), Is.True);

            Assert.That(zeroBuffer.IsEmpty, Is.True);
            Assert.That(oneBuffer.Count, Is.EqualTo(4));
        }

        [Test]
        public void FixedSeedProducesGoldenProbabilitySelection()
        {
            var sequencer = CreateQuarterStepSequencer(probability: 0.5f);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(8);

            Assert.That(sequencer.TryScheduleThrough(1_920L, buffer), Is.True);
            List<Core.ScheduledNoteEvent> events = Drain(buffer);

            Assert.That(
                GetStartTicks(events),
                Is.EqualTo(new[] { 0L, 240L, 1_200L, 1_440L }));
        }

        [Test]
        public void UnevenRatchetsUseFlooredOffsetsAndPreserveDuration()
        {
            var pattern = new Core.StepPattern(
                4,
                new Core.PatternStep(
                    new Core.Note(60),
                    100,
                    120L,
                    ratchetCount: 7));
            var sequencer = new Core.StepSequencer(pattern, 0UL, 0UL);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(8);

            Assert.That(sequencer.TryScheduleThrough(240L, buffer), Is.True);
            List<Core.ScheduledNoteEvent> events = Drain(buffer);

            Assert.That(
                GetStartTicks(events),
                Is.EqualTo(new[] { 0L, 34L, 68L, 102L, 137L, 171L, 205L }));
            for (int index = 0; index < events.Count; index++)
            {
                Assert.That(events[index].NoteEvent.DurationTicks, Is.EqualTo(120L));
            }
        }

        [Test]
        public void NegativeMicrotimingSkipsPreSessionOnsetAndUsesSourceTick()
        {
            var pattern = new Core.StepPattern(
                4,
                new Core.PatternStep(
                    new Core.Note(60),
                    100,
                    120L,
                    probability: 0.4f,
                    microtimingTickOffset: -10));
            var sequencer = new Core.StepSequencer(pattern, 0UL, 0UL);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(4);

            Assert.That(sequencer.TryScheduleThrough(230L, buffer), Is.True);
            Assert.That(buffer.IsEmpty, Is.True);
            Assert.That(sequencer.TryScheduleThrough(240L, buffer), Is.True);
            List<Core.ScheduledNoteEvent> events = Drain(buffer);

            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].StartTick, Is.EqualTo(230L));
        }

        [Test]
        public void PositiveRatchetSpillAppearsInTheFollowingOutputCycle()
        {
            var pattern = new Core.StepPattern(
                4,
                new Core.PatternStep(
                    new Core.Note(60),
                    100,
                    120L,
                    ratchetCount: 2,
                    microtimingTickOffset: 239));
            var sequencer = new Core.StepSequencer(pattern, 0UL, 0UL);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(4);

            Assert.That(sequencer.TryScheduleThrough(359L, buffer), Is.True);
            List<Core.ScheduledNoteEvent> beforeBoundary = Drain(buffer);
            Assert.That(GetStartTicks(beforeBoundary), Is.EqualTo(new[] { 239L }));

            Assert.That(sequencer.TryScheduleThrough(360L, buffer), Is.True);
            List<Core.ScheduledNoteEvent> atBoundary = Drain(buffer);
            Assert.That(GetStartTicks(atBoundary), Is.EqualTo(new[] { 359L }));
        }

        [Test]
        public void SameOnsetEventsUseCanonicalSourceOrder()
        {
            var pattern = new Core.StepPattern(
                4,
                new Core.PatternStep(
                    new Core.Note(60),
                    100,
                    120L,
                    microtimingTickOffset: 120),
                new Core.PatternStep(
                    new Core.Note(64),
                    100,
                    120L,
                    microtimingTickOffset: -120));
            var sequencer = new Core.StepSequencer(pattern, 0UL, 0UL);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(4);

            Assert.That(sequencer.TryScheduleThrough(121L, buffer), Is.True);
            List<Core.ScheduledNoteEvent> events = Drain(buffer);

            Assert.That(events.Count, Is.EqualTo(2));
            AssertEvent(events[0], 60, 100, 120L, 120L, 0UL);
            AssertEvent(events[1], 64, 100, 120L, 120L, 1UL);
        }

        [Test]
        public void AdjacentIrregularWindowsMatchOneLargeWindow()
        {
            long[] windowEnds = { 113L, 240L, 511L, 959L, 1_201L, 1_603L, 1_920L };
            var oneShotSequencer = CreateQuarterStepSequencer(probability: 0.5f);
            var partitionedSequencer = CreateQuarterStepSequencer(probability: 0.5f);
            var oneShotBuffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(8);
            var partitionedBuffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(8);

            Assert.That(
                oneShotSequencer.TryScheduleThrough(1_920L, oneShotBuffer),
                Is.True);
            List<Core.ScheduledNoteEvent> oneShotEvents = Drain(oneShotBuffer);
            var partitionedEvents = new List<Core.ScheduledNoteEvent>();

            for (int index = 0; index < windowEnds.Length; index++)
            {
                Assert.That(
                    partitionedSequencer.TryScheduleThrough(
                        windowEnds[index],
                        partitionedBuffer),
                    Is.True);
                partitionedEvents.AddRange(Drain(partitionedBuffer));
            }

            Assert.That(partitionedEvents, Is.EqualTo(oneShotEvents));
        }

        [Test]
        public void CapacityOneBackpressureResumesWithoutLossOrDuplication()
        {
            var pattern = new Core.StepPattern(
                4,
                new Core.PatternStep(
                    new Core.Note(60),
                    100,
                    120L,
                    ratchetCount: 3));
            var sequencer = new Core.StepSequencer(pattern, 0UL, 0UL);
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
        public void PartialWindowRequiresTheSameTargetUntilCompleted()
        {
            var sequencer = CreateQuarterStepSequencer(probability: 1f);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(1);

            Assert.That(sequencer.TryScheduleThrough(720L, buffer), Is.False);
            Assert.That(sequencer.HasPendingWindow, Is.True);
            Assert.Throws<InvalidOperationException>(
                () => sequencer.TryScheduleThrough(960L, buffer));
            Assert.That(sequencer.ScheduledThroughTick, Is.Zero);
        }

        [Test]
        public void CompletedWatermarkCannotMoveBackward()
        {
            var sequencer = CreateQuarterStepSequencer(probability: 0f);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(1);
            sequencer.TryScheduleThrough(960L, buffer);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => sequencer.TryScheduleThrough(959L, buffer));
        }

        [Test]
        public void SchedulingRejectsInvalidArguments()
        {
            var sequencer = CreateQuarterStepSequencer(probability: 1f);

            Assert.Throws<ArgumentNullException>(
                () => sequencer.TryScheduleThrough(1L, null));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => sequencer.TryScheduleThrough(
                    -1L,
                    new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(1)));
        }

        [Test]
        public void ResetReplaysTheSameDeterministicStream()
        {
            var sequencer = CreateQuarterStepSequencer(probability: 0.5f);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(8);

            sequencer.TryScheduleThrough(1_920L, buffer);
            List<Core.ScheduledNoteEvent> first = Drain(buffer);
            sequencer.Reset();
            sequencer.TryScheduleThrough(1_920L, buffer);
            List<Core.ScheduledNoteEvent> replay = Drain(buffer);

            Assert.That(replay, Is.EqualTo(first));
        }

        [Test]
        public void ResetAtAnExactFinalOnsetIncludesThatEvent()
        {
            var sequencer = CreateQuarterStepSequencer(probability: 1f);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(2);

            sequencer.Reset(240L);
            Assert.That(sequencer.TryScheduleThrough(241L, buffer), Is.True);

            Assert.That(buffer.TryDequeue(out Core.ScheduledNoteEvent scheduledEvent), Is.True);
            AssertEvent(scheduledEvent, 60, 100, 240L, 120L, 0UL);
        }

        [Test]
        public void ResetAfterAFinalOnsetSkipsThePastEvent()
        {
            var sequencer = CreateQuarterStepSequencer(probability: 1f);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(2);

            sequencer.Reset(241L);
            Assert.That(sequencer.TryScheduleThrough(481L, buffer), Is.True);

            Assert.That(buffer.TryDequeue(out Core.ScheduledNoteEvent scheduledEvent), Is.True);
            Assert.That(scheduledEvent.StartTick, Is.EqualTo(480L));
            Assert.That(scheduledEvent.SequenceNumber, Is.Zero);
        }

        [Test]
        public void ResetUsesMicrotimedFinalOnsetsAcrossSourceCycles()
        {
            var negativePattern = new Core.StepPattern(
                4,
                new Core.PatternStep(
                    new Core.Note(60),
                    100,
                    120L,
                    microtimingTickOffset: -10));
            var negativeSequencer = new Core.StepSequencer(negativePattern, 0UL, 0UL);
            var negativeBuffer =
                new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(2);
            negativeSequencer.Reset(230L);

            Assert.That(negativeSequencer.TryScheduleThrough(231L, negativeBuffer), Is.True);
            Assert.That(
                negativeBuffer.TryDequeue(out Core.ScheduledNoteEvent negativeEvent),
                Is.True);
            Assert.That(negativeEvent.StartTick, Is.EqualTo(230L));

            var positivePattern = new Core.StepPattern(
                4,
                new Core.PatternStep(
                    new Core.Note(60),
                    100,
                    120L,
                    ratchetCount: 2,
                    microtimingTickOffset: 239));
            var positiveSequencer = new Core.StepSequencer(positivePattern, 0UL, 0UL);
            var positiveBuffer =
                new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(2);
            positiveSequencer.Reset(240L);

            Assert.That(positiveSequencer.TryScheduleThrough(360L, positiveBuffer), Is.True);
            Assert.That(
                positiveBuffer.TryDequeue(out Core.ScheduledNoteEvent positiveEvent),
                Is.True);
            Assert.That(positiveEvent.StartTick, Is.EqualTo(359L));
        }

        [Test]
        public void ResetClearsPartialWindowAndRejectsNegativeTicksWithoutMutation()
        {
            var sequencer = CreateQuarterStepSequencer(probability: 1f);
            var buffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(1);
            Assert.That(sequencer.TryScheduleThrough(720L, buffer), Is.False);

            Assert.Throws<ArgumentOutOfRangeException>(() => sequencer.Reset(-1L));
            Assert.That(sequencer.HasPendingWindow, Is.True);
            Assert.That(sequencer.ScheduledThroughTick, Is.Zero);

            buffer.Clear();
            sequencer.Reset(480L);

            Assert.That(sequencer.HasPendingWindow, Is.False);
            Assert.That(sequencer.ScheduledThroughTick, Is.EqualTo(480L));
            Assert.That(sequencer.TryScheduleThrough(481L, buffer), Is.True);
            Assert.That(buffer.TryDequeue(out Core.ScheduledNoteEvent scheduledEvent), Is.True);
            Assert.That(scheduledEvent.StartTick, Is.EqualTo(480L));
            Assert.That(scheduledEvent.SequenceNumber, Is.Zero);
        }

        [Test]
        public void EventEndOverflowFailsWithoutCompletingTheWindow()
        {
            var pattern = new Core.StepPattern(
                4,
                new Core.PatternStep(new Core.Note(60), 100, long.MaxValue));
            var sequencer = new Core.StepSequencer(pattern, 0UL, 0UL);
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
            var pattern = new Core.StepPattern(
                4,
                new Core.PatternStep(new Core.Note(60), 100, 120L));
            var sequencer = new Core.StepSequencer(pattern, 0UL, 0UL);
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

            long afterBytes = GC.GetAllocatedBytesForCurrentThread();

            Assert.That(allOperationsSucceeded, Is.True);
            Assert.That(checksum, Is.EqualTo(600_000L));
            Assert.That(afterBytes - beforeBytes, Is.Zero);
        }

        private static Core.StepSequencer CreateQuarterStepSequencer(float probability)
        {
            var pattern = new Core.StepPattern(
                4,
                new Core.PatternStep(
                    new Core.Note(60),
                    100,
                    120L,
                    probability));
            return new Core.StepSequencer(pattern, 0UL, 0UL);
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

        private static void AssertEvent(
            Core.ScheduledNoteEvent scheduledEvent,
            int midiNumber,
            int velocity,
            long startTick,
            long durationTicks,
            ulong sequenceNumber)
        {
            Assert.That(scheduledEvent.NoteEvent.Note.MidiNumber, Is.EqualTo(midiNumber));
            Assert.That(scheduledEvent.NoteEvent.Velocity, Is.EqualTo(velocity));
            Assert.That(scheduledEvent.StartTick, Is.EqualTo(startTick));
            Assert.That(scheduledEvent.NoteEvent.DurationTicks, Is.EqualTo(durationTicks));
            Assert.That(scheduledEvent.SequenceNumber, Is.EqualTo(sequenceNumber));
        }
    }
}
