using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class FmodTrackMixerControlsTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ConstructorRejectsAnEmptyBusPathBeforeCallingFmod(string busPath)
        {
            Assert.Throws<ArgumentException>(
                () => new Fmod.FmodTrackMixerControls(default, default, busPath));
        }

        [TestCase(-0.01f)]
        [TestCase(1.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void ConstructorRejectsInvalidGainBeforeCallingFmod(float gain)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodTrackMixerControls(
                    default,
                    default,
                    "bus:/MUS_Synth",
                    gain));
        }

        [TestCase(19.99f)]
        [TestCase(22000.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.NegativeInfinity)]
        public void ConstructorRejectsInvalidCutoffBeforeCallingFmod(float cutoffHz)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodTrackMixerControls(
                    default,
                    default,
                    "bus:/MUS_Synth",
                    cutoffHz: cutoffHz));
        }

        [Test]
        public void ConstructorRejectsAnInvalidCoreSystemBeforeStudioAccess()
        {
            Assert.Throws<ArgumentException>(
                () => new Fmod.FmodTrackMixerControls(
                    default,
                    default,
                    "bus:/MUS_Synth"));
        }
    }
}
