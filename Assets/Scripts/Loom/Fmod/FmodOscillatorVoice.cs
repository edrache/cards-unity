using System;
using Loom.Core;

namespace Loom.Fmod
{
    public sealed class FmodOscillatorVoice : IDisposable
    {
        private const int SineWaveform = 0;

        private readonly FMOD.System coreSystem;
        private FMOD.DSP oscillator;
        private FMOD.Channel channel;
        private bool isReleased;

        public FmodOscillatorVoice(FMOD.System coreSystem, Note note, float gain = 0.1f)
        {
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

            CreateOscillator();
        }

        public Note Note { get; }

        public float Gain { get; }

        public bool IsCreated => oscillator.hasHandle();

        public bool IsStarted => channel.hasHandle();

        public bool IsPlaying
        {
            get
            {
                if (!IsStarted)
                {
                    return false;
                }

                FmodResult.Ensure(
                    channel.isPlaying(out bool isPlaying),
                    "FMOD.Channel.isPlaying");

                return isPlaying;
            }
        }

        public void Start()
        {
            ThrowIfReleased();

            if (IsStarted)
            {
                throw new InvalidOperationException("The oscillator voice is already started.");
            }

            FmodResult.Ensure(
                coreSystem.playDSP(oscillator, default, true, out channel),
                "FMOD.System.playDSP(OSCILLATOR)");

            try
            {
                FmodResult.Ensure(
                    channel.setVolume(Gain),
                    "FMOD.Channel.setVolume");

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

        public void Stop()
        {
            if (!IsStarted)
            {
                return;
            }

            FmodResult.Ensure(
                channel.stop(),
                "FMOD.Channel.stop");

            channel.clearHandle();
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
