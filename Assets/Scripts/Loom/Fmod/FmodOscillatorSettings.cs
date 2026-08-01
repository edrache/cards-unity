using System;
using Loom.Core;

namespace Loom.Fmod
{
    public enum FmodOscillatorWaveform
    {
        Sine = 0,
        Square = 1,
        SawUp = 2,
        SawDown = 3,
        Triangle = 4,
        Noise = 5
    }

    public sealed class FmodOscillatorSettings
    {
        public const float MinimumGain = 0f;
        public const float MaximumGain = 1f;
        public const int MinimumOctave = -4;
        public const int MaximumOctave = 4;
        public const float MinimumFrequencyHz = 1f;
        public const float MaximumFrequencyHz = 22000f;
        public const float MinimumCutoffHz = 20f;
        public const float MaximumCutoffHz = 22000f;
        public const float MinimumResonance = 0.1f;
        public const float MaximumResonance = 10f;

        public static FmodOscillatorSettings Default { get; } =
            new FmodOscillatorSettings();

        public FmodOscillatorSettings(
            FmodOscillatorWaveform waveform = FmodOscillatorWaveform.Sine,
            float gain = 0.1f,
            int octave = 0,
            float cutoffHz = MaximumCutoffHz,
            float resonance = 0.707f)
        {
            ValidateWaveform(waveform);
            ValidateGain(gain);
            ValidateOctave(octave);
            ValidateCutoffHz(cutoffHz);
            ValidateResonance(resonance);

            Waveform = waveform;
            Gain = gain;
            Octave = octave;
            CutoffHz = cutoffHz;
            Resonance = resonance;
        }

        public FmodOscillatorWaveform Waveform { get; }

        public float Gain { get; }

        public int Octave { get; }

        public float CutoffHz { get; }

        public float Resonance { get; }

        public float ResolveFrequencyHz(Note note)
        {
            return ResolveFrequencyHz(note, Octave);
        }

        public static float ResolveFrequencyHz(Note note, int octave)
        {
            ValidateOctave(octave);

            double frequencyHz = note.FrequencyHz * Math.Pow(2d, octave);
            if (frequencyHz < MinimumFrequencyHz || frequencyHz > MaximumFrequencyHz)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(octave),
                    octave,
                    $"The transposed oscillator frequency must be between {MinimumFrequencyHz} and {MaximumFrequencyHz} Hz.");
            }

            return (float)frequencyHz;
        }

        internal static void ValidateWaveform(FmodOscillatorWaveform waveform)
        {
            int nativeValue = (int)waveform;
            if (nativeValue < (int)FmodOscillatorWaveform.Sine ||
                nativeValue > (int)FmodOscillatorWaveform.Noise)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(waveform),
                    waveform,
                    "Waveform must be a supported FMOD oscillator type.");
            }
        }

        internal static void ValidateGain(float gain)
        {
            ValidateFiniteRange(
                gain,
                MinimumGain,
                MaximumGain,
                nameof(gain),
                "Gain");
        }

        internal static void ValidateOctave(int octave)
        {
            if (octave < MinimumOctave || octave > MaximumOctave)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(octave),
                    octave,
                    $"Octave must be between {MinimumOctave} and {MaximumOctave}.");
            }
        }

        internal static void ValidateCutoffHz(float cutoffHz)
        {
            ValidateFiniteRange(
                cutoffHz,
                MinimumCutoffHz,
                MaximumCutoffHz,
                nameof(cutoffHz),
                "Cutoff frequency");
        }

        internal static void ValidateResonance(float resonance)
        {
            ValidateFiniteRange(
                resonance,
                MinimumResonance,
                MaximumResonance,
                nameof(resonance),
                "Resonance");
        }

        private static void ValidateFiniteRange(
            float value,
            float minimum,
            float maximum,
            string parameterName,
            string displayName)
        {
            if (float.IsNaN(value) ||
                float.IsInfinity(value) ||
                value < minimum ||
                value > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    $"{displayName} must be finite and between {minimum} and {maximum}.");
            }
        }
    }
}
