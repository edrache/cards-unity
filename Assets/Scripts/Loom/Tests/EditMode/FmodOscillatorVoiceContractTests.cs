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
    }
}
