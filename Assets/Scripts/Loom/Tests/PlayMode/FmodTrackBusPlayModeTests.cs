using System.Collections;
using FMODUnity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Loom.Tests.PlayMode
{
    public sealed class FmodTrackBusPlayModeTests
    {
        private static readonly string[] RequiredBusPaths =
        {
            Fmod.FmodTrackBusPaths.Drums,
            Fmod.FmodTrackBusPaths.Bass,
            Fmod.FmodTrackBusPaths.Lead,
            Fmod.FmodTrackBusPaths.Pad,
            Fmod.FmodTrackBusPaths.Fx
        };

        [UnityTest]
        public IEnumerator RequiredTrackBusesResolveToValidChannelGroups()
        {
            foreach (string busPath in RequiredBusPaths)
            {
                FMOD.Studio.Bus bus = default;
                bool isLocked = false;

                try
                {
                    Assert.That(
                        RuntimeManager.StudioSystem.getBus(busPath, out bus),
                        Is.EqualTo(FMOD.RESULT.OK),
                        busPath);
                    Assert.That(bus.isValid(), Is.True, busPath);
                    Assert.That(
                        bus.lockChannelGroup(),
                        Is.EqualTo(FMOD.RESULT.OK),
                        busPath);
                    isLocked = true;
                    Assert.That(
                        RuntimeManager.StudioSystem.flushCommands(),
                        Is.EqualTo(FMOD.RESULT.OK),
                        busPath);
                    Assert.That(
                        bus.getChannelGroup(out FMOD.ChannelGroup channelGroup),
                        Is.EqualTo(FMOD.RESULT.OK),
                        busPath);
                    Assert.That(channelGroup.hasHandle(), Is.True, busPath);
                    Assert.That(
                        channelGroup.getNumChannels(out _),
                        Is.EqualTo(FMOD.RESULT.OK),
                        busPath);
                }
                finally
                {
                    if (isLocked && bus.isValid())
                    {
                        Assert.That(
                            bus.unlockChannelGroup(),
                            Is.EqualTo(FMOD.RESULT.OK),
                            busPath);
                        Assert.That(
                            RuntimeManager.StudioSystem.flushCommands(),
                            Is.EqualTo(FMOD.RESULT.OK),
                            busPath);
                    }
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator OscillatorInstrumentBindsToDeclaredTrackBus()
        {
            var listenerObject = new GameObject("LOOM Track Bus Test Listener")
            {
                hideFlags = HideFlags.DontSave
            };
            listenerObject.AddComponent<StudioListener>();
            Fmod.FmodOscillatorInstrument instrument = null;

            try
            {
                instrument = new Fmod.FmodOscillatorInstrument(
                    RuntimeManager.CoreSystem,
                    RuntimeManager.StudioSystem,
                    Fmod.FmodAdsrEnvelope.Default,
                    new Fmod.FmodOscillatorSettings(gain: 0.01f),
                    voiceCapacity: 1,
                    busPath: Fmod.FmodTrackBusPaths.Lead);

                Assert.That(instrument.BusPath, Is.EqualTo(Fmod.FmodTrackBusPaths.Lead));
                Core.VoiceHandle handle = instrument.NoteOn(new Core.Note(60), 80);
                Assert.That(handle.IsValid, Is.True);
                Assert.That(instrument.ActiveVoiceCount, Is.EqualTo(1));

                yield return new WaitForSecondsRealtime(0.03f);

                instrument.NoteOff(handle);
                Assert.That(instrument.OwnedVoiceCount, Is.Zero);
            }
            finally
            {
                instrument?.Dispose();
                Object.Destroy(listenerObject);
            }
        }
    }
}
