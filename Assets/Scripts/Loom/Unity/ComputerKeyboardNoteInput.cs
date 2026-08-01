using System;
using Loom.Core;
using UnityEngine;

namespace Loom.Unity
{
    /// <summary>
    /// Polls a fixed two-octave computer-keyboard layout and preserves the exact
    /// instrument voice handle that belongs to each pressed key.
    /// </summary>
    public sealed class ComputerKeyboardNoteInput
    {
        public const byte DefaultVelocity = 100;
        public const int MappedKeyCount = 24;

        private static readonly KeyCode[] MappedKeys =
        {
            KeyCode.Z,
            KeyCode.S,
            KeyCode.X,
            KeyCode.D,
            KeyCode.C,
            KeyCode.V,
            KeyCode.G,
            KeyCode.B,
            KeyCode.H,
            KeyCode.N,
            KeyCode.J,
            KeyCode.M,
            KeyCode.Q,
            KeyCode.Alpha2,
            KeyCode.W,
            KeyCode.Alpha3,
            KeyCode.E,
            KeyCode.R,
            KeyCode.Alpha5,
            KeyCode.T,
            KeyCode.Alpha6,
            KeyCode.Y,
            KeyCode.Alpha7,
            KeyCode.U
        };

        private readonly IInstrument instrument;
        private readonly VoiceHandle[] activeVoices =
            new VoiceHandle[MappedKeyCount];

        public ComputerKeyboardNoteInput(
            IInstrument instrument,
            int velocity = DefaultVelocity)
        {
            this.instrument = instrument
                ?? throw new ArgumentNullException(nameof(instrument));

            if (velocity < NoteEvent.MinVelocity
                || velocity > NoteEvent.MaxVelocity)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(velocity),
                    velocity,
                    $"Velocity must be between {NoteEvent.MinVelocity} and {NoteEvent.MaxVelocity}.");
            }

            Velocity = (byte)velocity;
        }

        public byte Velocity { get; }

        /// <summary>
        /// Polls Unity's legacy keyboard API once. The project enables both input
        /// backends, so this remains available while the demo is in focus.
        /// </summary>
        public void Poll()
        {
            for (int index = 0; index < MappedKeyCount; index++)
            {
                KeyCode key = MappedKeys[index];
                if (Input.GetKeyDown(key))
                {
                    HandleKeyDownAt(index);
                }

                if (Input.GetKeyUp(key))
                {
                    HandleKeyUpAt(index);
                }
            }
        }

        /// <summary>
        /// Starts the mapped note unless that physical key is already pressed.
        /// Returns true only when a new note was started.
        /// </summary>
        public bool HandleKeyDown(KeyCode key)
        {
            int index = IndexOfKey(key);
            return index >= 0 && HandleKeyDownAt(index);
        }

        /// <summary>
        /// Releases the exact voice returned for the matching key-down.
        /// Returns the instrument's result, or false for an unpressed/unmapped key.
        /// </summary>
        public bool HandleKeyUp(KeyCode key)
        {
            int index = IndexOfKey(key);
            return index >= 0 && HandleKeyUpAt(index);
        }

        /// <summary>
        /// Clears every retained physical-key state before stopping all voices.
        /// Repeated calls are safe and allow fresh key-downs after focus returns.
        /// </summary>
        public void Panic()
        {
            Array.Clear(activeVoices, 0, activeVoices.Length);
            instrument.AllNotesOff();
        }

        /// <summary>
        /// Resolves the fixed chromatic mapping. Z through M cover MIDI 60-71;
        /// Q through U, with number-row accidentals, cover MIDI 72-83.
        /// </summary>
        public static bool TryGetMidiNote(KeyCode key, out byte midiNoteNumber)
        {
            int index = IndexOfKey(key);
            if (index < 0)
            {
                midiNoteNumber = default;
                return false;
            }

            midiNoteNumber = (byte)(60 + index);
            return true;
        }

        private bool HandleKeyDownAt(int index)
        {
            if (activeVoices[index].IsValid)
            {
                return false;
            }

            var note = new Note(60 + index);
            VoiceHandle voice = instrument.NoteOn(note, Velocity);
            if (!voice.IsValid)
            {
                throw new InvalidOperationException(
                    "The instrument returned an invalid voice handle for note-on.");
            }

            activeVoices[index] = voice;
            return true;
        }

        private bool HandleKeyUpAt(int index)
        {
            VoiceHandle voice = activeVoices[index];
            if (!voice.IsValid)
            {
                return false;
            }

            activeVoices[index] = default;
            return instrument.NoteOff(voice);
        }

        private static int IndexOfKey(KeyCode key)
        {
            for (int index = 0; index < MappedKeyCount; index++)
            {
                if (MappedKeys[index] == key)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
