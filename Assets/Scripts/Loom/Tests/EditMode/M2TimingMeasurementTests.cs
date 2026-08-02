using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class M2TimingMeasurementTests
    {
        private const int SampleRate = 48_000;
        private const long EndTick = 1_152_000L;
        private const ulong EndSampleFrame = 28_800_000UL;
        private const ulong LookaheadSampleFrames = 9_600UL;
        private const int ExpectedEventCount = 1_200;
        private const long ExpectedLastEventTick = 1_151_040L;
        private const long ExpectedStartTickSum = 690_624_000L;

        private static readonly int[] IrregularCadenceSampleFrames =
        {
            800, 800, 800, 7_200, 400, 1_200, 600, 1_000
        };

        [Test]
        public void TenMinuteTransportHasNoDriftCadenceDependencyOrAllocations()
        {
            RunSimulation(useIrregularCadence: true, measureAllocations: false);

            TimingResult irregular = RunSimulation(
                useIrregularCadence: true,
                measureAllocations: true);
            TimingResult regular = RunSimulation(
                useIrregularCadence: false,
                measureAllocations: false);

            Assert.That(irregular.EventCount, Is.EqualTo(ExpectedEventCount));
            Assert.That(irregular.MissingEventCount, Is.Zero);
            Assert.That(irregular.DuplicateEventCount, Is.Zero);
            Assert.That(irregular.OutOfOrderEventCount, Is.Zero);
            Assert.That(irregular.MaximumAbsoluteTickDrift, Is.Zero);
            Assert.That(irregular.MaximumAbsoluteFrameDrift, Is.Zero);
            Assert.That(irregular.FinalLogicalFrameError, Is.Zero);
            Assert.That(irregular.UnderrunUpdateCount, Is.Zero);
            Assert.That(irregular.BufferFullUpdateCount, Is.Zero);
            Assert.That(irregular.LastEventTick, Is.EqualTo(ExpectedLastEventTick));
            Assert.That(irregular.StartTickSum, Is.EqualTo(ExpectedStartTickSum));
            Assert.That(irregular.AllocatedBytes, Is.Zero);

            Assert.That(regular.EventCount, Is.EqualTo(irregular.EventCount));
            Assert.That(regular.StreamChecksum, Is.EqualTo(irregular.StreamChecksum));
            Assert.That(regular.StartTickSum, Is.EqualTo(irregular.StartTickSum));
            Assert.That(regular.MaximumAbsoluteTickDrift, Is.Zero);
            Assert.That(regular.MaximumAbsoluteFrameDrift, Is.Zero);
            Assert.That(regular.UnderrunUpdateCount, Is.Zero);
        }

        private static TimingResult RunSimulation(
            bool useIrregularCadence,
            bool measureAllocations)
        {
            var converter = new Core.TickSampleConverter(
                Core.TempoMap.Default,
                SampleRate);
            var pattern = new Core.StepPattern(
                1,
                new Core.PatternStep(new Core.Note(60), 100, 480L));
            var transport = new Core.Transport(
                new Core.StepSequencer(pattern, 123UL, 7UL),
                32);
            var result = new TimingResult
            {
                StreamChecksum = 14_695_981_039_346_656_037UL,
                LastEventTick = -1L
            };

            converter.ToSampleFrame(0L);
            converter.ToTickAtOrBeforeSampleFrame(0UL);
            transport.Start();
            GC.GetAllocatedBytesForCurrentThread();
            long beforeBytes = measureAllocations
                ? GC.GetAllocatedBytesForCurrentThread()
                : 0L;

            ulong currentSampleFrame = 0UL;
            int cadenceIndex = 0;

            while (true)
            {
                long currentTick = converter.ToTickAtOrBeforeSampleFrame(
                    currentSampleFrame);
                ulong targetSampleFrame = currentSampleFrame
                    >= EndSampleFrame - LookaheadSampleFrames
                        ? EndSampleFrame
                        : currentSampleFrame + LookaheadSampleFrames;
                long targetTickExclusive = targetSampleFrame == EndSampleFrame
                    ? EndTick
                    : converter.ToTickAtOrBeforeSampleFrame(targetSampleFrame) + 1L;
                Core.TransportUpdateStatus status = transport.Update(
                    currentTick,
                    targetTickExclusive);
                if ((status & Core.TransportUpdateStatus.Underrun) != 0)
                {
                    result.UnderrunUpdateCount++;
                }

                if ((status & Core.TransportUpdateStatus.BufferFull) != 0)
                {
                    result.BufferFullUpdateCount++;
                }

                while (transport.TryDequeueScheduledEvent(
                    out Core.ScheduledNoteEvent scheduledEvent))
                {
                    AccumulateEvent(ref result, scheduledEvent, converter);
                }

                if (currentSampleFrame == EndSampleFrame)
                {
                    break;
                }

                ulong cadence = useIrregularCadence
                    ? (ulong)IrregularCadenceSampleFrames[
                        cadenceIndex % IrregularCadenceSampleFrames.Length]
                    : 800UL;
                cadenceIndex++;
                currentSampleFrame = cadence >= EndSampleFrame - currentSampleFrame
                    ? EndSampleFrame
                    : currentSampleFrame + cadence;
            }

            long afterBytes = measureAllocations
                ? GC.GetAllocatedBytesForCurrentThread()
                : beforeBytes;
            result.AllocatedBytes = afterBytes - beforeBytes;
            ulong resolvedFinalFrame = converter.ToSampleFrame(EndTick);
            result.FinalLogicalFrameError = resolvedFinalFrame >= EndSampleFrame
                ? resolvedFinalFrame - EndSampleFrame
                : EndSampleFrame - resolvedFinalFrame;
            return result;
        }

        private static void AccumulateEvent(
            ref TimingResult result,
            Core.ScheduledNoteEvent scheduledEvent,
            Core.TickSampleConverter converter)
        {
            long expectedTick = (long)result.EventCount
                * Core.MusicalTime.TicksPerQuarterNote;
            long actualTick = scheduledEvent.StartTick;
            long tickDrift = actualTick >= expectedTick
                ? actualTick - expectedTick
                : expectedTick - actualTick;
            if (tickDrift > result.MaximumAbsoluteTickDrift)
            {
                result.MaximumAbsoluteTickDrift = tickDrift;
            }

            ulong expectedFrame = (ulong)result.EventCount * 24_000UL;
            ulong actualFrame = converter.ToSampleFrame(actualTick);
            ulong frameDrift = actualFrame >= expectedFrame
                ? actualFrame - expectedFrame
                : expectedFrame - actualFrame;
            if (frameDrift > result.MaximumAbsoluteFrameDrift)
            {
                result.MaximumAbsoluteFrameDrift = frameDrift;
            }

            if (actualTick > expectedTick)
            {
                result.MissingEventCount += (int)(
                    (actualTick - expectedTick)
                    / Core.MusicalTime.TicksPerQuarterNote);
            }
            else if (actualTick < expectedTick)
            {
                result.DuplicateEventCount++;
            }

            if (result.LastEventTick >= actualTick)
            {
                result.OutOfOrderEventCount++;
            }

            result.LastEventTick = actualTick;
            result.StartTickSum += actualTick;
            result.StreamChecksum ^= (ulong)actualTick;
            result.StreamChecksum *= 1_099_511_628_211UL;
            result.StreamChecksum ^= scheduledEvent.SequenceNumber;
            result.StreamChecksum *= 1_099_511_628_211UL;
            result.EventCount++;
        }

        private struct TimingResult
        {
            public int EventCount;
            public int MissingEventCount;
            public int DuplicateEventCount;
            public int OutOfOrderEventCount;
            public int UnderrunUpdateCount;
            public int BufferFullUpdateCount;
            public long MaximumAbsoluteTickDrift;
            public ulong MaximumAbsoluteFrameDrift;
            public ulong FinalLogicalFrameError;
            public long LastEventTick;
            public long StartTickSum;
            public ulong StreamChecksum;
            public long AllocatedBytes;
        }
    }
}
