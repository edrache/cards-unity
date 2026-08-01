using System;
using System.Collections.Generic;
using Loom.Core;

namespace Loom.Fmod
{
    public sealed class FmodOscillatorVoice : IDisposable
    {
        private readonly FMOD.System coreSystem;
        private readonly FmodAdsrEnvelopeSamples envelopeSamples;
        private FMOD.DSP oscillator;
        private FMOD.DSP lowPassFilter;
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
            : this(
                coreSystem,
                note,
                FmodAdsrEnvelope.Default,
                new FmodOscillatorSettings(gain: gain))
        {
        }

        public FmodOscillatorVoice(
            FMOD.System coreSystem,
            Note note,
            FmodAdsrEnvelope envelope,
            float gain = 0.1f)
            : this(
                coreSystem,
                note,
                envelope,
                new FmodOscillatorSettings(gain: gain))
        {
        }

        public FmodOscillatorVoice(
            FMOD.System coreSystem,
            Note note,
            FmodAdsrEnvelope envelope,
            FmodOscillatorSettings settings)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!coreSystem.hasHandle())
            {
                throw new ArgumentException(
                    "FMOD Core system must have a valid handle.",
                    nameof(coreSystem));
            }

            this.coreSystem = coreSystem;
            Note = note;
            Envelope = envelope;
            Waveform = settings.Waveform;
            Gain = settings.Gain;
            Octave = settings.Octave;
            FrequencyHz = settings.ResolveFrequencyHz(Note);
            CutoffHz = settings.CutoffHz;
            Resonance = settings.Resonance;

            FmodResult.Ensure(
                coreSystem.getSoftwareFormat(
                    out int sampleRate,
                    out _,
                    out _),
                "FMOD.System.getSoftwareFormat");

            SampleRate = sampleRate;
            envelopeSamples = Envelope.ResolveSampleFrames(SampleRate);

            CreateDspGraph();
        }

        public Note Note { get; private set; }

        public FmodOscillatorWaveform Waveform { get; private set; }

        public float Gain { get; private set; }

        public int Octave { get; private set; }

        public float FrequencyHz { get; private set; }

        public float CutoffHz { get; private set; }

        public float Resonance { get; private set; }

        public FmodAdsrEnvelope Envelope { get; }

        public int SampleRate { get; }

        public FmodAdsrEnvelopeSamples EnvelopeSamples => envelopeSamples;

        public bool IsCreated => oscillator.hasHandle() && lowPassFilter.hasHandle();

        public bool IsOscillatorCreated => oscillator.hasHandle();

        public bool IsFilterCreated => lowPassFilter.hasHandle();

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

        public void SetWaveform(FmodOscillatorWaveform waveform)
        {
            ThrowIfReleased();
            FmodOscillatorSettings.ValidateWaveform(waveform);

            SetOscillatorWaveform(waveform);
            Waveform = waveform;
        }

        public void SetNote(Note note)
        {
            ThrowIfReleased();
            float frequencyHz = FmodOscillatorSettings.ResolveFrequencyHz(
                note,
                Octave);

            SetOscillatorFrequency(frequencyHz);
            Note = note;
            FrequencyHz = frequencyHz;
        }

        public void SetGain(float gain)
        {
            ThrowIfReleased();
            FmodOscillatorSettings.ValidateGain(gain);
            SynchronizeScheduledStop();

            if (channel.hasHandle())
            {
                FmodResult.Ensure(
                    channel.setVolume(gain),
                    "FMOD.Channel.setVolume(gain control)");
            }

            Gain = gain;
        }

        public void SetOctave(int octave)
        {
            ThrowIfReleased();
            float frequencyHz = FmodOscillatorSettings.ResolveFrequencyHz(Note, octave);

            SetOscillatorFrequency(frequencyHz);
            Octave = octave;
            FrequencyHz = frequencyHz;
        }

        public void SetCutoffHz(float cutoffHz)
        {
            ThrowIfReleased();
            FmodOscillatorSettings.ValidateCutoffHz(cutoffHz);

            SetFilterParameter(
                FMOD.DSP_MULTIBAND_EQ.A_FREQUENCY,
                cutoffHz,
                "FMOD.DSP.setParameterFloat(MULTIBAND_EQ.A_FREQUENCY)");
            CutoffHz = cutoffHz;
        }

        public void SetResonance(float resonance)
        {
            ThrowIfReleased();
            FmodOscillatorSettings.ValidateResonance(resonance);

            SetFilterParameter(
                FMOD.DSP_MULTIBAND_EQ.A_Q,
                resonance,
                "FMOD.DSP.setParameterFloat(MULTIBAND_EQ.A_Q)");
            Resonance = resonance;
        }

        public void Start()
        {
            StartCore(
                default,
                "FMOD.System.playDSP(OSCILLATOR, default Core output)");
        }

        public void Start(FMOD.ChannelGroup targetChannelGroup)
        {
            if (!targetChannelGroup.hasHandle())
            {
                throw new ArgumentException(
                    "Target FMOD ChannelGroup must have a valid handle.",
                    nameof(targetChannelGroup));
            }

            StartCore(
                targetChannelGroup,
                "FMOD.System.playDSP(OSCILLATOR, target Studio bus ChannelGroup)");
        }

        private void StartCore(
            FMOD.ChannelGroup targetChannelGroup,
            string playOperation)
        {
            ThrowIfReleased();

            SynchronizeScheduledStop();
            if (channel.hasHandle())
            {
                throw new InvalidOperationException("The oscillator voice is already started.");
            }

            ResetPlaybackState();

            FmodResult.Ensure(
                coreSystem.playDSP(
                    oscillator,
                    targetChannelGroup,
                    true,
                    out channel),
                playOperation);

            try
            {
                FmodResult.Ensure(
                    channel.setVolume(Gain),
                    "FMOD.Channel.setVolume");

                FmodResult.Ensure(
                    channel.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.HEAD, lowPassFilter),
                    "FMOD.Channel.addDSP(MULTIBAND_EQ low-pass)");

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

            var failures = new List<Exception>();

            try
            {
                Stop();
            }
            catch (Exception stopException)
            {
                failures.Add(stopException);
            }

            AddFailureIfPresent(
                failures,
                TryReleaseDsp(ref lowPassFilter, "FMOD.DSP.release(MULTIBAND_EQ)"));
            AddFailureIfPresent(
                failures,
                TryReleaseDsp(ref oscillator, "FMOD.DSP.release(OSCILLATOR)"));

            if (failures.Count == 1)
            {
                throw failures[0];
            }

            if (failures.Count > 1)
            {
                throw new AggregateException(failures);
            }

            isReleased = true;
        }

        public void Dispose()
        {
            Release();
        }

        private void CreateDspGraph()
        {
            try
            {
                FmodResult.Ensure(
                    coreSystem.createDSPByType(FMOD.DSP_TYPE.OSCILLATOR, out oscillator),
                    "FMOD.System.createDSPByType(OSCILLATOR)");

                SetOscillatorWaveform(Waveform);
                SetOscillatorFrequency(FrequencyHz);

                FmodResult.Ensure(
                    coreSystem.createDSPByType(
                        FMOD.DSP_TYPE.MULTIBAND_EQ,
                        out lowPassFilter),
                    "FMOD.System.createDSPByType(MULTIBAND_EQ)");

                FmodResult.Ensure(
                    lowPassFilter.setParameterInt(
                        (int)FMOD.DSP_MULTIBAND_EQ.A_FILTER,
                        (int)FMOD.DSP_MULTIBAND_EQ_FILTER_TYPE.LOWPASS_24DB),
                    "FMOD.DSP.setParameterInt(MULTIBAND_EQ.A_FILTER=LOWPASS_24DB)");

                SetFilterParameter(
                    FMOD.DSP_MULTIBAND_EQ.A_FREQUENCY,
                    CutoffHz,
                    "FMOD.DSP.setParameterFloat(MULTIBAND_EQ.A_FREQUENCY)");
                SetFilterParameter(
                    FMOD.DSP_MULTIBAND_EQ.A_Q,
                    Resonance,
                    "FMOD.DSP.setParameterFloat(MULTIBAND_EQ.A_Q)");
            }
            catch (Exception creationException)
            {
                var failures = new List<Exception> { creationException };
                AddFailureIfPresent(
                    failures,
                    TryReleaseDsp(
                        ref lowPassFilter,
                        "FMOD.DSP.release(MULTIBAND_EQ) after graph creation failure"));
                AddFailureIfPresent(
                    failures,
                    TryReleaseDsp(
                        ref oscillator,
                        "FMOD.DSP.release(OSCILLATOR) after graph creation failure"));

                if (failures.Count > 1)
                {
                    throw new AggregateException(failures);
                }

                throw;
            }
        }

        private void SetOscillatorWaveform(FmodOscillatorWaveform waveform)
        {
            FmodResult.Ensure(
                oscillator.setParameterInt(
                    (int)FMOD.DSP_OSCILLATOR.TYPE,
                    (int)waveform),
                "FMOD.DSP.setParameterInt(OSCILLATOR.TYPE)");
        }

        private void SetOscillatorFrequency(float frequencyHz)
        {
            FmodResult.Ensure(
                oscillator.setParameterFloat(
                    (int)FMOD.DSP_OSCILLATOR.RATE,
                    frequencyHz),
                "FMOD.DSP.setParameterFloat(OSCILLATOR.RATE)");
        }

        private void SetFilterParameter(
            FMOD.DSP_MULTIBAND_EQ parameter,
            float value,
            string operation)
        {
            FmodResult.Ensure(
                lowPassFilter.setParameterFloat((int)parameter, value),
                operation);
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

        private static Exception TryReleaseDsp(ref FMOD.DSP dsp, string operation)
        {
            if (!dsp.hasHandle())
            {
                return null;
            }

            FMOD.RESULT result = dsp.release();
            if (result != FMOD.RESULT.OK)
            {
                return new FmodOperationException(operation, result);
            }

            dsp.clearHandle();
            return null;
        }

        private static void AddFailureIfPresent(
            ICollection<Exception> failures,
            Exception failure)
        {
            if (failure != null)
            {
                failures.Add(failure);
            }
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
