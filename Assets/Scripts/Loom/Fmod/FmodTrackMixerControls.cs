using System;

namespace Loom.Fmod
{
    /// <summary>
    /// Owns runtime gain, mute, and low-pass controls for one lifecycle-bound Studio bus.
    /// </summary>
    public sealed class FmodTrackMixerControls : IDisposable
    {
        public const float MinimumGain = 0f;
        public const float MaximumGain = 1f;
        public const float MinimumCutoffHz = 20f;
        public const float MaximumCutoffHz = 22000f;

        private readonly FMOD.System coreSystem;
        private readonly FmodStudioBusRouting busRouting;
        private FMOD.DSP lowPassFilter;
        private float originalGain;
        private bool originalMute;
        private bool isPreparedForBankReload;
        private bool isDisposed;

        public FmodTrackMixerControls(
            FMOD.System coreSystem,
            FMOD.Studio.System studioSystem,
            string busPath,
            float gain = MaximumGain,
            bool isMuted = false,
            float cutoffHz = MaximumCutoffHz)
        {
            ValidateBusPath(busPath);
            ValidateGain(gain);
            ValidateCutoffHz(cutoffHz);
            if (!coreSystem.hasHandle())
            {
                throw new ArgumentException(
                    "FMOD Core system must have a valid handle.",
                    nameof(coreSystem));
            }

            if (!studioSystem.isValid())
            {
                throw new ArgumentException(
                    "FMOD Studio system must have a valid handle.",
                    nameof(studioSystem));
            }

            this.coreSystem = coreSystem;
            busRouting = new FmodStudioBusRouting(studioSystem, busPath);
            Gain = gain;
            IsMuted = isMuted;
            CutoffHz = cutoffHz;

            try
            {
                AcquireRuntimeControls();
            }
            catch
            {
                busRouting.Dispose();
                throw;
            }
        }

        public string BusPath => busRouting.BusPath;

        public float Gain { get; private set; }

        public bool IsMuted { get; private set; }

        public float CutoffHz { get; private set; }

        public void SetGain(float gain)
        {
            ThrowIfUnavailable();
            ValidateGain(gain);
            EnsureBusResult(
                busRouting.GetBus().setVolume(gain),
                "FMOD.Studio.Bus.setVolume");
            Gain = gain;
        }

        public void SetMuted(bool isMuted)
        {
            ThrowIfUnavailable();
            EnsureBusResult(
                busRouting.GetBus().setMute(isMuted),
                "FMOD.Studio.Bus.setMute");
            IsMuted = isMuted;
        }

        public void SetCutoffHz(float cutoffHz)
        {
            ThrowIfUnavailable();
            ValidateCutoffHz(cutoffHz);
            FmodResult.Ensure(
                lowPassFilter.setParameterFloat(
                    (int)FMOD.DSP_MULTIBAND_EQ.A_FREQUENCY,
                    cutoffHz),
                "FMOD.DSP.setParameterFloat(track low-pass cutoff)");
            CutoffHz = cutoffHz;
        }

        public void PrepareForBankReload()
        {
            ThrowIfDisposed();
            if (isPreparedForBankReload)
            {
                throw new InvalidOperationException(
                    "Track mixer controls are already prepared for bank reload.");
            }

            RestoreOriginalBusState();
            ReleaseLowPassFilter();
            busRouting.PrepareForBankReload();
            isPreparedForBankReload = true;
        }

        public void RestoreAfterBankReload()
        {
            ThrowIfDisposed();
            if (!isPreparedForBankReload)
            {
                throw new InvalidOperationException(
                    "Track mixer controls must be prepared before bank reload restoration.");
            }

            busRouting.RestoreAfterBankReload();
            try
            {
                AcquireRuntimeControls();
                isPreparedForBankReload = false;
            }
            catch
            {
                busRouting.PrepareForBankReload();
                throw;
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            Exception cleanupException = null;
            if (!isPreparedForBankReload)
            {
                cleanupException = TryCleanupRuntimeControls();
            }

            try
            {
                busRouting.Dispose();
            }
            catch (Exception exception)
            {
                cleanupException = cleanupException == null
                    ? exception
                    : new AggregateException(cleanupException, exception);
            }

            isDisposed = true;
            if (cleanupException != null)
            {
                throw cleanupException;
            }
        }

        private void AcquireRuntimeControls()
        {
            FMOD.Studio.Bus bus = busRouting.GetBus();
            EnsureBusResult(
                bus.getVolume(out originalGain),
                "FMOD.Studio.Bus.getVolume");
            EnsureBusResult(
                bus.getMute(out originalMute),
                "FMOD.Studio.Bus.getMute");

            CreateLowPassFilter();
            try
            {
                EnsureBusResult(bus.setVolume(Gain), "FMOD.Studio.Bus.setVolume");
                EnsureBusResult(bus.setMute(IsMuted), "FMOD.Studio.Bus.setMute");
            }
            catch
            {
                try
                {
                    RestoreOriginalBusState();
                }
                finally
                {
                    ReleaseLowPassFilter();
                }

                throw;
            }
        }

        private void CreateLowPassFilter()
        {
            FmodResult.Ensure(
                coreSystem.createDSPByType(FMOD.DSP_TYPE.MULTIBAND_EQ, out lowPassFilter),
                "FMOD.System.createDSPByType(track MULTIBAND_EQ)");
            try
            {
                FmodResult.Ensure(
                    lowPassFilter.setParameterInt(
                        (int)FMOD.DSP_MULTIBAND_EQ.A_FILTER,
                        (int)FMOD.DSP_MULTIBAND_EQ_FILTER_TYPE.LOWPASS_24DB),
                    "FMOD.DSP.setParameterInt(track low-pass type)");
                FmodResult.Ensure(
                    lowPassFilter.setParameterFloat(
                        (int)FMOD.DSP_MULTIBAND_EQ.A_FREQUENCY,
                        CutoffHz),
                    "FMOD.DSP.setParameterFloat(track low-pass cutoff)");
                FmodResult.Ensure(
                    busRouting.GetChannelGroup().addDSP(
                        FMOD.CHANNELCONTROL_DSP_INDEX.TAIL,
                        lowPassFilter),
                    "FMOD.ChannelGroup.addDSP(track low-pass)");
            }
            catch
            {
                lowPassFilter.release();
                lowPassFilter.clearHandle();
                throw;
            }
        }

        private void RestoreOriginalBusState()
        {
            FMOD.Studio.Bus bus = busRouting.GetBus();
            EnsureBusResult(
                bus.setVolume(originalGain),
                "FMOD.Studio.Bus.setVolume(restore original)");
            EnsureBusResult(
                bus.setMute(originalMute),
                "FMOD.Studio.Bus.setMute(restore original)");
        }

        private void ReleaseLowPassFilter()
        {
            if (!lowPassFilter.hasHandle())
            {
                return;
            }

            FmodResult.Ensure(
                busRouting.GetChannelGroup().removeDSP(lowPassFilter),
                "FMOD.ChannelGroup.removeDSP(track low-pass)");
            FmodResult.Ensure(
                lowPassFilter.release(),
                "FMOD.DSP.release(track low-pass)");
            lowPassFilter.clearHandle();
        }

        private Exception TryCleanupRuntimeControls()
        {
            Exception cleanupException = null;
            try
            {
                RestoreOriginalBusState();
            }
            catch (Exception exception)
            {
                cleanupException = exception;
            }

            try
            {
                ReleaseLowPassFilter();
            }
            catch (Exception exception)
            {
                cleanupException = cleanupException == null
                    ? exception
                    : new AggregateException(cleanupException, exception);
            }

            return cleanupException;
        }

        private void EnsureBusResult(FMOD.RESULT result, string operation)
        {
            if (result != FMOD.RESULT.OK)
            {
                throw new FmodOperationException(
                    $"{operation} for '{BusPath}'",
                    result);
            }
        }

        private static void ValidateBusPath(string busPath)
        {
            if (string.IsNullOrWhiteSpace(busPath))
            {
                throw new ArgumentException(
                    "FMOD Studio bus path must not be empty.",
                    nameof(busPath));
            }
        }

        private static void ValidateGain(float gain)
        {
            ValidateFiniteRange(
                gain,
                MinimumGain,
                MaximumGain,
                nameof(gain),
                "Gain");
        }

        private static void ValidateCutoffHz(float cutoffHz)
        {
            ValidateFiniteRange(
                cutoffHz,
                MinimumCutoffHz,
                MaximumCutoffHz,
                nameof(cutoffHz),
                "Cutoff frequency");
        }

        private static void ValidateFiniteRange(
            float value,
            float minimum,
            float maximum,
            string parameterName,
            string displayName)
        {
            if (float.IsNaN(value)
                || float.IsInfinity(value)
                || value < minimum
                || value > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    $"{displayName} must be finite and between {minimum} and {maximum}.");
            }
        }

        private void ThrowIfUnavailable()
        {
            ThrowIfDisposed();
            if (isPreparedForBankReload)
            {
                throw new InvalidOperationException(
                    "Track mixer controls are unavailable during bank reload.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(FmodTrackMixerControls));
            }
        }
    }
}
