using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class FmodOscillatorVoiceContractTests
    {
        [Test]
        public void ConstructorRejectsAnInvalidCoreSystemBeforeCallingFmod()
        {
            Assert.Throws<ArgumentException>(
                () => new Fmod.FmodOscillatorVoice(
                    default,
                    new Core.Note(Core.Note.ConcertAMidiNumber)));
        }

        [Test]
        public void ConstructorRejectsANullEnvelopeBeforeCallingFmod()
        {
            Assert.Throws<ArgumentNullException>(
                () => new Fmod.FmodOscillatorVoice(
                    default,
                    new Core.Note(Core.Note.ConcertAMidiNumber),
                    null));
        }

        [Test]
        public void ConstructorRejectsNullOscillatorSettingsBeforeCallingFmod()
        {
            Assert.Throws<ArgumentNullException>(
                () => new Fmod.FmodOscillatorVoice(
                    default,
                    new Core.Note(Core.Note.ConcertAMidiNumber),
                    Fmod.FmodAdsrEnvelope.Default,
                    (Fmod.FmodOscillatorSettings)null));
        }

        [TestCase(-0.01f)]
        [TestCase(1.01f)]
        public void ConstructorRejectsGainOutsideNormalizedRange(float gain)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodOscillatorVoice(
                    default,
                    new Core.Note(Core.Note.ConcertAMidiNumber),
                    gain));
        }

        [Test]
        public void ConstructorRejectsNonFiniteGain()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodOscillatorVoice(
                    default,
                    new Core.Note(Core.Note.ConcertAMidiNumber),
                    float.NaN));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodOscillatorVoice(
                    default,
                    new Core.Note(Core.Note.ConcertAMidiNumber),
                    float.PositiveInfinity));
        }

        [Test]
        public void FmodExceptionPreservesOperationAndResultContext()
        {
            var exception = new Fmod.FmodOperationException(
                "FMOD test operation",
                FMOD.RESULT.ERR_INVALID_HANDLE);

            Assert.That(exception.Operation, Is.EqualTo("FMOD test operation"));
            Assert.That(exception.Result, Is.EqualTo(FMOD.RESULT.ERR_INVALID_HANDLE));
            Assert.That(exception.Message, Does.Contain("FMOD test operation"));
            Assert.That(exception.Message, Does.Contain("ERR_INVALID_HANDLE"));
        }

        [Test]
        public void AdsrEnvelopeRejectsInvalidDurationsAndLevels()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodAdsrEnvelope(0d, 0.1d, 0.5f, 0.2d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodAdsrEnvelope(0.1d, -0.1d, 0.5f, 0.2d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodAdsrEnvelope(0.1d, 0.1d, -0.01f, 0.2d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodAdsrEnvelope(0.1d, 0.1d, 1.01f, 0.2d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodAdsrEnvelope(0.1d, 0.1d, 0.5f, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodAdsrEnvelope(double.NaN, 0.1d, 0.5f, 0.2d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodAdsrEnvelope(0.1d, 0.1d, float.PositiveInfinity, 0.2d));
        }

        [Test]
        public void AdsrEnvelopeResolvesDurationsUsingTheRuntimeSampleRate()
        {
            var envelope = new Fmod.FmodAdsrEnvelope(0.01d, 0.1d, 0.75f, 0.2d);

            Fmod.FmodAdsrEnvelopeSamples samples = envelope.ResolveSampleFrames(48000);

            Assert.That(samples.AttackFrames, Is.EqualTo(480UL));
            Assert.That(samples.DecayFrames, Is.EqualTo(4800UL));
            Assert.That(samples.SustainLevel, Is.EqualTo(0.75f));
            Assert.That(samples.ReleaseFrames, Is.EqualTo(9600UL));
        }

        [Test]
        public void AdsrEnvelopeKeepsPositiveSubSampleDurationsAtOneFrame()
        {
            var envelope = new Fmod.FmodAdsrEnvelope(0.000001d, 0d, 0.5f, 0.000001d);

            Fmod.FmodAdsrEnvelopeSamples samples = envelope.ResolveSampleFrames(48000);

            Assert.That(samples.AttackFrames, Is.EqualTo(1UL));
            Assert.That(samples.DecayFrames, Is.Zero);
            Assert.That(samples.ReleaseFrames, Is.EqualTo(1UL));
        }

        [Test]
        public void AdsrEnvelopeCalculatesAttackDecayAndSustainLevels()
        {
            var envelope = new Fmod.FmodAdsrEnvelope(0.1d, 0.1d, 0.25f, 0.2d);
            Fmod.FmodAdsrEnvelopeSamples samples = envelope.ResolveSampleFrames(48000);

            Assert.That(samples.GetAttackDecaySustainLevel(0UL), Is.EqualTo(0f));
            Assert.That(samples.GetAttackDecaySustainLevel(2400UL), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(samples.GetAttackDecaySustainLevel(4800UL), Is.EqualTo(1f));
            Assert.That(samples.GetAttackDecaySustainLevel(7200UL), Is.EqualTo(0.625f).Within(0.0001f));
            Assert.That(samples.GetAttackDecaySustainLevel(9600UL), Is.EqualTo(0.25f));
            Assert.That(samples.GetAttackDecaySustainLevel(48000UL), Is.EqualTo(0.25f));
        }

        [Test]
        public void ZeroDecayRampsDirectlyToSustainWithoutADiscontinuity()
        {
            var envelope = new Fmod.FmodAdsrEnvelope(0.1d, 0d, 0.75f, 0.2d);
            Fmod.FmodAdsrEnvelopeSamples samples = envelope.ResolveSampleFrames(48000);

            Assert.That(samples.GetAttackDecaySustainLevel(2400UL), Is.EqualTo(0.375f).Within(0.0001f));
            Assert.That(samples.GetAttackDecaySustainLevel(4800UL), Is.EqualTo(0.75f));
        }

        [TestCase(Fmod.FmodOscillatorWaveform.Sine, 0)]
        [TestCase(Fmod.FmodOscillatorWaveform.Square, 1)]
        [TestCase(Fmod.FmodOscillatorWaveform.SawUp, 2)]
        [TestCase(Fmod.FmodOscillatorWaveform.SawDown, 3)]
        [TestCase(Fmod.FmodOscillatorWaveform.Triangle, 4)]
        [TestCase(Fmod.FmodOscillatorWaveform.Noise, 5)]
        public void WaveformValuesMatchTheFmodOscillatorContract(
            Fmod.FmodOscillatorWaveform waveform,
            int nativeValue)
        {
            Assert.That((int)waveform, Is.EqualTo(nativeValue));
        }

        [Test]
        public void OscillatorSettingsExposeSafeDefaults()
        {
            Fmod.FmodOscillatorSettings settings = Fmod.FmodOscillatorSettings.Default;

            Assert.That(settings.Waveform, Is.EqualTo(Fmod.FmodOscillatorWaveform.Sine));
            Assert.That(settings.Gain, Is.EqualTo(0.1f));
            Assert.That(settings.Octave, Is.Zero);
            Assert.That(settings.CutoffHz, Is.EqualTo(22000f));
            Assert.That(settings.Resonance, Is.EqualTo(0.707f));
        }

        [Test]
        public void OscillatorSettingsRejectUnsupportedControlValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodOscillatorSettings(
                    waveform: (Fmod.FmodOscillatorWaveform)(-1)));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodOscillatorSettings(
                    waveform: (Fmod.FmodOscillatorWaveform)6));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodOscillatorSettings(gain: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodOscillatorSettings(octave: -5));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodOscillatorSettings(octave: 5));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodOscillatorSettings(cutoffHz: 19.9f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodOscillatorSettings(cutoffHz: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodOscillatorSettings(resonance: 0.09f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodOscillatorSettings(resonance: 10.01f));
        }

        [Test]
        public void OscillatorSettingsResolveOctaveTranspositionWithinFmodRange()
        {
            var note = new Core.Note(Core.Note.ConcertAMidiNumber);

            Assert.That(
                Fmod.FmodOscillatorSettings.ResolveFrequencyHz(note, -2),
                Is.EqualTo(110f).Within(0.0001f));
            Assert.That(
                Fmod.FmodOscillatorSettings.ResolveFrequencyHz(note, 4),
                Is.EqualTo(7040f).Within(0.0001f));
        }

        [Test]
        public void OscillatorSettingsRejectTranspositionOutsideFmodFrequencyRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Fmod.FmodOscillatorSettings.ResolveFrequencyHz(
                    new Core.Note(Core.Note.MinMidiNumber),
                    -4));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Fmod.FmodOscillatorSettings.ResolveFrequencyHz(
                    new Core.Note(Core.Note.MaxMidiNumber),
                    1));
        }
    }
}
