using System.Collections;
using FMODUnity;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Loom.Tests.PlayMode
{
    public sealed class FmodTrackMixerControlsPlayModeTests
    {
        [UnityTest]
        public IEnumerator ControlsApplyAndRestoreBusStateAndOwnedLowPass()
        {
            const string BusPath = Fmod.FmodOscillatorInstrument.SynthBusPath;
            Assert.That(
                RuntimeManager.StudioSystem.getBus(BusPath, out FMOD.Studio.Bus bus),
                Is.EqualTo(FMOD.RESULT.OK));
            Assert.That(bus.getVolume(out float originalGain), Is.EqualTo(FMOD.RESULT.OK));
            Assert.That(bus.getMute(out bool originalMute), Is.EqualTo(FMOD.RESULT.OK));

            Fmod.FmodTrackMixerControls controls = null;
            try
            {
                controls = new Fmod.FmodTrackMixerControls(
                    RuntimeManager.CoreSystem,
                    RuntimeManager.StudioSystem,
                    BusPath,
                    gain: 0.25f,
                    isMuted: true,
                    cutoffHz: 1200f);

                Assert.That(controls.BusPath, Is.EqualTo(BusPath));
                Assert.That(controls.Gain, Is.EqualTo(0.25f));
                Assert.That(controls.IsMuted, Is.True);
                Assert.That(controls.CutoffHz, Is.EqualTo(1200f));
                Assert.That(bus.getVolume(out float appliedGain), Is.EqualTo(FMOD.RESULT.OK));
                Assert.That(appliedGain, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(bus.getMute(out bool appliedMute), Is.EqualTo(FMOD.RESULT.OK));
                Assert.That(appliedMute, Is.True);

                controls.SetGain(0.5f);
                controls.SetMuted(false);
                controls.SetCutoffHz(2400f);
                Assert.That(controls.Gain, Is.EqualTo(0.5f));
                Assert.That(controls.IsMuted, Is.False);
                Assert.That(controls.CutoffHz, Is.EqualTo(2400f));

                controls.PrepareForBankReload();
                Assert.That(bus.getVolume(out float preparedGain), Is.EqualTo(FMOD.RESULT.OK));
                Assert.That(preparedGain, Is.EqualTo(originalGain).Within(0.0001f));
                Assert.That(bus.getMute(out bool preparedMute), Is.EqualTo(FMOD.RESULT.OK));
                Assert.That(preparedMute, Is.EqualTo(originalMute));
                Assert.Throws<System.InvalidOperationException>(() => controls.SetMuted(true));

                controls.RestoreAfterBankReload();
                Assert.That(bus.getVolume(out float restoredControlGain), Is.EqualTo(FMOD.RESULT.OK));
                Assert.That(restoredControlGain, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(bus.getMute(out bool restoredControlMute), Is.EqualTo(FMOD.RESULT.OK));
                Assert.That(restoredControlMute, Is.False);

                controls.Dispose();
                controls = null;
                Assert.That(bus.getVolume(out float restoredGain), Is.EqualTo(FMOD.RESULT.OK));
                Assert.That(restoredGain, Is.EqualTo(originalGain).Within(0.0001f));
                Assert.That(bus.getMute(out bool restoredMute), Is.EqualTo(FMOD.RESULT.OK));
                Assert.That(restoredMute, Is.EqualTo(originalMute));
            }
            finally
            {
                controls?.Dispose();
                if (bus.isValid())
                {
                    bus.setVolume(originalGain);
                    bus.setMute(originalMute);
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator LeadControlsDoNotChangeBassBusState()
        {
            Assert.That(
                RuntimeManager.StudioSystem.getBus(
                    Fmod.FmodTrackBusPaths.Lead,
                    out FMOD.Studio.Bus leadBus),
                Is.EqualTo(FMOD.RESULT.OK));
            Assert.That(
                RuntimeManager.StudioSystem.getBus(
                    Fmod.FmodTrackBusPaths.Bass,
                    out FMOD.Studio.Bus bassBus),
                Is.EqualTo(FMOD.RESULT.OK));
            Assert.That(leadBus.getVolume(out float originalLeadGain), Is.EqualTo(FMOD.RESULT.OK));
            Assert.That(leadBus.getMute(out bool originalLeadMute), Is.EqualTo(FMOD.RESULT.OK));
            Assert.That(bassBus.getVolume(out float originalBassGain), Is.EqualTo(FMOD.RESULT.OK));
            Assert.That(bassBus.getMute(out bool originalBassMute), Is.EqualTo(FMOD.RESULT.OK));

            Fmod.FmodTrackMixerControls controls = null;
            try
            {
                controls = new Fmod.FmodTrackMixerControls(
                    RuntimeManager.CoreSystem,
                    RuntimeManager.StudioSystem,
                    Fmod.FmodTrackBusPaths.Lead,
                    gain: 0.35f,
                    isMuted: !originalLeadMute,
                    cutoffHz: 3200f);

                Assert.That(bassBus.getVolume(out float bassGain), Is.EqualTo(FMOD.RESULT.OK));
                Assert.That(bassGain, Is.EqualTo(originalBassGain).Within(0.0001f));
                Assert.That(bassBus.getMute(out bool bassMute), Is.EqualTo(FMOD.RESULT.OK));
                Assert.That(bassMute, Is.EqualTo(originalBassMute));
            }
            finally
            {
                controls?.Dispose();
                if (leadBus.isValid())
                {
                    leadBus.setVolume(originalLeadGain);
                    leadBus.setMute(originalLeadMute);
                }

                if (bassBus.isValid())
                {
                    bassBus.setVolume(originalBassGain);
                    bassBus.setMute(originalBassMute);
                }
            }

            yield return null;
        }
    }
}
