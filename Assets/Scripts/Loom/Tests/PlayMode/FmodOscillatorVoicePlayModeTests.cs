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
        public IEnumerator VoiceRunsAdsrAndStopsAtTheScheduledReleaseEnd()
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
                    0.05f);

                Assert.That(voice.IsCreated, Is.True);
                Assert.That(voice.IsStarted, Is.False);

                voice.Start();

                Assert.That(voice.IsStarted, Is.True);
                Assert.That(voice.SampleRate, Is.GreaterThan(0));
                Assert.That(voice.ScheduledStartDspClock, Is.GreaterThan(0UL));

                yield return new WaitForSecondsRealtime(0.05f);
                Assert.That(voice.IsPlaying, Is.True);
                Assert.That(voice.CurrentEnvelopeLevel, Is.GreaterThan(0f));

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
