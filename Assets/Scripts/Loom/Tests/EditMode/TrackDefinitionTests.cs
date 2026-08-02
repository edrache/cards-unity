using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class TrackDefinitionTests
    {
        [Test]
        public void ConstructorRejectsInvalidIdentityOrMissingPattern()
        {
            var trackId = new Core.TrackId(1UL);
            var patternId = new Core.PatternId(1UL);
            Core.StepPattern pattern = CreatePattern(1);

            Assert.Throws<ArgumentException>(
                () => new Core.TrackDefinition(default, patternId, pattern));
            Assert.Throws<ArgumentException>(
                () => new Core.TrackDefinition(trackId, default, pattern));
            Assert.Throws<ArgumentNullException>(
                () => new Core.TrackDefinition(trackId, patternId, null));
        }

        [Test]
        public void DefinitionPreservesIdentitiesAndOwnedPatternLength()
        {
            var source = new[]
            {
                new Core.PatternStep(new Core.Note(60), 100, 120L),
                Core.PatternStep.Rest
            };
            var pattern = new Core.StepPattern(4, source);
            var definition = new Core.TrackDefinition(
                new Core.TrackId(7UL),
                new Core.PatternId(11UL),
                pattern);

            source[0] = Core.PatternStep.Rest;

            Assert.That(definition.Id, Is.EqualTo(new Core.TrackId(7UL)));
            Assert.That(definition.PatternId, Is.EqualTo(new Core.PatternId(11UL)));
            Assert.That(definition.Pattern, Is.SameAs(pattern));
            Assert.That(definition.PatternLengthTicks, Is.EqualTo(480L));
            Assert.That(definition.Pattern[0].IsRest, Is.False);
        }

        [Test]
        public void TrackSequencersRepeatAtTheirIndependentPatternLengths()
        {
            Core.TrackDefinition sixteenStepTrack = CreateTrack(1UL, 16);
            Core.TrackDefinition twelveStepTrack = CreateTrack(2UL, 12);
            Core.StepSequencer sixteenStepSequencer = sixteenStepTrack.CreateSequencer(0UL);
            Core.StepSequencer twelveStepSequencer = twelveStepTrack.CreateSequencer(0UL);
            var sixteenStepBuffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(4);
            var twelveStepBuffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(5);

            sixteenStepSequencer.TryScheduleThrough(11_521L, sixteenStepBuffer);
            twelveStepSequencer.TryScheduleThrough(11_521L, twelveStepBuffer);

            Assert.That(sixteenStepTrack.PatternLengthTicks, Is.EqualTo(3_840L));
            Assert.That(twelveStepTrack.PatternLengthTicks, Is.EqualTo(2_880L));
            Assert.That(
                DrainStartTicks(sixteenStepBuffer),
                Is.EqualTo(new[] { 0L, 3_840L, 7_680L, 11_520L }));
            Assert.That(
                DrainStartTicks(twelveStepBuffer),
                Is.EqualTo(new[] { 0L, 2_880L, 5_760L, 8_640L, 11_520L }));
        }

        [Test]
        public void TrackIdentitySelectsTheSequencerRandomStream()
        {
            var probabilityPattern = new Core.StepPattern(
                4,
                new Core.PatternStep(
                    new Core.Note(60),
                    100,
                    120L,
                    probability: 0.5f));
            var firstTrack = new Core.TrackDefinition(
                new Core.TrackId(1UL),
                new Core.PatternId(1UL),
                probabilityPattern);
            var secondTrack = new Core.TrackDefinition(
                new Core.TrackId(2UL),
                new Core.PatternId(1UL),
                probabilityPattern);
            Core.StepSequencer firstSequencer = firstTrack.CreateSequencer(123UL);
            Core.StepSequencer secondSequencer = secondTrack.CreateSequencer(123UL);
            var firstBuffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(1);
            var secondBuffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(1);

            firstSequencer.TryScheduleThrough(1L, firstBuffer);
            secondSequencer.TryScheduleThrough(1L, secondBuffer);

            Assert.That(firstBuffer.IsEmpty, Is.True);
            Assert.That(secondBuffer.Count, Is.EqualTo(1));
        }

        [Test]
        public void RecreatingTrackSequencerReplaysTheSameResolvedNoteStream()
        {
            Core.TrackDefinition track = CreateTrack(5UL, 3);
            Core.StepSequencer first = track.CreateSequencer(91UL);
            Core.StepSequencer replay = track.CreateSequencer(91UL);
            var firstBuffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(4);
            var replayBuffer = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(4);

            first.TryScheduleThrough(2_161L, firstBuffer);
            replay.TryScheduleThrough(2_161L, replayBuffer);

            Assert.That(Drain(firstBuffer), Is.EqualTo(Drain(replayBuffer)));
        }

        private static Core.TrackDefinition CreateTrack(ulong trackId, int stepCount)
        {
            return new Core.TrackDefinition(
                new Core.TrackId(trackId),
                new Core.PatternId(trackId),
                CreatePattern(stepCount));
        }

        private static Core.StepPattern CreatePattern(int stepCount)
        {
            var steps = new Core.PatternStep[stepCount];
            steps[0] = new Core.PatternStep(new Core.Note(60), 100, 120L);
            for (int index = 1; index < steps.Length; index++)
            {
                steps[index] = Core.PatternStep.Rest;
            }

            return new Core.StepPattern(4, steps);
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

        private static long[] DrainStartTicks(
            Core.SchedulingBuffer<Core.ScheduledNoteEvent> buffer)
        {
            List<Core.ScheduledNoteEvent> events = Drain(buffer);
            var startTicks = new long[events.Count];
            for (int index = 0; index < events.Count; index++)
            {
                startTicks[index] = events[index].StartTick;
            }

            return startTicks;
        }
    }
}
