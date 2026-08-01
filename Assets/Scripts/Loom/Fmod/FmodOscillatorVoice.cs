using System;
using Loom.Core;

namespace Loom.Fmod
{
    public sealed class FmodOscillatorVoice : IDisposable
    {
        private const int SineWaveform = 0;

        private readonly FMOD.System coreSystem;
        private readonly FmodAdsrEnvelopeSamples envelopeSamples;
        private FMOD.DSP oscillator;
        private FMOD.Channel channel;
        private FMOD.ChannelGroup parentChannelGroup;
        private uint dspBufferLength;
        private ulong scheduledStartDspClock;
        private ulong releaseStartDspClock;
        private ulong releaseEndDspClock;
        private float releaseStartLevel;
        private bool isReleaseScheduled;
        private bool isReleased;

        public FmodOscillatorVoice(FMOD.System coreSystem, Note note, float gain = 0.1f)
            : this(coreSystem, note, FmodAdsrEnvelope.Default, gain)
        {
        }

        public FmodOscillatorVoice(
            FMOD.System coreSystem,
            Note note,
            FmodAdsrEnvelope envelope,
            float gain = 0.1f)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            ValidateGain(gain);

            if (!coreSystem.hasHandle())
            {
                throw new ArgumentException(
                    "FMOD Core system must have a valid handle.",
                    nameof(coreSystem));
            }

            this.coreSystem = coreSystem;
            Note = note;
            Gain = gain;
            Envelope = envelope;

            FmodResult.Ensure(
                coreSystem.getSoftwareFormat(
                    out int sampleRate,
                    out _,
                    out _),
                "FMOD.System.getSoftwareFormat");

            SampleRate = sampleRate;
            envelopeSamples = Envelope.ResolveSampleFrames(SampleRate);

            CreateOscillator();
        }

        public Note Note { get; }

        public float Gain { get; }

        public FmodAdsrEnvelope Envelope { get; }

        public int SampleRate { get; }

        public FmodAdsrEnvelopeSamples EnvelopeSamples => envelopeSamples;

        public bool IsCreated => oscillator.hasHandle();

        public bool IsStarted
        {
            get
            {
                SynchronizeScheduledStop();
                return channel.hasHandle();
            }
        }

        public bool IsReleasing
        {
            get
            {
                SynchronizeScheduledStop();
                return isReleaseScheduled && channel.hasHandle();
            }
        }

        public bool IsReleaseComplete
        {
            get
            {
                SynchronizeScheduledStop();
                return isReleaseScheduled && !channel.hasHandle();
            }
        }

        public ulong ScheduledStartDspClock => scheduledStartDspClock;

        public ulong ReleaseStartDspClock => releaseStartDspClock;

        public ulong ReleaseEndDspClock => releaseEndDspClock;

        public float ReleaseStartLevel => releaseStartLevel;

        public bool IsPlaying
        {
            get
            {
                SynchronizeScheduledStop();
                if (!channel.hasHandle())
                {
                    return false;
                }

                FmodResult.Ensure(
                    channel.isPlaying(out bool isPlaying),
                    "FMOD.Channel.isPlaying");

                return isPlaying;
            }
        }

        public float CurrentEnvelopeLevel
        {
            get
            {
                SynchronizeScheduledStop();
                if (!channel.hasHandle())
                {
                    return 0f;
                }

                ulong currentDspClock = GetParentDspClock();
                if (currentDspClock <= scheduledStartDspClock)
                {
                    return 0f;
                }

                if (!isReleaseScheduled || currentDspClock <= releaseStartDspClock)
                {
                    return envelopeSamples.GetAttackDecaySustainLevel(
                        currentDspClock - scheduledStartDspClock);
                }

                if (currentDspClock >= releaseEndDspClock)
                {
                    return 0f;
                }

                double releaseProgress =
                    (double)(currentDspClock - releaseStartDspClock) /
                    envelopeSamples.ReleaseFrames;
                return releaseStartLevel * (float)(1d - releaseProgress);
            }
        }

        public void Start()
        {
            ThrowIfReleased();

            SynchronizeScheduledStop();
            if (channel.hasHandle())
            {
                throw new InvalidOperationException("The oscillator voice is already started.");
            }

            ResetPlaybackState();

            FmodResult.Ensure(
                coreSystem.playDSP(oscillator, default, true, out channel),
                "FMOD.System.playDSP(OSCILLATOR)");

            try
            {
                FmodResult.Ensure(
                    channel.setVolume(Gain),
                    "FMOD.Channel.setVolume");

                FmodResult.Ensure(
                    channel.getChannelGroup(out parentChannelGroup),
                    "FMOD.Channel.getChannelGroup");

                FmodResult.Ensure(
                    coreSystem.getDSPBufferSize(out dspBufferLength, out _),
                    "FMOD.System.getDSPBufferSize");

                ulong currentDspClock = GetParentDspClock();
                scheduledStartDspClock = checked(currentDspClock + dspBufferLength);

                FmodResult.Ensure(
                    channel.setDelay(scheduledStartDspClock, 0UL, true),
                    "FMOD.Channel.setDelay(ADSR start)");

                ScheduleAttackDecaySustain();

                FmodResult.Ensure(
                    channel.setPaused(false),
                    "FMOD.Channel.setPaused(false)");
            }
            catch (Exception startException)
            {
                Exception cleanupException = TryStopChannelAfterFailure();
                if (cleanupException != null)
                {
                    throw new AggregateException(startException, cleanupException);
                }

                throw;
            }
        }

        public void BeginRelease()
        {
            ThrowIfReleased();
            SynchronizeScheduledStop();

            if (!channel.hasHandle())
            {
                throw new InvalidOperationException(
                    "The oscillator voice must be started before its envelope can be released.");
            }

            if (isReleaseScheduled)
            {
                return;
            }

            try
            {
                ulong currentDspClock = GetParentDspClock();
                ulong earliestReleaseDspClock = checked(currentDspClock + dspBufferLength);
                releaseStartDspClock = Math.Max(
                    earliestReleaseDspClock,
                    scheduledStartDspClock);
                releaseStartLevel = envelopeSamples.GetAttackDecaySustainLevel(
                    releaseStartDspClock - scheduledStartDspClock);
                releaseEndDspClock = checked(
                    releaseStartDspClock + envelopeSamples.ReleaseFrames);

                FmodResult.Ensure(
                    channel.removeFadePoints(releaseStartDspClock, ulong.MaxValue),
                    "FMOD.Channel.removeFadePoints(ADSR release)");

                AddFadePoint(
                    releaseStartDspClock,
                    releaseStartLevel,
                    "ADSR release start");
                AddFadePoint(releaseEndDspClock, 0f, "ADSR release end");

                ulong preservedStartDspClock =
                    currentDspClock < scheduledStartDspClock
                        ? scheduledStartDspClock
                        : 0UL;
                FmodResult.Ensure(
                    channel.setDelay(preservedStartDspClock, releaseEndDspClock, true),
                    "FMOD.Channel.setDelay(ADSR release stop)");

                isReleaseScheduled = true;
            }
            catch (Exception releaseException)
            {
                Exception cleanupException = TryStopChannelAfterFailure();
                if (cleanupException != null)
                {
                    throw new AggregateException(releaseException, cleanupException);
                }

                throw;
            }
        }

        public void Stop()
        {
            SynchronizeScheduledStop();
            if (!channel.hasHandle())
            {
                return;
            }

            FmodResult.Ensure(
                channel.stop(),
                "FMOD.Channel.stop");

            channel.clearHandle();
            parentChannelGroup.clearHandle();
        }

        public void Release()
        {
            if (isReleased)
            {
                return;
            }

            Stop();

            if (oscillator.hasHandle())
            {
                FmodResult.Ensure(
                    oscillator.release(),
                    "FMOD.DSP.release(OSCILLATOR)");

                oscillator.clearHandle();
            }

            isReleased = true;
        }

        public void Dispose()
        {
            Release();
        }

        private static void ValidateGain(float gain)
        {
            if (float.IsNaN(gain) || float.IsInfinity(gain) || gain < 0f || gain > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(gain),
                    gain,
                    "Gain must be a finite value between 0 and 1.");
            }
        }

        private void CreateOscillator()
        {
            FmodResult.Ensure(
                coreSystem.createDSPByType(FMOD.DSP_TYPE.OSCILLATOR, out oscillator),
                "FMOD.System.createDSPByType(OSCILLATOR)");

            try
            {
                FmodResult.Ensure(
                    oscillator.setParameterInt((int)FMOD.DSP_OSCILLATOR.TYPE, SineWaveform),
                    "FMOD.DSP.setParameterInt(OSCILLATOR.TYPE)");

                float frequencyHz = (float)Note.FrequencyHz;
                FmodResult.Ensure(
                    oscillator.setParameterFloat((int)FMOD.DSP_OSCILLATOR.RATE, frequencyHz),
                    "FMOD.DSP.setParameterFloat(OSCILLATOR.RATE)");
            }
            catch (Exception creationException)
            {
                Exception cleanupException = TryReleaseOscillatorAfterFailure();
                if (cleanupException != null)
                {
                    throw new AggregateException(creationException, cleanupException);
                }

                throw;
            }
        }

        private void ScheduleAttackDecaySustain()
        {
            ulong attackEndDspClock = checked(
                scheduledStartDspClock + envelopeSamples.AttackFrames);

            AddFadePoint(scheduledStartDspClock, 0f, "ADSR attack start");

            if (envelopeSamples.DecayFrames == 0UL)
            {
                AddFadePoint(
                    attackEndDspClock,
                    envelopeSamples.SustainLevel,
                    "ADSR attack end and sustain");
                return;
            }

            AddFadePoint(attackEndDspClock, 1f, "ADSR attack end");

            ulong decayEndDspClock = checked(
                attackEndDspClock + envelopeSamples.DecayFrames);
            AddFadePoint(
                decayEndDspClock,
                envelopeSamples.SustainLevel,
                "ADSR decay end and sustain");
        }

        private void AddFadePoint(ulong dspClock, float level, string context)
        {
            FmodResult.Ensure(
                channel.addFadePoint(dspClock, level),
                $"FMOD.Channel.addFadePoint({context})");
        }

        private ulong GetParentDspClock()
        {
            FmodResult.Ensure(
                parentChannelGroup.getDSPClock(out ulong dspClock, out _),
                "FMOD.ChannelGroup.getDSPClock");
            return dspClock;
        }

        private void SynchronizeScheduledStop()
        {
            if (!isReleaseScheduled || !channel.hasHandle())
            {
                return;
            }

            if (GetParentDspClock() < releaseEndDspClock)
            {
                return;
            }

            channel.clearHandle();
            parentChannelGroup.clearHandle();
        }

        private void ResetPlaybackState()
        {
            parentChannelGroup.clearHandle();
            dspBufferLength = 0U;
            scheduledStartDspClock = 0UL;
            releaseStartDspClock = 0UL;
            releaseEndDspClock = 0UL;
            releaseStartLevel = 0f;
            isReleaseScheduled = false;
        }

        private Exception TryStopChannelAfterFailure()
        {
            if (!channel.hasHandle())
            {
                return null;
            }

            FMOD.RESULT result = channel.stop();
            if (result != FMOD.RESULT.OK)
            {
                return new FmodOperationException(
                    "FMOD.Channel.stop after failed voice start",
                    result);
            }

            channel.clearHandle();
            parentChannelGroup.clearHandle();
            return null;
        }

        private Exception TryReleaseOscillatorAfterFailure()
        {
            if (!oscillator.hasHandle())
            {
                return null;
            }

            FMOD.RESULT result = oscillator.release();
            if (result != FMOD.RESULT.OK)
            {
                return new FmodOperationException(
                    "FMOD.DSP.release after failed oscillator creation",
                    result);
            }

            oscillator.clearHandle();
            return null;
        }

        private void ThrowIfReleased()
        {
            if (isReleased)
            {
                throw new ObjectDisposedException(nameof(FmodOscillatorVoice));
            }
        }
    }
}
