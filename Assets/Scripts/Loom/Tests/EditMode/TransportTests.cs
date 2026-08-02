using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class TransportTests
    {
        [Test]
        public void ConstructorCreatesAStoppedEmptyTransport()
        {
            var transport = CreateTransport(4);

            Assert.That(transport.State, Is.EqualTo(Core.TransportState.Stopped));
            Assert.That(transport.CurrentTick, Is.Zero);
            Assert.That(transport.ScheduledThroughTick, Is.Zero);
            Assert.That(transport.PendingEventCount, Is.Zero);
            Assert.That(transport.HasPendingWindow, Is.False);
            Assert.Throws<ArgumentNullException>(() => new Core.Transport(null, 1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.Transport(CreateSequencer(), 0));
        }

        [Test]
        public void StartUpdateAndRepeatedTargetEmitEachEventOnce()
        {
            var transport = CreateTransport(4);

            transport.Start();
            Core.TransportUpdateStatus status = transport.Update(0L, 480L);
            List<Core.ScheduledNoteEvent> events = Drain(transport);

            Assert.That(status, Is.EqualTo(Core.TransportUpdateStatus.Completed));
            Assert.That(GetStartTicks(events), Is.EqualTo(new[] { 0L, 240L }));
            Assert.That(transport.Update(0L, 480L), Is.EqualTo(status));
            Assert.That(transport.PendingEventCount, Is.Zero);
            Assert.That(transport.ScheduledThroughTick, Is.EqualTo(480L));
        }

        [Test]
        public void CapacityOneCoalescesANewerTargetBehindThePendingWindow()
        {
            var pattern = new Core.StepPattern(
                4,
                new Core.PatternStep(
                    new Core.Note(60),
                    100,
                    120L,
                    ratchetCount: 3));
            var transport = new Core.Transport(
                new Core.StepSequencer(pattern, 0UL, 0UL),
                1);
            var events = new List<Core.ScheduledNoteEvent>();
            transport.Start();

            Assert.That(
                transport.Update(0L, 480L) & Core.TransportUpdateStatus.BufferFull,
                Is.EqualTo(Core.TransportUpdateStatus.BufferFull));
            Assert.That(
                transport.Update(0L, 720L) & Core.TransportUpdateStatus.BufferFull,
                Is.EqualTo(Core.TransportUpdateStatus.BufferFull));

            Core.TransportUpdateStatus status;
            do
            {
                Assert.That(
                    transport.TryPeekScheduledEvent(out Core.ScheduledNoteEvent firstPeek),
                    Is.True);
                Assert.That(
                    transport.TryPeekScheduledEvent(out Core.ScheduledNoteEvent secondPeek),
                    Is.True);
                Assert.That(secondPeek, Is.EqualTo(firstPeek));
                Assert.That(transport.TryDequeueScheduledEvent(out _), Is.True);
                events.Add(firstPeek);
                status = transport.Update(0L, 720L);
            }
            while ((status & Core.TransportUpdateStatus.BufferFull) != 0);

            events.AddRange(Drain(transport));

            Assert.That(
                GetStartTicks(events),
                Is.EqualTo(new[]
                {
                    0L, 80L, 160L, 240L, 320L, 400L, 480L, 560L, 640L
                }));
            for (int index = 0; index < events.Count; index++)
            {
                Assert.That(events[index].SequenceNumber, Is.EqualTo((ulong)index));
            }

            Assert.That(transport.ScheduledThroughTick, Is.EqualTo(720L));
            Assert.That(transport.HasPendingWindow, Is.False);
        }

        [Test]
        public void PauseClearsLookaheadAndResumeRegeneratesFromTheHeldTick()
        {
            var transport = CreateTransport(4);
            transport.Start();
            transport.Update(0L, 960L);
            Assert.That(transport.PendingEventCount, Is.EqualTo(4));

            transport.Pause(480L);

            Assert.That(transport.State, Is.EqualTo(Core.TransportState.Paused));
            Assert.That(transport.CurrentTick, Is.EqualTo(480L));
            Assert.That(transport.PendingEventCount, Is.Zero);
            Assert.That(transport.ScheduledThroughTick, Is.EqualTo(480L));
            Assert.Throws<InvalidOperationException>(
                () => transport.Update(480L, 960L));

            transport.Resume();
            transport.Update(480L, 960L);
            List<Core.ScheduledNoteEvent> events = Drain(transport);

            Assert.That(GetStartTicks(events), Is.EqualTo(new[] { 480L, 720L }));
            Assert.That(events[0].SequenceNumber, Is.Zero);
        }

        [Test]
        public void PanicClearsPendingWorkWithoutChangingTheLifecycleState()
        {
            var transport = CreateTransport(1);
            transport.Start();
            transport.Update(0L, 720L);

            transport.Panic(240L);

            Assert.That(transport.State, Is.EqualTo(Core.TransportState.Playing));
            Assert.That(transport.CurrentTick, Is.EqualTo(240L));
            Assert.That(transport.PendingEventCount, Is.Zero);
            Assert.That(transport.HasPendingWindow, Is.False);
            Assert.That(transport.ScheduledThroughTick, Is.EqualTo(240L));
            Assert.That(
                transport.Update(240L, 241L),
                Is.EqualTo(Core.TransportUpdateStatus.Completed));
            Assert.That(transport.TryDequeueScheduledEvent(out Core.ScheduledNoteEvent value), Is.True);
            Assert.That(value.StartTick, Is.EqualTo(240L));
        }

        [Test]
        public void StopIsIdempotentAndRestartReplaysTheBeginning()
        {
            var transport = CreateTransport(4);
            transport.Start();
            transport.Update(0L, 480L);
            List<Core.ScheduledNoteEvent> first = Drain(transport);

            transport.Stop();
            transport.Stop();

            Assert.That(transport.State, Is.EqualTo(Core.TransportState.Stopped));
            Assert.That(transport.CurrentTick, Is.Zero);
            Assert.That(transport.ScheduledThroughTick, Is.Zero);
            Assert.That(transport.PendingEventCount, Is.Zero);

            transport.Start();
            transport.Update(0L, 480L);
            Assert.That(Drain(transport), Is.EqualTo(first));
        }

        [Test]
        public void InvalidLifecycleTransitionsAreRejected()
        {
            var transport = CreateTransport(1);

            Assert.Throws<InvalidOperationException>(() => transport.Update(0L, 0L));
            Assert.Throws<InvalidOperationException>(() => transport.Pause(0L));
            Assert.Throws<InvalidOperationException>(() => transport.Resume());

            transport.Start();
            Assert.Throws<InvalidOperationException>(() => transport.Start());
            Assert.Throws<InvalidOperationException>(() => transport.Resume());
            transport.Pause(0L);
            Assert.Throws<InvalidOperationException>(() => transport.Start());
            Assert.Throws<InvalidOperationException>(() => transport.Pause(0L));
        }

        [Test]
        public void InvalidUpdateDoesNotMutateTransportState()
        {
            var transport = CreateTransport(2);
            transport.Start();
            transport.Update(0L, 480L);
            int eventCount = transport.PendingEventCount;
            long watermark = transport.ScheduledThroughTick;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => transport.Update(-1L, 480L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => transport.Update(1L, 0L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => transport.Update(0L, 479L));

            Assert.That(transport.CurrentTick, Is.Zero);
            Assert.That(transport.PendingEventCount, Is.EqualTo(eventCount));
            Assert.That(transport.ScheduledThroughTick, Is.EqualTo(watermark));
        }

        [Test]
        public void UpdateReportsUnderrunSeparatelyFromBackpressure()
        {
            var transport = CreateTransport(1);
            transport.Start();

            Core.TransportUpdateStatus status = transport.Update(1L, 480L);

            Assert.That(
                status & Core.TransportUpdateStatus.Underrun,
                Is.EqualTo(Core.TransportUpdateStatus.Underrun));
            Assert.That(
                status & Core.TransportUpdateStatus.BufferFull,
                Is.EqualTo(Core.TransportUpdateStatus.BufferFull));
        }

        [Test]
        public void TransportLifecycleAndSchedulingDoNotAllocateAfterWarmup()
        {
            var transport = CreateTransport(1);
            transport.Start();
            transport.Update(0L, 1L);
            transport.TryDequeueScheduledEvent(out _);
            transport.Stop();

            long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            long checksum = 0L;
            bool allSucceeded = true;

            for (int index = 0; index < 10_000; index++)
            {
                transport.Start();
                allSucceeded &= transport.Update(0L, 1L)
                    == Core.TransportUpdateStatus.Completed;
                allSucceeded &= transport.TryDequeueScheduledEvent(
                    out Core.ScheduledNoteEvent scheduledEvent);
                checksum += scheduledEvent.NoteEvent.Note.MidiNumber;
                transport.Stop();
            }

            long afterBytes = GC.GetAllocatedBytesForCurrentThread();

            Assert.That(allSucceeded, Is.True);
            Assert.That(checksum, Is.EqualTo(600_000L));
            Assert.That(afterBytes - beforeBytes, Is.Zero);
        }

        private static Core.Transport CreateTransport(int capacity)
        {
            return new Core.Transport(CreateSequencer(), capacity);
        }

        private static Core.StepSequencer CreateSequencer()
        {
            var pattern = new Core.StepPattern(
                4,
                new Core.PatternStep(new Core.Note(60), 100, 120L));
            return new Core.StepSequencer(pattern, 0UL, 0UL);
        }

        private static List<Core.ScheduledNoteEvent> Drain(Core.Transport transport)
        {
            var events = new List<Core.ScheduledNoteEvent>(
                transport.PendingEventCount);
            while (transport.TryDequeueScheduledEvent(
                out Core.ScheduledNoteEvent scheduledEvent))
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
