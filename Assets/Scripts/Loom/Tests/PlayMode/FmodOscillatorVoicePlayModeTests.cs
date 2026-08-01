using System.Collections;
using FMODUnity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
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

        [UnityTest]
        public IEnumerator InstrumentPlaysEightVoicesAndCleansStolenAndReleasedResources()
        {
            var listenerObject = new GameObject("LOOM Polyphony Test Listener")
            {
                hideFlags = HideFlags.DontSave
            };
            listenerObject.AddComponent<StudioListener>();

            Fmod.FmodOscillatorInstrument instrument = null;
            FMOD.Studio.Bus synthBus = default;
            bool restoreMute = false;
            bool originalMute = false;

            try
            {
                instrument = new Fmod.FmodOscillatorInstrument(
                    RuntimeManager.CoreSystem,
                    RuntimeManager.StudioSystem,
                    new Fmod.FmodAdsrEnvelope(0.01d, 0.02d, 0.5f, 0.05d),
                    new Fmod.FmodOscillatorSettings(gain: 0.01f));
                var handles = new Core.VoiceHandle[
                    Fmod.FmodOscillatorInstrument.DefaultVoiceCapacity];

                for (int i = 0; i < handles.Length; i++)
                {
                    handles[i] = instrument.NoteOn(new Core.Note(60 + i), 100);
                }

                Assert.That(instrument.VoiceCapacity, Is.EqualTo(8));
                Assert.That(instrument.CreatedVoiceCount, Is.EqualTo(8));
                Assert.That(instrument.ActiveVoiceCount, Is.EqualTo(8));
                Assert.That(instrument.OwnedVoiceCount, Is.EqualTo(8));
                Assert.That(handles, Is.Unique);

                var updatedEnvelope = new Fmod.FmodAdsrEnvelope(
                    0.02d,
                    0.04d,
                    0.65f,
                    0.08d);
                instrument.SetEnvelope(updatedEnvelope);
                instrument.SetWaveform(Fmod.FmodOscillatorWaveform.Triangle);
                instrument.SetGain(0.02f);
                instrument.SetOctave(-1);
                instrument.SetCutoffHz(2400f);
                instrument.SetResonance(1.25f);

                Assert.That(instrument.Envelope, Is.SameAs(updatedEnvelope));
                Assert.That(
                    instrument.Waveform,
                    Is.EqualTo(Fmod.FmodOscillatorWaveform.Triangle));
                Assert.That(instrument.Gain, Is.EqualTo(0.02f));
                Assert.That(instrument.Octave, Is.EqualTo(-1));
                Assert.That(instrument.CutoffHz, Is.EqualTo(2400f));
                Assert.That(instrument.Resonance, Is.EqualTo(1.25f));

                Assert.That(
                    RuntimeManager.StudioSystem.getBus(
                        Fmod.FmodOscillatorInstrument.SynthBusPath,
                        out synthBus),
                    Is.EqualTo(FMOD.RESULT.OK));
                Assert.That(synthBus.isValid(), Is.True);
                Assert.That(
                    synthBus.getChannelGroup(out FMOD.ChannelGroup synthGroup),
                    Is.EqualTo(FMOD.RESULT.OK));
                Assert.That(synthGroup.hasHandle(), Is.True);
                Assert.That(
                    synthGroup.getNumChannels(out int routedChannelCount),
                    Is.EqualTo(FMOD.RESULT.OK));
                Assert.That(routedChannelCount, Is.GreaterThanOrEqualTo(8));
                Assert.That(
                    synthBus.getMute(out originalMute),
                    Is.EqualTo(FMOD.RESULT.OK));
                restoreMute = true;
                Assert.That(
                    synthBus.setMute(true),
                    Is.EqualTo(FMOD.RESULT.OK));
                Assert.That(
                    synthBus.getMute(out bool isMuted),
                    Is.EqualTo(FMOD.RESULT.OK));
                Assert.That(isMuted, Is.True);

                yield return new WaitForSecondsRealtime(0.05f);

                Assert.That(instrument.ActiveVoiceCount, Is.EqualTo(8));
                Assert.That(
                    synthGroup.getAudibility(out float mutedAudibility),
                    Is.EqualTo(FMOD.RESULT.OK));
                Assert.That(mutedAudibility, Is.Zero.Within(0.0001f));
                Assert.That(
                    synthBus.setMute(originalMute),
                    Is.EqualTo(FMOD.RESULT.OK));
                restoreMute = false;

                Core.VoiceHandle ninth = instrument.NoteOn(new Core.Note(72), 100);

                Assert.That(instrument.CreatedVoiceCount, Is.EqualTo(8));
                Assert.That(instrument.ActiveVoiceCount, Is.EqualTo(8));
                Assert.That(instrument.OwnedVoiceCount, Is.EqualTo(8));
                Assert.That(instrument.NoteOff(handles[0]), Is.False);
                Assert.That(instrument.NoteOff(handles[1]), Is.True);
                Assert.That(instrument.NoteOff(ninth), Is.True);

                instrument.AllNotesOff();
                Assert.That(instrument.OwnedVoiceCount, Is.Zero);

                yield return new WaitForSecondsRealtime(0.15f);

                Assert.That(instrument.ActiveVoiceCount, Is.Zero);
                Assert.That(instrument.CreatedVoiceCount, Is.EqualTo(8));
                instrument.Dispose();
                Assert.That(instrument.CreatedVoiceCount, Is.Zero);
            }
            finally
            {
                if (restoreMute && synthBus.isValid())
                {
                    synthBus.setMute(originalMute);
                }

                instrument?.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator InstrumentReacquiresSynthBusAfterMasterBankReload()
        {
            var listenerObject = new GameObject("LOOM Bank Reload Test Listener")
            {
                hideFlags = HideFlags.DontSave
            };
            listenerObject.AddComponent<StudioListener>();

            Fmod.FmodOscillatorInstrument instrument = null;
            const string MasterBankName = "Master";

            try
            {
                Assert.That(RuntimeManager.HasBankLoaded(MasterBankName), Is.True);
                instrument = new Fmod.FmodOscillatorInstrument(
                    RuntimeManager.CoreSystem,
                    RuntimeManager.StudioSystem,
                    new Fmod.FmodAdsrEnvelope(0.01d, 0.01d, 0.5f, 0.03d),
                    new Fmod.FmodOscillatorSettings(gain: 0.01f),
                    voiceCapacity: 1);

                Core.VoiceHandle first = instrument.NoteOn(
                    new Core.Note(60),
                    100);
                yield return new WaitForSecondsRealtime(0.04f);
                Assert.Throws<System.InvalidOperationException>(
                    () => instrument.PrepareForBankReload());
                Assert.That(instrument.NoteOff(first), Is.True);
                yield return new WaitForSecondsRealtime(0.1f);
                Assert.That(instrument.ActiveVoiceCount, Is.Zero);

                Assert.That(
                    RuntimeManager.StudioSystem.getBus(
                        Fmod.FmodOscillatorInstrument.SynthBusPath,
                        out FMOD.Studio.Bus busBeforeReload),
                    Is.EqualTo(FMOD.RESULT.OK));
                Assert.That(busBeforeReload.isValid(), Is.True);

                instrument.PrepareForBankReload();
                RuntimeManager.UnloadBank(MasterBankName);
                yield return null;

                Assert.That(RuntimeManager.HasBankLoaded(MasterBankName), Is.False);

                RuntimeManager.LoadBank(MasterBankName);
                yield return null;

                Assert.That(RuntimeManager.HasBankLoaded(MasterBankName), Is.True);
                instrument.RestoreAfterBankReload();
                Assert.That(
                    RuntimeManager.StudioSystem.getBus(
                        Fmod.FmodOscillatorInstrument.SynthBusPath,
                        out FMOD.Studio.Bus busAfterReload),
                    Is.EqualTo(FMOD.RESULT.OK));
                Assert.That(busAfterReload.isValid(), Is.True);

                Core.VoiceHandle second = instrument.NoteOn(
                    new Core.Note(67),
                    100);
                yield return new WaitForSecondsRealtime(0.04f);

                Assert.That(instrument.ActiveVoiceCount, Is.EqualTo(1));
                Assert.That(
                    busAfterReload.getChannelGroup(
                        out FMOD.ChannelGroup synthGroup),
                    Is.EqualTo(FMOD.RESULT.OK));
                Assert.That(
                    synthGroup.getNumChannels(out int routedChannelCount),
                    Is.EqualTo(FMOD.RESULT.OK));
                Assert.That(routedChannelCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(instrument.NoteOff(second), Is.True);

                yield return new WaitForSecondsRealtime(0.1f);

                Assert.That(instrument.ActiveVoiceCount, Is.Zero);
            }
            finally
            {
                instrument?.Dispose();
                if (!RuntimeManager.HasBankLoaded(MasterBankName))
                {
                    RuntimeManager.LoadBank(MasterBankName);
                }
            }
        }

        [UnityTest]
        public IEnumerator DemoPanicsAndShutsDownIdempotently()
        {
            yield return SceneManager.LoadSceneAsync(
                "scn_loom",
                LoadSceneMode.Single);
            yield return null;

            Demo.LoomSynthDemoController controller =
                Object.FindFirstObjectByType<Demo.LoomSynthDemoController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Instrument, Is.Not.Null);

            Fmod.FmodOscillatorInstrument focusInstrument =
                controller.Instrument;
            focusInstrument.NoteOn(new Core.Note(60), 100);
            Assert.That(focusInstrument.OwnedVoiceCount, Is.EqualTo(1));

            controller.SendMessage(
                "OnApplicationFocus",
                false,
                SendMessageOptions.RequireReceiver);

            Assert.That(focusInstrument.OwnedVoiceCount, Is.Zero);
            Assert.That(controller.Instrument, Is.SameAs(focusInstrument));

            focusInstrument.NoteOn(new Core.Note(64), 100);
            controller.SendMessage(
                "OnApplicationPause",
                true,
                SendMessageOptions.RequireReceiver);
            Assert.That(focusInstrument.OwnedVoiceCount, Is.Zero);

            focusInstrument.NoteOn(new Core.Note(67), 100);
            controller.enabled = false;

            Assert.That(controller.Instrument, Is.Null);
            Assert.That(focusInstrument.CreatedVoiceCount, Is.Zero);

            controller.enabled = false;
            controller.enabled = true;
            yield return null;

            Fmod.FmodOscillatorInstrument reloadInstrument =
                controller.Instrument;
            Assert.That(reloadInstrument, Is.Not.Null);
            reloadInstrument.NoteOn(new Core.Note(69), 100);

            controller.SendMessage(
                "HandleBeforeAssemblyReload",
                SendMessageOptions.RequireReceiver);

            Assert.That(controller.Instrument, Is.Null);
            Assert.That(reloadInstrument.CreatedVoiceCount, Is.Zero);

            controller.enabled = false;
            controller.enabled = true;
            yield return null;

            Fmod.FmodOscillatorInstrument quitInstrument =
                controller.Instrument;
            Assert.That(quitInstrument, Is.Not.Null);
            quitInstrument.NoteOn(new Core.Note(72), 100);

            controller.SendMessage(
                "OnApplicationQuit",
                SendMessageOptions.RequireReceiver);

            Assert.That(controller.Instrument, Is.Null);
            Assert.That(quitInstrument.CreatedVoiceCount, Is.Zero);

            Object.Destroy(controller.gameObject);
            yield return null;
        }
    }
}
