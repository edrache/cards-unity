using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class FmodScheduledNoteDispatcherTests
    {
        [Test]
        public void ConstructorRejectsInvalidInjectedDependencies()
        {
            var mapper = new FakeClockMapper();
            var instrument = new FakeScheduledInstrument();

            Assert.Throws<ArgumentNullException>(
                () => new Fmod.FmodScheduledNoteDispatcher(null, instrument, 1UL));
            Assert.Throws<ArgumentNullException>(
                () => new Fmod.FmodScheduledNoteDispatcher(mapper, null, 1UL));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodScheduledNoteDispatcher(mapper, instrument, 0UL));
        }

        [Test]
        public void EmptyBufferDoesNotReadTheNativeClock()
        {
            var mapper = new FakeClockMapper();
            var dispatcher = new Fmod.FmodScheduledNoteDispatcher(
                mapper,
                new FakeScheduledInstrument(),
                100UL);
            var source = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(1);

            int count = dispatcher.DispatchAvailable(
                source,
                1,
                out int lateCount,
                out int plateauCount);

            Assert.That(count, Is.Zero);
            Assert.That(lateCount, Is.Zero);
            Assert.That(plateauCount, Is.Zero);
            Assert.That(mapper.GetCurrentClockCallCount, Is.Zero);
        }

        [Test]
        public void DispatcherPreservesExactClocksAndFifoOrder()
        {
            var mapper = new FakeClockMapper
            {
                CurrentDspClock = 1_000UL,
                BaseDspClock = 10_000UL,
                SampleFramesPerTick = 2UL
            };
            var instrument = new FakeScheduledInstrument();
            var dispatcher = new Fmod.FmodScheduledNoteDispatcher(
                mapper,
                instrument,
                100UL);
            var source = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(2);
            Core.ScheduledNoteEvent first = CreateEvent(100, 10, 0UL);
            Core.ScheduledNoteEvent second = CreateEvent(100, 20, 1UL);
            Assert.That(source.TryEnqueue(first), Is.True);
            Assert.That(source.TryEnqueue(second), Is.True);

            int count = dispatcher.DispatchAvailable(
                source,
                2,
                out int lateCount,
                out int plateauCount);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(source.IsEmpty, Is.True);
            Assert.That(lateCount, Is.Zero);
            Assert.That(plateauCount, Is.Zero);
            Assert.That(mapper.GetCurrentClockCallCount, Is.EqualTo(1));
            Assert.That(instrument.Events[0], Is.EqualTo(first.NoteEvent));
            Assert.That(instrument.Events[1], Is.EqualTo(second.NoteEvent));
            Assert.That(instrument.StartClocks[0], Is.EqualTo(10_200UL));
            Assert.That(instrument.EndClocks[0], Is.EqualTo(10_220UL));
            Assert.That(instrument.StartClocks[1], Is.EqualTo(10_200UL));
            Assert.That(instrument.EndClocks[1], Is.EqualTo(10_240UL));
        }

        [Test]
        public void DispatcherShiftsLatePlateauEventsAndPreservesOneFrameGate()
        {
            var mapper = new FakeClockMapper
            {
                CurrentDspClock = 10_000UL,
                BaseDspClock = 500UL,
                SampleFramesPerTick = 0UL
            };
            var instrument = new FakeScheduledInstrument();
            var dispatcher = new Fmod.FmodScheduledNoteDispatcher(
                mapper,
                instrument,
                256UL);
            var source = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(1);
            Assert.That(source.TryEnqueue(CreateEvent(0, 1, 0UL)), Is.True);

            int count = dispatcher.DispatchAvailable(
                source,
                1,
                out int lateCount,
                out int plateauCount);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(lateCount, Is.EqualTo(1));
            Assert.That(plateauCount, Is.EqualTo(1));
            Assert.That(instrument.StartClocks[0], Is.EqualTo(10_256UL));
            Assert.That(instrument.EndClocks[0], Is.EqualTo(10_257UL));
        }

        [Test]
        public void DispatcherHonorsTheBatchLimit()
        {
            var instrument = new FakeScheduledInstrument();
            var dispatcher = new Fmod.FmodScheduledNoteDispatcher(
                new FakeClockMapper(),
                instrument,
                1UL);
            var source = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(2);
            Assert.That(source.TryEnqueue(CreateEvent(100, 1, 0UL)), Is.True);
            Assert.That(source.TryEnqueue(CreateEvent(101, 1, 1UL)), Is.True);

            int count = dispatcher.DispatchAvailable(source, 1, out _, out _);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(source.Count, Is.EqualTo(1));
            Assert.That(instrument.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void InstrumentFailureRetainsTheExactHeadForRetry()
        {
            var instrument = new FakeScheduledInstrument
            {
                Failure = new InvalidOperationException("Native schedule failed.")
            };
            var dispatcher = new Fmod.FmodScheduledNoteDispatcher(
                new FakeClockMapper(),
                instrument,
                1UL);
            var source = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(1);
            Core.ScheduledNoteEvent scheduledEvent = CreateEvent(100, 1, 7UL);
            Assert.That(source.TryEnqueue(scheduledEvent), Is.True);

            Assert.Throws<InvalidOperationException>(
                () => dispatcher.DispatchAvailable(source, 1, out _, out _));
            Assert.That(source.TryPeek(out Core.ScheduledNoteEvent retained), Is.True);
            Assert.That(retained, Is.EqualTo(scheduledEvent));

            instrument.Failure = null;
            Assert.That(
                dispatcher.DispatchAvailable(source, 1, out _, out _),
                Is.EqualTo(1));
            Assert.That(source.IsEmpty, Is.True);
        }

        [Test]
        public void RepeatedDispatchDoesNotAllocateAfterWarmup()
        {
            var mapper = new FakeClockMapper();
            var instrument = new FakeScheduledInstrument(10_001);
            var dispatcher = new Fmod.FmodScheduledNoteDispatcher(
                mapper,
                instrument,
                1UL);
            var source = new Core.SchedulingBuffer<Core.ScheduledNoteEvent>(1);
            Core.ScheduledNoteEvent scheduledEvent = CreateEvent(100, 1, 0UL);

            Assert.That(source.TryEnqueue(scheduledEvent), Is.True);
            dispatcher.DispatchAvailable(source, 1, out _, out _);

            long beforeBytes = GC.GetAllocatedBytesForCurrentThread();

            for (int index = 0; index < 10_000; index++)
            {
                source.TryEnqueue(scheduledEvent);
                dispatcher.DispatchAvailable(source, 1, out _, out _);
            }

            long afterBytes = GC.GetAllocatedBytesForCurrentThread();

            Assert.That(instrument.CallCount, Is.EqualTo(10_001));
            Assert.That(afterBytes - beforeBytes, Is.Zero);
        }

        private static Core.ScheduledNoteEvent CreateEvent(
            long startTick,
            long durationTicks,
            ulong sequenceNumber)
        {
            return new Core.ScheduledNoteEvent(
                new Core.NoteEvent(
                    new Core.Note(60),
                    100,
                    startTick,
                    durationTicks),
                sequenceNumber);
        }

        private sealed class FakeClockMapper : Fmod.IFmodScheduledNoteClockMapper
        {
            public ulong CurrentDspClock { get; set; } = 1_000UL;

            public ulong BaseDspClock { get; set; } = 10_000UL;

            public ulong SampleFramesPerTick { get; set; } = 1UL;

            public int GetCurrentClockCallCount { get; private set; }

            public ulong GetCurrentDspClock()
            {
                GetCurrentClockCallCount++;
                return CurrentDspClock;
            }

            public ulong ToDspClock(long tick)
            {
                return checked(BaseDspClock + ((ulong)tick * SampleFramesPerTick));
            }
        }

        private sealed class FakeScheduledInstrument : Fmod.IFmodScheduledInstrument
        {
            private readonly Core.NoteEvent[] events;
            private readonly ulong[] startClocks;
            private readonly ulong[] endClocks;

            public FakeScheduledInstrument(int capacity = 16)
            {
                events = new Core.NoteEvent[capacity];
                startClocks = new ulong[capacity];
                endClocks = new ulong[capacity];
            }

            public int CallCount { get; private set; }

            public Exception Failure { get; set; }

            public Core.NoteEvent[] Events => events;

            public ulong[] StartClocks => startClocks;

            public ulong[] EndClocks => endClocks;

            public Core.VoiceHandle ScheduleNote(
                Core.NoteEvent noteEvent,
                ulong startDspClock,
                ulong releaseStartDspClock)
            {
                if (Failure != null)
                {
                    throw Failure;
                }

                events[CallCount] = noteEvent;
                startClocks[CallCount] = startDspClock;
                endClocks[CallCount] = releaseStartDspClock;
                CallCount++;
                return new Core.VoiceHandle((ulong)CallCount);
            }
        }
    }
}
