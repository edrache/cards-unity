using System;
using FMODUnity;
using Loom.Core;
using Loom.Fmod;
using UnityEngine;

namespace Loom.Demo
{
    /// <summary>
    /// Runs the M3 drums, bass, lead, and pad oscillator spike in one fixed pump order.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoomFourTrackDemoController : MonoBehaviour
    {
        public const int TrackCount = 4;

        private readonly FmodOscillatorInstrument[] instruments =
            new FmodOscillatorInstrument[TrackCount];
        private readonly FmodTransportScheduler[] schedulers =
            new FmodTransportScheduler[TrackCount];

        private bool isShutdown = true;

        public bool IsRunning { get; private set; }

        public int ActiveSchedulerCount => IsRunning ? TrackCount : 0;

        private void OnEnable()
        {
            isShutdown = false;
            try
            {
                CreateTracks();
                for (int index = 0; index < schedulers.Length; index++)
                {
                    schedulers[index].Start();
                }

                IsRunning = true;
            }
            catch (Exception exception)
            {
                Shutdown();
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!IsRunning)
            {
                return;
            }

            try
            {
                for (int index = 0; index < schedulers.Length; index++)
                {
                    schedulers[index].Pump();
                }
            }
            catch (Exception exception)
            {
                Shutdown();
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            Shutdown();
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && IsRunning)
            {
                for (int index = 0; index < schedulers.Length; index++)
                {
                    schedulers[index].Panic();
                }
            }
        }

        public string GetBusPath(int trackIndex)
        {
            ValidateTrackIndex(trackIndex);
            return instruments[trackIndex]?.BusPath;
        }

        public long GetDispatchedEventCount(int trackIndex)
        {
            ValidateTrackIndex(trackIndex);
            return schedulers[trackIndex]?.DispatchedEventCount ?? 0L;
        }

        private void CreateTracks()
        {
            TempoMap tempoMap = TempoMap.Default;
            CreateTrack(
                0,
                FmodTrackBusPaths.Drums,
                CreateDrumPattern(),
                new FmodOscillatorSettings(
                    FmodOscillatorWaveform.Noise,
                    gain: 0.025f,
                    cutoffHz: 1800f),
                voiceCapacity: 4,
                tempoMap);
            CreateTrack(
                1,
                FmodTrackBusPaths.Bass,
                CreateBassPattern(),
                new FmodOscillatorSettings(
                    FmodOscillatorWaveform.Square,
                    gain: 0.025f,
                    octave: -1,
                    cutoffHz: 1200f),
                voiceCapacity: 4,
                tempoMap);
            CreateTrack(
                2,
                FmodTrackBusPaths.Lead,
                CreateLeadPattern(),
                new FmodOscillatorSettings(
                    FmodOscillatorWaveform.SawUp,
                    gain: 0.018f,
                    cutoffHz: 4800f),
                voiceCapacity: 6,
                tempoMap);
            CreateTrack(
                3,
                FmodTrackBusPaths.Pad,
                CreatePadPattern(),
                new FmodOscillatorSettings(
                    FmodOscillatorWaveform.Triangle,
                    gain: 0.012f,
                    cutoffHz: 2600f),
                voiceCapacity: 6,
                tempoMap);
        }

        private void CreateTrack(
            int index,
            string busPath,
            StepPattern pattern,
            FmodOscillatorSettings settings,
            int voiceCapacity,
            TempoMap tempoMap)
        {
            instruments[index] = new FmodOscillatorInstrument(
                RuntimeManager.CoreSystem,
                RuntimeManager.StudioSystem,
                new FmodAdsrEnvelope(0.005d, 0.04d, 0.55f, 0.08d),
                settings,
                voiceCapacity,
                busPath);
            schedulers[index] = new FmodTransportScheduler(
                RuntimeManager.CoreSystem,
                instruments[index],
                tempoMap,
                pattern,
                seed: 0x4C4F4F4DUL,
                streamId: (ulong)(index + 1));
        }

        private static StepPattern CreateDrumPattern()
        {
            var steps = new PatternStep[12];
            steps[0] = CreateStep(36, 110, 120L);
            steps[4] = CreateStep(38, 92, 120L);
            steps[8] = CreateStep(36, 105, 120L);
            steps[10] = CreateStep(42, 70, 80L);
            return new StepPattern(4, steps);
        }

        private static StepPattern CreateBassPattern()
        {
            var steps = new PatternStep[16];
            steps[0] = CreateStep(48, 96, 360L);
            steps[4] = CreateStep(48, 88, 360L);
            steps[8] = CreateStep(43, 94, 360L);
            steps[12] = CreateStep(46, 90, 360L);
            return new StepPattern(4, steps);
        }

        private static StepPattern CreateLeadPattern()
        {
            var steps = new PatternStep[20];
            steps[0] = CreateStep(60, 82, 180L);
            steps[3] = CreateStep(64, 76, 180L);
            steps[7] = CreateStep(67, 80, 240L);
            steps[11] = CreateStep(62, 74, 180L);
            steps[15] = CreateStep(69, 78, 300L);
            return new StepPattern(4, steps);
        }

        private static StepPattern CreatePadPattern()
        {
            var steps = new PatternStep[28];
            steps[0] = CreateStep(60, 68, 1680L);
            steps[8] = CreateStep(64, 62, 1680L);
            steps[16] = CreateStep(67, 64, 1680L);
            steps[24] = CreateStep(65, 60, 840L);
            return new StepPattern(4, steps);
        }

        private static PatternStep CreateStep(
            int midiNumber,
            int velocity,
            long durationTicks)
        {
            return new PatternStep(
                new Note(midiNumber),
                velocity,
                durationTicks);
        }

        private void Shutdown()
        {
            if (isShutdown)
            {
                return;
            }

            isShutdown = true;
            IsRunning = false;
            Exception cleanupException = null;
            for (int index = schedulers.Length - 1; index >= 0; index--)
            {
                cleanupException = DisposeAndCombine(
                    schedulers[index],
                    cleanupException);
                schedulers[index] = null;
            }

            for (int index = instruments.Length - 1; index >= 0; index--)
            {
                cleanupException = DisposeAndCombine(
                    instruments[index],
                    cleanupException);
                instruments[index] = null;
            }

            if (cleanupException != null)
            {
                Debug.LogException(cleanupException, this);
            }
        }

        private static Exception DisposeAndCombine(
            IDisposable disposable,
            Exception existingException)
        {
            if (disposable == null)
            {
                return existingException;
            }

            try
            {
                disposable.Dispose();
                return existingException;
            }
            catch (Exception exception)
            {
                return existingException == null
                    ? exception
                    : new AggregateException(existingException, exception);
            }
        }

        private static void ValidateTrackIndex(int trackIndex)
        {
            if (trackIndex < 0 || trackIndex >= TrackCount)
            {
                throw new ArgumentOutOfRangeException(nameof(trackIndex), trackIndex, null);
            }
        }
    }
}
