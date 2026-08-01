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
        public IEnumerator VoicePlaysA440ToneAndReleasesCleanly()
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
                    0.05f);

                Assert.That(voice.IsCreated, Is.True);
                Assert.That(voice.IsStarted, Is.False);

                voice.Start();

                Assert.That(voice.IsStarted, Is.True);
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.That(voice.IsPlaying, Is.True);

                voice.Stop();

                Assert.That(voice.IsStarted, Is.False);
                Assert.That(voice.IsPlaying, Is.False);

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
