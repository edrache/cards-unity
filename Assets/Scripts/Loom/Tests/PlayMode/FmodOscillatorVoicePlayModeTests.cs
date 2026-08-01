using System.Collections;
using FMODUnity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Loom.Tests.PlayMode
{
    public sealed class FmodOscillatorVoicePlayModeTests
    {
        [UnityTest]
        public IEnumerator VoiceRunsAdsrAndAppliesLiveSynthControls()
        {
            var listenerObject = new GameObject("LOOM FMOD Test Listener")
            {
                hideFlags = HideFlags.DontSave
            };
            listenerObject.AddComponent<StudioListener>();

            Fmod.FmodOscillatorVoice voice = null;

            try
            {
                voice = new Fmod.FmodOscillatorVoice(
                    RuntimeManager.CoreSystem,
                    new Core.Note(Core.Note.ConcertAMidiNumber),
                    new Fmod.FmodAdsrEnvelope(0.08d, 0.06d, 0.4f, 0.1d),
                    new Fmod.FmodOscillatorSettings(
                        Fmod.FmodOscillatorWaveform.SawUp,
                        gain: 0.05f,
                        octave: 1,
                        cutoffHz: 12000f,
                        resonance: 1.5f));

                Assert.That(voice.IsCreated, Is.True);
                Assert.That(voice.IsOscillatorCreated, Is.True);
                Assert.That(voice.IsFilterCreated, Is.True);
                Assert.That(voice.IsStarted, Is.False);
                Assert.That(voice.Waveform, Is.EqualTo(Fmod.FmodOscillatorWaveform.SawUp));
                Assert.That(voice.FrequencyHz, Is.EqualTo(880f).Within(0.0001f));
                Assert.That(voice.CutoffHz, Is.EqualTo(12000f));
                Assert.That(voice.Resonance, Is.EqualTo(1.5f));

                voice.Start();

                Assert.That(voice.IsStarted, Is.True);
                Assert.That(voice.SampleRate, Is.GreaterThan(0));
                Assert.That(voice.ScheduledStartDspClock, Is.GreaterThan(0UL));

                yield return new WaitForSecondsRealtime(0.05f);
                Assert.That(voice.IsPlaying, Is.True);
                Assert.That(voice.CurrentEnvelopeLevel, Is.GreaterThan(0f));

                voice.SetWaveform(Fmod.FmodOscillatorWaveform.Square);
                voice.SetGain(0.03f);
                voice.SetOctave(-1);
                voice.SetCutoffHz(2400f);
                voice.SetResonance(3f);

                Assert.That(voice.Waveform, Is.EqualTo(Fmod.FmodOscillatorWaveform.Square));
                Assert.That(voice.Gain, Is.EqualTo(0.03f));
                Assert.That(voice.Octave, Is.EqualTo(-1));
                Assert.That(voice.FrequencyHz, Is.EqualTo(220f).Within(0.0001f));
                Assert.That(voice.CutoffHz, Is.EqualTo(2400f));
                Assert.That(voice.Resonance, Is.EqualTo(3f));
                Assert.That(voice.IsPlaying, Is.True);

                voice.BeginRelease();

                ulong expectedReleaseEnd =
                    voice.ReleaseStartDspClock + voice.EnvelopeSamples.ReleaseFrames;
                float expectedReleaseStartLevel =
                    voice.EnvelopeSamples.GetAttackDecaySustainLevel(
                        voice.ReleaseStartDspClock - voice.ScheduledStartDspClock);

                Assert.That(voice.IsReleasing, Is.True);
                Assert.That(voice.ReleaseEndDspClock, Is.EqualTo(expectedReleaseEnd));
                Assert.That(
                    voice.ReleaseStartLevel,
                    Is.EqualTo(expectedReleaseStartLevel).Within(0.0001f));

                voice.BeginRelease();

                yield return new WaitForSecondsRealtime(0.2f);

                Assert.That(voice.IsStarted, Is.False);
                Assert.That(voice.IsPlaying, Is.False);
                Assert.That(voice.IsReleaseComplete, Is.True);
                Assert.That(voice.CurrentEnvelopeLevel, Is.Zero);

                voice.Release();

                Assert.That(voice.IsCreated, Is.False);
                Assert.That(voice.IsOscillatorCreated, Is.False);
                Assert.That(voice.IsFilterCreated, Is.False);
                Assert.That(voice.IsStarted, Is.False);

                voice.Release();
            }
            finally
            {
                voice?.Dispose();
            }
        }
    }
}
