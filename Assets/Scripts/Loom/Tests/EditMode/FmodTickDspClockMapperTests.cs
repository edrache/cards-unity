using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class FmodTickDspClockMapperTests
    {
        [Test]
        public void PublicConstructorRejectsInvalidFmodHandles()
        {
            Assert.Throws<ArgumentException>(
                () => new Fmod.FmodTickDspClockMapper(
                    default,
                    default,
                    Core.TempoMap.Default,
                    0));
        }

        [Test]
        public void ConstructorRejectsNullClockSourceBeforeReadingRuntimeState()
        {
            Assert.Throws<ArgumentNullException>(
                () => new Fmod.FmodTickDspClockMapper(
                    null,
                    Core.TempoMap.Default,
                    0));
        }

        [Test]
        public void ConstructorRejectsNullTempoMapBeforeReadingRuntimeState()
        {
            var source = new FakeDspClockSource();

            Assert.Throws<ArgumentNullException>(
                () => new Fmod.FmodTickDspClockMapper(source, null, 0));

            Assert.That(source.GetSoftwareFormatCallCount, Is.Zero);
            Assert.That(source.GetDspClockCallCount, Is.Zero);
        }

        [Test]
        public void ConstructorRejectsNegativeAnchorTickBeforeReadingRuntimeState()
        {
            var source = new FakeDspClockSource();

            ArgumentOutOfRangeException exception =
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => new Fmod.FmodTickDspClockMapper(
                        source,
                        Core.TempoMap.Default,
                        -1));

            Assert.That(exception.ParamName, Is.EqualTo("anchorTick"));
            Assert.That(source.GetSoftwareFormatCallCount, Is.Zero);
            Assert.That(source.GetDspClockCallCount, Is.Zero);
        }

        [Test]
        public void ConstructorPreservesSampleRateFailureContext()
        {
            var source = new FakeDspClockSource
            {
                SoftwareFormatResult = FMOD.RESULT.ERR_INVALID_HANDLE
            };

            Fmod.FmodOperationException exception =
                Assert.Throws<Fmod.FmodOperationException>(
                    () => new Fmod.FmodTickDspClockMapper(
                        source,
                        Core.TempoMap.Default,
                        0));

            Assert.That(
                exception.Operation,
                Is.EqualTo("FMOD.System.getSoftwareFormat"));
            Assert.That(exception.Result, Is.EqualTo(FMOD.RESULT.ERR_INVALID_HANDLE));
            Assert.That(source.GetSoftwareFormatCallCount, Is.EqualTo(1));
            Assert.That(source.GetDspClockCallCount, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ConstructorRejectsInvalidRuntimeSampleRate(int sampleRate)
        {
            var source = new FakeDspClockSource
            {
                SampleRate = sampleRate
            };

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(
                    () => new Fmod.FmodTickDspClockMapper(
                        source,
                        Core.TempoMap.Default,
                        0));

            Assert.That(exception.Message, Does.Contain(sampleRate.ToString()));
            Assert.That(source.GetDspClockCallCount, Is.Zero);
        }

        [Test]
        public void ConstructorPreservesDspClockFailureContext()
        {
            var source = new FakeDspClockSource
            {
                DspClockResult = FMOD.RESULT.ERR_INVALID_HANDLE
            };

            Fmod.FmodOperationException exception =
                Assert.Throws<Fmod.FmodOperationException>(
                    () => new Fmod.FmodTickDspClockMapper(
                        source,
                        Core.TempoMap.Default,
                        0));

            Assert.That(
                exception.Operation,
                Is.EqualTo("FMOD.ChannelGroup.getDSPClock"));
            Assert.That(exception.Result, Is.EqualTo(FMOD.RESULT.ERR_INVALID_HANDLE));
            Assert.That(source.GetSoftwareFormatCallCount, Is.EqualTo(1));
            Assert.That(source.GetDspClockCallCount, Is.EqualTo(1));
        }

        [Test]
        public void ConstructorCapturesRuntimeRateSampleFrameAndClockAnchor()
        {
            var source = new FakeDspClockSource
            {
                SampleRate = 48_000,
                DspClock = 1_000_000UL
            };

            var mapper = new Fmod.FmodTickDspClockMapper(
                source,
                Core.TempoMap.Default,
                Core.MusicalTime.TicksPerQuarterNote,
                4_096UL);

            Assert.That(mapper.SampleRate, Is.EqualTo(48_000));
            Assert.That(
                mapper.AnchorTick,
                Is.EqualTo(Core.MusicalTime.TicksPerQuarterNote));
            Assert.That(mapper.AnchorSampleFrame, Is.EqualTo(24_000UL));
            Assert.That(mapper.AnchorDspClock, Is.EqualTo(1_004_096UL));
            Assert.That(mapper.ToDspClock(mapper.AnchorTick), Is.EqualTo(1_004_096UL));
        }

        [TestCase(44_100, 22_050UL)]
        [TestCase(48_000, 24_000UL)]
        public void MapperUsesTheRuntimeSampleRate(
            int sampleRate,
            ulong expectedQuarterNoteFrames)
        {
            var source = new FakeDspClockSource
            {
                SampleRate = sampleRate,
                DspClock = 50_000UL
            };
            var mapper = new Fmod.FmodTickDspClockMapper(
                source,
                Core.TempoMap.Default,
                0);

            Assert.That(
                mapper.ToDspClock(Core.MusicalTime.TicksPerQuarterNote),
                Is.EqualTo(50_000UL + expectedQuarterNoteFrames));
        }

        [Test]
        public void MapperConvertsForwardAndBackwardRelativeToTheAnchor()
        {
            var source = new FakeDspClockSource
            {
                SampleRate = 48_000,
                DspClock = 1_000_000UL
            };
            var mapper = new Fmod.FmodTickDspClockMapper(
                source,
                Core.TempoMap.Default,
                Core.MusicalTime.TicksPerQuarterNote);

            Assert.That(mapper.ToDspClock(0), Is.EqualTo(976_000UL));
            Assert.That(mapper.ToDspClock(960), Is.EqualTo(1_000_000UL));
            Assert.That(mapper.ToDspClock(961), Is.EqualTo(1_000_025UL));
            Assert.That(mapper.ToDspClock(1_920), Is.EqualTo(1_024_000UL));
        }

        [Test]
        public void MapperConvertsAcrossTempoBoundariesRelativeToTheAnchor()
        {
            var tempoMap = new Core.TempoMap(
                new Core.TempoSegment(0, 120d),
                new Core.TempoSegment(960, 60d),
                new Core.TempoSegment(1_920, 240d));
            var source = new FakeDspClockSource
            {
                SampleRate = 48_000,
                DspClock = 100_000UL
            };
            var mapper = new Fmod.FmodTickDspClockMapper(
                source,
                tempoMap,
                960);

            Assert.That(mapper.AnchorSampleFrame, Is.EqualTo(24_000UL));
            Assert.That(mapper.ToDspClock(1_920), Is.EqualTo(148_000UL));
            Assert.That(mapper.ToDspClock(2_880), Is.EqualTo(160_000UL));
        }

        [Test]
        public void RepeatedConversionsDoNotReadOrMoveTheNativeClock()
        {
            var source = new FakeDspClockSource
            {
                DspClock = 100_000UL
            };
            var mapper = new Fmod.FmodTickDspClockMapper(
                source,
                Core.TempoMap.Default,
                0);
            source.DspClock = 900_000UL;

            Assert.That(mapper.ToDspClock(0), Is.EqualTo(100_000UL));
            Assert.That(mapper.ToDspClock(960), Is.EqualTo(124_000UL));
            Assert.That(mapper.ToDspClock(960), Is.EqualTo(124_000UL));
            Assert.That(source.GetDspClockCallCount, Is.EqualTo(1));
        }

        [Test]
        public void GetCurrentDspClockReadsTheSameSourceAndChecksItsResult()
        {
            var source = new FakeDspClockSource
            {
                DspClock = 100_000UL
            };
            var mapper = new Fmod.FmodTickDspClockMapper(
                source,
                Core.TempoMap.Default,
                0);
            source.DspClock = 125_000UL;

            Assert.That(mapper.GetCurrentDspClock(), Is.EqualTo(125_000UL));
            Assert.That(source.GetDspClockCallCount, Is.EqualTo(2));

            source.DspClockResult = FMOD.RESULT.ERR_INVALID_HANDLE;
            Fmod.FmodOperationException exception =
                Assert.Throws<Fmod.FmodOperationException>(
                    () => mapper.GetCurrentDspClock());
            Assert.That(
                exception.Operation,
                Is.EqualTo("FMOD.ChannelGroup.getDSPClock"));
        }

        [Test]
        public void MapperRejectsNegativeTargetTick()
        {
            var mapper = CreateDefaultMapper();

            ArgumentOutOfRangeException exception =
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => mapper.ToDspClock(-1));

            Assert.That(exception.ParamName, Is.EqualTo("tick"));
        }

        [Test]
        public void MapperAllowsAResultAtTheMaximumDspClock()
        {
            var source = new FakeDspClockSource
            {
                DspClock = ulong.MaxValue - 25UL
            };
            var mapper = new Fmod.FmodTickDspClockMapper(
                source,
                Core.TempoMap.Default,
                0);

            Assert.That(mapper.ToDspClock(1), Is.EqualTo(ulong.MaxValue));
        }

        [Test]
        public void MapperRejectsDspClockAdditionOverflow()
        {
            var source = new FakeDspClockSource
            {
                DspClock = ulong.MaxValue - 25UL
            };
            var mapper = new Fmod.FmodTickDspClockMapper(
                source,
                Core.TempoMap.Default,
                0);

            Assert.Throws<OverflowException>(() => mapper.ToDspClock(2));
        }

        [Test]
        public void MapperRejectsDspClockSubtractionUnderflow()
        {
            var source = new FakeDspClockSource
            {
                DspClock = 24UL
            };
            var mapper = new Fmod.FmodTickDspClockMapper(
                source,
                Core.TempoMap.Default,
                1);

            Assert.Throws<OverflowException>(() => mapper.ToDspClock(0));
        }

        [Test]
        public void ConstructorRejectsAnchorLeadOverflow()
        {
            var source = new FakeDspClockSource
            {
                DspClock = ulong.MaxValue
            };

            Assert.Throws<OverflowException>(
                () => new Fmod.FmodTickDspClockMapper(
                    source,
                    Core.TempoMap.Default,
                    0,
                    1UL));
        }

        [Test]
        public void MapperPropagatesSampleFrameOverflow()
        {
            var mapper = CreateDefaultMapper();

            Assert.Throws<OverflowException>(
                () => mapper.ToDspClock(long.MaxValue));
        }

        private static Fmod.FmodTickDspClockMapper CreateDefaultMapper()
        {
            return new Fmod.FmodTickDspClockMapper(
                new FakeDspClockSource(),
                Core.TempoMap.Default,
                0);
        }

        private sealed class FakeDspClockSource : Fmod.IFmodDspClockSource
        {
            public FMOD.RESULT SoftwareFormatResult { get; set; } = FMOD.RESULT.OK;

            public FMOD.RESULT DspClockResult { get; set; } = FMOD.RESULT.OK;

            public int SampleRate { get; set; } = 48_000;

            public ulong DspClock { get; set; } = 1_000_000UL;

            public int GetSoftwareFormatCallCount { get; private set; }

            public int GetDspClockCallCount { get; private set; }

            public FMOD.RESULT GetSoftwareFormat(out int sampleRate)
            {
                GetSoftwareFormatCallCount++;
                sampleRate = SampleRate;
                return SoftwareFormatResult;
            }

            public FMOD.RESULT GetDspClock(out ulong dspClock)
            {
                GetDspClockCallCount++;
                dspClock = DspClock;
                return DspClockResult;
            }
        }
    }
}
