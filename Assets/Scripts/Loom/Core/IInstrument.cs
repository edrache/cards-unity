using System;

namespace Loom.Core
{
    public interface IInstrument : IDisposable
    {
        /// <summary>
        /// Starts a voice and returns the stable handle that owns its later note-off.
        /// Velocity uses the MIDI range 1 through 127.
        /// </summary>
        VoiceHandle NoteOn(Note note, byte velocity);

        /// <summary>
        /// Releases the voice identified by <paramref name="voice"/>.
        /// Returns false when the handle is invalid, stale, or not owned by this instrument.
        /// </summary>
        bool NoteOff(VoiceHandle voice);

        /// <summary>
        /// Releases every active voice and invalidates all outstanding voice handles.
        /// </summary>
        void AllNotesOff();
    }
}
