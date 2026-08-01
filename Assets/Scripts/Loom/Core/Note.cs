using System;

namespace Loom.Core
{
    public readonly struct Note : IEquatable<Note>
    {
        public const int MinMidiNumber = 0;
        public const int MaxMidiNumber = 127;
        public const int ConcertAMidiNumber = 69;
        public const double ConcertAFrequencyHz = 440.0;

        private const double SemitonesPerOctave = 12.0;

        public Note(int midiNumber)
        {
            if (midiNumber < MinMidiNumber || midiNumber > MaxMidiNumber)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(midiNumber),
                    midiNumber,
                    $"MIDI note number must be between {MinMidiNumber} and {MaxMidiNumber}.");
            }

            MidiNumber = (byte)midiNumber;
        }

        public byte MidiNumber { get; }

        public double FrequencyHz => ConcertAFrequencyHz
            * Math.Pow(2.0, (MidiNumber - ConcertAMidiNumber) / SemitonesPerOctave);

        public bool Equals(Note other)
        {
            return MidiNumber == other.MidiNumber;
        }

        public override bool Equals(object obj)
        {
            return obj is Note other && Equals(other);
        }

        public override int GetHashCode()
        {
            return MidiNumber;
        }

        public override string ToString()
        {
            return MidiNumber.ToString();
        }

        public static bool operator ==(Note left, Note right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Note left, Note right)
        {
            return !left.Equals(right);
        }
    }
}
