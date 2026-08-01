using System;

namespace Loom.Core
{
    public readonly struct NoteEvent : IEquatable<NoteEvent>
    {
        public const int MinVelocity = 1;
        public const int MaxVelocity = 127;

        public NoteEvent(Note note, int velocity, long startTick, long durationTicks)
        {
            if (velocity < MinVelocity || velocity > MaxVelocity)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(velocity),
                    velocity,
                    $"Velocity must be between {MinVelocity} and {MaxVelocity}.");
            }

            if (startTick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startTick),
                    startTick,
                    "Start tick cannot be negative.");
            }

            if (durationTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationTicks),
                    durationTicks,
                    "Duration must be greater than zero ticks.");
            }

            if (durationTicks > long.MaxValue - startTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationTicks),
                    durationTicks,
                    "Start tick and duration must not exceed Int64.MaxValue.");
            }

            Note = note;
            Velocity = (byte)velocity;
            StartTick = startTick;
            DurationTicks = durationTicks;
        }

        public Note Note { get; }

        public byte Velocity { get; }

        public long StartTick { get; }

        public long DurationTicks { get; }

        public long EndTick => StartTick + DurationTicks;

        public bool Equals(NoteEvent other)
        {
            return Note == other.Note
                && Velocity == other.Velocity
                && StartTick == other.StartTick
                && DurationTicks == other.DurationTicks;
        }

        public override bool Equals(object obj)
        {
            return obj is NoteEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Note.GetHashCode();
                hashCode = (hashCode * 397) ^ Velocity;
                hashCode = (hashCode * 397) ^ StartTick.GetHashCode();
                hashCode = (hashCode * 397) ^ DurationTicks.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            return $"Note {Note.MidiNumber} at tick {StartTick} for {DurationTicks} ticks, velocity {Velocity}";
        }

        public static bool operator ==(NoteEvent left, NoteEvent right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NoteEvent left, NoteEvent right)
        {
            return !left.Equals(right);
        }
    }
}
