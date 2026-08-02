using System;

namespace Loom.Core
{
    /// <summary>
    /// Adds a deterministic same-onset ordering key to a resolved logical note event.
    /// </summary>
    public readonly struct ScheduledNoteEvent : IEquatable<ScheduledNoteEvent>
    {
        public ScheduledNoteEvent(NoteEvent noteEvent, ulong sequenceNumber)
        {
            if (noteEvent.Velocity < NoteEvent.MinVelocity
                || noteEvent.Velocity > NoteEvent.MaxVelocity
                || noteEvent.StartTick < 0
                || noteEvent.DurationTicks <= 0
                || noteEvent.DurationTicks > long.MaxValue - noteEvent.StartTick)
            {
                throw new ArgumentException(
                    "Scheduled note event must contain a valid logical note event.",
                    nameof(noteEvent));
            }

            NoteEvent = noteEvent;
            SequenceNumber = sequenceNumber;
        }

        public NoteEvent NoteEvent { get; }

        public long StartTick => NoteEvent.StartTick;

        public ulong SequenceNumber { get; }

        public bool Equals(ScheduledNoteEvent other)
        {
            return NoteEvent.Equals(other.NoteEvent)
                && SequenceNumber == other.SequenceNumber;
        }

        public override bool Equals(object obj)
        {
            return obj is ScheduledNoteEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (NoteEvent.GetHashCode() * 397) ^ SequenceNumber.GetHashCode();
            }
        }

        public static bool operator ==(
            ScheduledNoteEvent left,
            ScheduledNoteEvent right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            ScheduledNoteEvent left,
            ScheduledNoteEvent right)
        {
            return !left.Equals(right);
        }
    }
}
