using System;
using Loom.Core;

namespace Loom.Fmod
{
    /// <summary>
    /// Maps absolute musical ticks into one FMOD parent-channel-group DSP clock domain.
    /// </summary>
    public sealed class FmodTickDspClockMapper
    {
        private const string GetSoftwareFormatOperation =
            "FMOD.System.getSoftwareFormat";
        private const string GetDspClockOperation =
            "FMOD.ChannelGroup.getDSPClock";

        private readonly IFmodDspClockSource clockSource;
        private readonly TickSampleConverter tickSampleConverter;

        /// <summary>
        /// Captures a stable tick-to-clock anchor using the current runtime sample rate and
        /// the DSP clock of the group that will parent scheduled channels.
        /// </summary>
        /// <remarks>
        /// The mapper borrows both FMOD handles. Recreate it after a device change, bank
        /// reload, or parent-group replacement.
        /// </remarks>
        public FmodTickDspClockMapper(
            FMOD.System coreSystem,
            FMOD.ChannelGroup parentChannelGroup,
            TempoMap tempoMap,
            long anchorTick,
            ulong anchorLeadSampleFrames = 0UL)
            : this(
                CreateClockSource(coreSystem, parentChannelGroup),
                tempoMap,
                anchorTick,
                anchorLeadSampleFrames)
        {
        }

        internal FmodTickDspClockMapper(
            IFmodDspClockSource clockSource,
            TempoMap tempoMap,
            long anchorTick,
            ulong anchorLeadSampleFrames = 0UL)
        {
            this.clockSource = clockSource
                ?? throw new ArgumentNullException(nameof(clockSource));

            if (tempoMap == null)
            {
                throw new ArgumentNullException(nameof(tempoMap));
            }

            if (anchorTick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(anchorTick),
                    anchorTick,
                    "Anchor tick cannot be negative.");
            }

            FmodResult.Ensure(
                clockSource.GetSoftwareFormat(out int sampleRate),
                GetSoftwareFormatOperation);

            if (sampleRate <= 0)
            {
                throw new InvalidOperationException(
                    $"FMOD returned an invalid runtime sample rate: {sampleRate}.");
            }

            tickSampleConverter = new TickSampleConverter(tempoMap, sampleRate);
            ulong anchorSampleFrame = tickSampleConverter.ToSampleFrame(anchorTick);

            FmodResult.Ensure(
                clockSource.GetDspClock(out ulong currentDspClock),
                GetDspClockOperation);

            SampleRate = sampleRate;
            AnchorTick = anchorTick;
            AnchorSampleFrame = anchorSampleFrame;
            AnchorDspClock = checked(currentDspClock + anchorLeadSampleFrames);
        }

        public int SampleRate { get; }

        public long AnchorTick { get; }

        public ulong AnchorSampleFrame { get; }

        public ulong AnchorDspClock { get; }

        /// <summary>
        /// Reads the current clock from the same parent-group domain used by the anchor.
        /// </summary>
        public ulong GetCurrentDspClock()
        {
            FmodResult.Ensure(
                clockSource.GetDspClock(out ulong currentDspClock),
                GetDspClockOperation);
            return currentDspClock;
        }

        /// <summary>
        /// Converts an absolute tick without changing the captured anchor.
        /// </summary>
        public ulong ToDspClock(long tick)
        {
            ulong targetSampleFrame = tickSampleConverter.ToSampleFrame(tick);

            if (targetSampleFrame >= AnchorSampleFrame)
            {
                return checked(
                    AnchorDspClock + (targetSampleFrame - AnchorSampleFrame));
            }

            ulong backwardSampleFrames = AnchorSampleFrame - targetSampleFrame;
            if (backwardSampleFrames > AnchorDspClock)
            {
                throw new OverflowException(
                    "Tick maps before the start of the FMOD DSP clock domain.");
            }

            return AnchorDspClock - backwardSampleFrames;
        }

        /// <summary>
        /// Converts a DSP clock in the captured parent-group domain to the greatest
        /// logical tick whose rounded sample position is not after that clock.
        /// </summary>
        /// <exception cref="OverflowException">
        /// Thrown when the clock maps outside the unsigned logical sample-frame domain.
        /// </exception>
        public long ToTickAtOrBeforeDspClock(ulong dspClock)
        {
            if (!TryToTickAtOrBeforeDspClock(dspClock, out long tick))
            {
                throw new OverflowException(
                    "DSP clock maps outside the logical sample-frame domain.");
            }

            return tick;
        }

        /// <summary>
        /// Attempts to convert a DSP clock without rereading or changing the captured anchor.
        /// </summary>
        /// <remarks>
        /// This returns false during preroll before logical sample frame zero and when
        /// affine clock translation exceeds the UInt64 sample-frame range.
        /// </remarks>
        public bool TryToTickAtOrBeforeDspClock(ulong dspClock, out long tick)
        {
            if (!TryResolveSampleFrame(dspClock, out ulong sampleFrame))
            {
                tick = default;
                return false;
            }

            tick = tickSampleConverter.ToTickAtOrBeforeSampleFrame(sampleFrame);
            return true;
        }

        private bool TryResolveSampleFrame(ulong dspClock, out ulong sampleFrame)
        {
            if (dspClock >= AnchorDspClock)
            {
                ulong forwardSampleFrames = dspClock - AnchorDspClock;
                if (forwardSampleFrames > ulong.MaxValue - AnchorSampleFrame)
                {
                    sampleFrame = default;
                    return false;
                }

                sampleFrame = AnchorSampleFrame + forwardSampleFrames;
                return true;
            }

            ulong backwardSampleFrames = AnchorDspClock - dspClock;
            if (backwardSampleFrames > AnchorSampleFrame)
            {
                sampleFrame = default;
                return false;
            }

            sampleFrame = AnchorSampleFrame - backwardSampleFrames;
            return true;
        }

        private static IFmodDspClockSource CreateClockSource(
            FMOD.System coreSystem,
            FMOD.ChannelGroup parentChannelGroup)
        {
            if (!coreSystem.hasHandle())
            {
                throw new ArgumentException(
                    "FMOD Core system must have a valid handle.",
                    nameof(coreSystem));
            }

            if (!parentChannelGroup.hasHandle())
            {
                throw new ArgumentException(
                    "FMOD parent channel group must have a valid handle.",
                    nameof(parentChannelGroup));
            }

            return new NativeFmodDspClockSource(coreSystem, parentChannelGroup);
        }

        private sealed class NativeFmodDspClockSource : IFmodDspClockSource
        {
            private readonly FMOD.System coreSystem;
            private readonly FMOD.ChannelGroup parentChannelGroup;

            public NativeFmodDspClockSource(
                FMOD.System coreSystem,
                FMOD.ChannelGroup parentChannelGroup)
            {
                this.coreSystem = coreSystem;
                this.parentChannelGroup = parentChannelGroup;
            }

            public FMOD.RESULT GetSoftwareFormat(out int sampleRate)
            {
                return coreSystem.getSoftwareFormat(
                    out sampleRate,
                    out _,
                    out _);
            }

            public FMOD.RESULT GetDspClock(out ulong dspClock)
            {
                return parentChannelGroup.getDSPClock(out dspClock, out _);
            }
        }
    }

    internal interface IFmodDspClockSource
    {
        FMOD.RESULT GetSoftwareFormat(out int sampleRate);

        FMOD.RESULT GetDspClock(out ulong dspClock);
    }
}
