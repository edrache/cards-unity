using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class FourTrackSequencerTests
    {
        [Test]
        public void ConstructorRejectsDuplicateTrackIds()
        {
            CreateSourceSequencers(
                out var drums,
                out var bass,
                out var lead,
                out var pad);

            Assert.Throws<ArgumentException>(
                () => new Core.FourTrackSequencer(
                    new Core.TrackId(1UL),
                    drums,
                    new Core.TrackId(1UL),
                    bass,
                    new Core.TrackId(2UL),
                    lead,
                    new Core.TrackId(3UL),
                    pad));
        }

        [Test]
        public void SameTickEventsUseTrackIdThenTrackSequenceOrder()
        {
            Core.FourTrackSequencer sequencer = CreateCoordinator();
            var destination =
                new Core.SchedulingBuffer<Core.MultiTrackScheduledEvent>(8);

            Assert.That(sequencer.TryScheduleThrough(1L, destination), Is.True);

            var orderedIds = new List<ulong>();
            while (destination.TryDequeue(out var scheduledEvent))
            {
                orderedIds.Add(scheduledEvent.TrackId.Value);
            }

            Assert.That(orderedIds, Is.EqualTo(new ulong[] { 10UL, 20UL, 30UL, 40UL }));
        }

        [Test]
        public void FourTracksRepeatAtTheirIndependentPatternLengths()
        {
            Core.FourTrackSequencer sequencer = CreateCoordinator();
            var destination =
                new Core.SchedulingBuffer<Core.MultiTrackScheduledEvent>(32);

            Assert.That(
                sequencer.TryScheduleThrough(
                    (7L * Core.MusicalTime.TicksPerBeat) + 1L,
                    destination),
                Is.True);

            var ticksByTrack = new Dictionary<ulong, List<long>>
            {
                [10UL] = new List<long>(),
                [20UL] = new List<long>(),
                [30UL] = new List<long>(),
                [40UL] = new List<long>()
            };
            while (destination.TryDequeue(out var scheduledEvent))
            {
                ticksByTrack[scheduledEvent.TrackId.Value].Add(scheduledEvent.StartTick);
            }

            Assert.That(ticksByTrack[40UL], Is.EqualTo(new[] { 0L, 2880L, 5760L }));
            Assert.That(ticksByTrack[10UL], Is.EqualTo(new[] { 0L, 3840L }));
            Assert.That(ticksByTrack[30UL], Is.EqualTo(new[] { 0L, 4800L }));
            Assert.That(ticksByTrack[20UL], Is.EqualTo(new[] { 0L, 6720L }));
        }

        [Test]
        public void DestinationBackpressureRetainsExactMergeCursor()
        {
            Core.FourTrackSequencer sequencer = CreateCoordinator();
            var destination =
                new Core.SchedulingBuffer<Core.MultiTrackScheduledEvent>(1);
            var orderedIds = new List<ulong>();

            while (!sequencer.TryScheduleThrough(1L, destination))
            {
                Assert.That(destination.TryDequeue(out var scheduledEvent), Is.True);
                orderedIds.Add(scheduledEvent.TrackId.Value);
            }

            while (destination.TryDequeue(out var remainingEvent))
            {
                orderedIds.Add(remainingEvent.TrackId.Value);
            }

            Assert.That(orderedIds, Is.EqualTo(new ulong[] { 10UL, 20UL, 30UL, 40UL }));
            Assert.That(sequencer.ScheduledThroughTick, Is.EqualTo(1L));
            Assert.That(sequencer.HasPendingWindow, Is.False);
        }

        [Test]
        public void PartitionedWindowsMatchOneShotScheduling()
        {
            List<Core.MultiTrackScheduledEvent> oneShot = Schedule(
                CreateCoordinator(),
                new[] { 8000L });
            List<Core.MultiTrackScheduledEvent> partitioned = Schedule(
                CreateCoordinator(),
                new[] { 1000L, 2900L, 5000L, 8000L });

            Assert.That(partitioned, Is.EqualTo(oneShot));
        }

        [Test]
        public void ResetReplaysTheSameFourTrackStream()
        {
            Core.FourTrackSequencer sequencer = CreateCoordinator();
            List<Core.MultiTrackScheduledEvent> first = Schedule(
                sequencer,
                new[] { 8000L });

            sequencer.Reset();
            List<Core.MultiTrackScheduledEvent> replay = Schedule(
                sequencer,
                new[] { 8000L });

            Assert.That(replay, Is.EqualTo(first));
        }

        [Test]
        public void WarmedFourTrackMergeAllocatesNoManagedMemory()
        {
            Core.FourTrackSequencer sequencer = CreateCoordinator();
            var destination =
                new Core.SchedulingBuffer<Core.MultiTrackScheduledEvent>(4);
            sequencer.TryScheduleThrough(1L, destination);
            destination.Clear();
            sequencer.Reset();

            long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            long checksum = 0L;
            bool allSucceeded = true;

            for (int iteration = 0; iteration < 10_000; iteration++)
            {
                allSucceeded &= sequencer.TryScheduleThrough(1L, destination);
                while (destination.TryDequeue(out var scheduledEvent))
                {
                    checksum += (long)scheduledEvent.TrackId.Value;
                }

                sequencer.Reset();
            }

            long afterBytes = GC.GetAllocatedBytesForCurrentThread();

            Assert.That(allSucceeded, Is.True);
            Assert.That(checksum, Is.EqualTo(1_000_000L));
            Assert.That(afterBytes - beforeBytes, Is.Zero);
        }

        private static List<Core.MultiTrackScheduledEvent> Schedule(
            Core.FourTrackSequencer sequencer,
            long[] targets)
        {
            var destination =
                new Core.SchedulingBuffer<Core.MultiTrackScheduledEvent>(64);
            var events = new List<Core.MultiTrackScheduledEvent>();

            foreach (long target in targets)
            {
                Assert.That(sequencer.TryScheduleThrough(target, destination), Is.True);
                while (destination.TryDequeue(out var scheduledEvent))
                {
                    events.Add(scheduledEvent);
                }
            }

            return events;
        }

        private static Core.FourTrackSequencer CreateCoordinator()
        {
            CreateSourceSequencers(
                out var drums,
                out var bass,
                out var lead,
                out var pad);
            return new Core.FourTrackSequencer(
                new Core.TrackId(40UL),
                drums,
                new Core.TrackId(10UL),
                bass,
                new Core.TrackId(30UL),
                lead,
                new Core.TrackId(20UL),
                pad,
                perTrackBufferCapacity: 4);
        }

        private static void CreateSourceSequencers(
            out Core.PercussionStepSequencer drums,
            out Core.PitchedStepSequencer bass,
            out Core.PitchedStepSequencer lead,
            out Core.PitchedStepSequencer pad)
        {
            var resolver = new Core.HarmonyResolver(
                new Core.Scale(0, 2, 4, 5, 7, 9, 11),
                new Core.Note(60),
                new Core.HarmonyPlan(
                    new Core.HarmonyStep(0, 3840L),
                    new Core.HarmonyStep(3, 3840L)));
            drums = new Core.PercussionStepSequencer(
                CreateDrumPattern(3),
                11UL,
                40UL);
            bass = new Core.PitchedStepSequencer(
                CreatePitchedPattern(4),
                resolver,
                Core.TrackRole.Bass,
                11UL,
                10UL);
            lead = new Core.PitchedStepSequencer(
                CreatePitchedPattern(5),
                resolver,
                Core.TrackRole.Lead,
                11UL,
                30UL);
            pad = new Core.PitchedStepSequencer(
                CreatePitchedPattern(7),
                resolver,
                Core.TrackRole.Pad,
                11UL,
                20UL);
        }

        private static Core.PercussionStepPattern CreateDrumPattern(int length)
        {
            var steps = new Core.PercussionPatternStep[length];
            steps[0] = new Core.PercussionPatternStep(
                new Core.PercussionNote(1UL),
                100,
                120L);
            return new Core.PercussionStepPattern(1, steps);
        }

        private static Core.PitchedStepPattern CreatePitchedPattern(int length)
        {
            var steps = new Core.PitchedPatternStep[length];
            steps[0] = new Core.PitchedPatternStep(
                new Core.ScaleDegreePitch(0),
                90,
                240L);
            return new Core.PitchedStepPattern(1, steps);
        }
    }
}
