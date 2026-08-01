using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class InstrumentContractTests
    {
        [Test]
        public void DefaultVoiceHandleIsInvalid()
        {
            Core.VoiceHandle handle = default;

            Assert.That(handle.IsValid, Is.False);
            Assert.That(handle.Id, Is.Zero);
        }

        [Test]
        public void VoiceHandleRequiresANonZeroIdAndHasValueSemantics()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Core.VoiceHandle(0));

            Core.VoiceHandle first = new Core.VoiceHandle(42);
            Core.VoiceHandle second = new Core.VoiceHandle(42);
            Core.VoiceHandle different = new Core.VoiceHandle(43);

            Assert.That(first.IsValid, Is.True);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first == second, Is.True);
            Assert.That(first != different, Is.True);
        }

        [Test]
        public void InstrumentUsesReturnedHandleForVoiceOwnership()
        {
            using (var instrument = new RecordingInstrument())
            {
                Core.VoiceHandle first = instrument.NoteOn(new Core.Note(60), 100);
                Core.VoiceHandle second = instrument.NoteOn(new Core.Note(64), 100);

                Assert.That(first, Is.Not.EqualTo(second));
                Assert.That(instrument.NoteOff(first), Is.True);
                Assert.That(instrument.NoteOff(first), Is.False);
                Assert.That(instrument.NoteOff(default), Is.False);

                instrument.AllNotesOff();

                Assert.That(instrument.NoteOff(second), Is.False);
            }
        }

        [Test]
        public void InstrumentContractOwnsDisposableResources()
        {
            Assert.That(typeof(IDisposable).IsAssignableFrom(typeof(Core.IInstrument)), Is.True);
        }

        private sealed class RecordingInstrument : Core.IInstrument
        {
            private readonly HashSet<Core.VoiceHandle> activeVoices =
                new HashSet<Core.VoiceHandle>();

            private ulong nextVoiceId = 1;
            private bool isDisposed;

            public Core.VoiceHandle NoteOn(Core.Note note, byte velocity)
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(nameof(RecordingInstrument));
                }

                if (velocity < Core.NoteEvent.MinVelocity
                    || velocity > Core.NoteEvent.MaxVelocity)
                {
                    throw new ArgumentOutOfRangeException(nameof(velocity));
                }

                var handle = new Core.VoiceHandle(nextVoiceId++);
                activeVoices.Add(handle);
                return handle;
            }

            public bool NoteOff(Core.VoiceHandle voice)
            {
                return voice.IsValid && activeVoices.Remove(voice);
            }

            public void AllNotesOff()
            {
                activeVoices.Clear();
            }

            public void Dispose()
            {
                if (isDisposed)
                {
                    return;
                }

                AllNotesOff();
                isDisposed = true;
            }
        }
    }
}
