using System;

namespace Loom.Core
{
    /// <summary>
    /// Stores one immutable octave of scale intervals and resolves signed scale degrees.
    /// </summary>
    public sealed class Scale
    {
        private const int SemitonesPerOctave = 12;

        private readonly int[] semitoneIntervals;

        public Scale(params int[] semitoneIntervals)
        {
            if (semitoneIntervals == null)
            {
                throw new ArgumentNullException(nameof(semitoneIntervals));
            }

            if (semitoneIntervals.Length == 0)
            {
                throw new ArgumentException(
                    "A scale must contain at least the tonic interval.",
                    nameof(semitoneIntervals));
            }

            if (semitoneIntervals[0] != 0)
            {
                throw new ArgumentException(
                    "The first scale interval must be the tonic at zero semitones.",
                    nameof(semitoneIntervals));
            }

            for (int index = 0; index < semitoneIntervals.Length; index++)
            {
                int interval = semitoneIntervals[index];
                if (interval < 0 || interval >= SemitonesPerOctave)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(semitoneIntervals),
                        interval,
                        "Scale intervals must be between 0 and 11 semitones.");
                }

                if (index > 0 && interval <= semitoneIntervals[index - 1])
                {
                    throw new ArgumentException(
                        "Scale intervals must be unique and strictly increasing.",
                        nameof(semitoneIntervals));
                }
            }

            this.semitoneIntervals = (int[])semitoneIntervals.Clone();
        }

        public int Count => semitoneIntervals.Length;

        public int this[int index]
        {
            get
            {
                if (index < 0 || index >= semitoneIntervals.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), index, null);
                }

                return semitoneIntervals[index];
            }
        }

        /// <summary>
        /// Resolves a signed scale degree relative to an absolute tonic MIDI note.
        /// </summary>
        public Note Resolve(Note tonic, ScaleDegreePitch pitch)
        {
            long degreeCycle = pitch.Degree / Count;
            int intervalIndex = pitch.Degree % Count;
            if (intervalIndex < 0)
            {
                intervalIndex += Count;
                degreeCycle--;
            }

            long octave = (long)pitch.OctaveOffset + degreeCycle;
            long midiNumber = tonic.MidiNumber
                + semitoneIntervals[intervalIndex]
                + (octave * SemitonesPerOctave);

            if (midiNumber < Note.MinMidiNumber || midiNumber > Note.MaxMidiNumber)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pitch),
                    pitch,
                    $"Resolved MIDI note number must be between {Note.MinMidiNumber} "
                    + $"and {Note.MaxMidiNumber}.");
            }

            return new Note((int)midiNumber);
        }
    }
}
