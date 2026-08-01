using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Loom.Tests.EditMode
{
    public sealed class ComputerKeyboardNoteInputTests
    {
        [TestCase(KeyCode.Z, 60)]
        [TestCase(KeyCode.S, 61)]
        [TestCase(KeyCode.X, 62)]
        [TestCase(KeyCode.D, 63)]
        [TestCase(KeyCode.C, 64)]
        [TestCase(KeyCode.V, 65)]
        [TestCase(KeyCode.G, 66)]
        [TestCase(KeyCode.B, 67)]
        [TestCase(KeyCode.H, 68)]
        [TestCase(KeyCode.N, 69)]
        [TestCase(KeyCode.J, 70)]
        [TestCase(KeyCode.M, 71)]
        [TestCase(KeyCode.Q, 72)]
        [TestCase(KeyCode.Alpha2, 73)]
        [TestCase(KeyCode.W, 74)]
        [TestCase(KeyCode.Alpha3, 75)]
        [TestCase(KeyCode.E, 76)]
        [TestCase(KeyCode.R, 77)]
        [TestCase(KeyCode.Alpha5, 78)]
        [TestCase(KeyCode.T, 79)]
        [TestCase(KeyCode.Alpha6, 80)]
        [TestCase(KeyCode.Y, 81)]
        [TestCase(KeyCode.Alpha7, 82)]
        [TestCase(KeyCode.U, 83)]
        public void FixedMappingResolvesExpectedMidiNote(
            KeyCode key,
            int expectedMidiNote)
        {
            bool mapped = Unity.ComputerKeyboardNoteInput.TryGetMidiNote(
                key,
                out byte midiNote);

            Assert.That(mapped, Is.True);
            Assert.That(midiNote, Is.EqualTo(expectedMidiNote));
        }

        [Test]
        public void ConstructorRequiresAnInstrumentAndPlayableVelocity()
        {
            Assert.Throws<ArgumentNullException>(
                () => new Unity.ComputerKeyboardNoteInput(null));

            using (var instrument = new RecordingInstrument())
            {
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => new Unity.ComputerKeyboardNoteInput(instrument, 0));
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => new Unity.ComputerKeyboardNoteInput(instrument, 128));
            }
        }

        [Test]
        public void KeyDownStartsMappedNoteAndSuppressesRepeat()
        {
            using (var instrument = new RecordingInstrument())
            {
                var input = new Unity.ComputerKeyboardNoteInput(instrument, 91);

                Assert.That(input.HandleKeyDown(KeyCode.E), Is.True);
                Assert.That(input.HandleKeyDown(KeyCode.E), Is.False);

                Assert.That(instrument.NoteOnCount, Is.EqualTo(1));
                Assert.That(instrument.LastNote.MidiNumber, Is.EqualTo(76));
                Assert.That(instrument.LastVelocity, Is.EqualTo(91));
            }
        }

        [Test]
        public void KeyUpReleasesExactVoiceAndSuppressesRepeat()
        {
            using (var instrument = new RecordingInstrument())
            {
                var input = new Unity.ComputerKeyboardNoteInput(instrument);

                Assert.That(input.HandleKeyDown(KeyCode.Z), Is.True);
                Core.VoiceHandle zVoice = instrument.LastStartedVoice;
                Assert.That(input.HandleKeyDown(KeyCode.X), Is.True);
                Core.VoiceHandle xVoice = instrument.LastStartedVoice;

                Assert.That(input.HandleKeyUp(KeyCode.Z), Is.True);
                Assert.That(instrument.LastReleasedVoice, Is.EqualTo(zVoice));
                Assert.That(instrument.LastReleasedVoice, Is.Not.EqualTo(xVoice));
                Assert.That(input.HandleKeyUp(KeyCode.Z), Is.False);
                Assert.That(instrument.NoteOffCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void FailedNoteOffStillClearsPressedKeyState()
        {
            using (var instrument = new RecordingInstrument
            {
                NoteOffResult = false
            })
            {
                var input = new Unity.ComputerKeyboardNoteInput(instrument);

                Assert.That(input.HandleKeyDown(KeyCode.Q), Is.True);
                Assert.That(input.HandleKeyUp(KeyCode.Q), Is.False);
                Assert.That(input.HandleKeyDown(KeyCode.Q), Is.True);
                Assert.That(instrument.NoteOnCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void UnmappedKeysDoNotReachInstrument()
        {
            using (var instrument = new RecordingInstrument())
            {
                var input = new Unity.ComputerKeyboardNoteInput(instrument);

                Assert.That(input.HandleKeyDown(KeyCode.Space), Is.False);
                Assert.That(input.HandleKeyUp(KeyCode.Space), Is.False);
                Assert.That(
                    Unity.ComputerKeyboardNoteInput.TryGetMidiNote(
                        KeyCode.Space,
                        out byte midiNote),
                    Is.False);
                Assert.That(midiNote, Is.Zero);
                Assert.That(instrument.NoteOnCount, Is.Zero);
                Assert.That(instrument.NoteOffCount, Is.Zero);
            }
        }

        [Test]
        public void InvalidVoiceHandleFromInstrumentIsRejected()
        {
            using (var instrument = new RecordingInstrument
            {
                ReturnInvalidVoice = true
            })
            {
                var input = new Unity.ComputerKeyboardNoteInput(instrument);

                Assert.Throws<InvalidOperationException>(
                    () => input.HandleKeyDown(KeyCode.Z));
            }
        }

        private sealed class RecordingInstrument : Core.IInstrument
        {
            private readonly HashSet<Core.VoiceHandle> activeVoices =
                new HashSet<Core.VoiceHandle>();

            private ulong nextVoiceId = 1;

            public int NoteOnCount { get; private set; }

            public int NoteOffCount { get; private set; }

            public Core.Note LastNote { get; private set; }

            public byte LastVelocity { get; private set; }

            public Core.VoiceHandle LastStartedVoice { get; private set; }

            public Core.VoiceHandle LastReleasedVoice { get; private set; }

            public bool NoteOffResult { get; set; } = true;

            public bool ReturnInvalidVoice { get; set; }

            public Core.VoiceHandle NoteOn(Core.Note note, byte velocity)
            {
                NoteOnCount++;
                LastNote = note;
                LastVelocity = velocity;

                if (ReturnInvalidVoice)
                {
                    return default;
                }

                LastStartedVoice = new Core.VoiceHandle(nextVoiceId++);
                activeVoices.Add(LastStartedVoice);
                return LastStartedVoice;
            }

            public bool NoteOff(Core.VoiceHandle voice)
            {
                NoteOffCount++;
                LastReleasedVoice = voice;
                activeVoices.Remove(voice);
                return NoteOffResult;
            }

            public void AllNotesOff()
            {
                activeVoices.Clear();
            }

            public void Dispose()
            {
                AllNotesOff();
            }
        }
    }
}
