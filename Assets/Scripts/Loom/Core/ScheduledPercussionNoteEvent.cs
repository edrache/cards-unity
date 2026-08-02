using System;

namespace Loom.Core
{
    /// <summary>
    /// Adds a deterministic same-onset ordering key to a logical percussion note event.
    /// </summary>
    public readonly struct ScheduledPercussionNoteEvent : IEquatable<ScheduledPercussionNoteEvent>
    {
        public ScheduledPercussionNoteEvent(PercussionNoteEvent noteEvent, ulong sequenceNumber)
        {
            if (!noteEvent.Note.IsValid
                || noteEvent.Velocity < PercussionNoteEvent.MinVelocity
                || noteEvent.Velocity > PercussionNoteEvent.MaxVelocity
                || noteEvent.StartTick < 0
                || noteEvent.DurationTicks <= 0
                || noteEvent.DurationTicks > long.MaxValue - noteEvent.StartTick)
            {
                throw new ArgumentException(
                    "Scheduled note event must contain a valid logical note event.",
                    nameof(noteEvent));
            }

            PercussionNoteEvent = noteEvent;
            SequenceNumber = sequenceNumber;
        }

        public PercussionNoteEvent PercussionNoteEvent { get; }

        public long StartTick => PercussionNoteEvent.StartTick;

        public ulong SequenceNumber { get; }

        public bool Equals(ScheduledPercussionNoteEvent other)
        {
            return PercussionNoteEvent.Equals(other.PercussionNoteEvent)
                && SequenceNumber == other.SequenceNumber;
        }

        public override bool Equals(object obj)
        {
            return obj is ScheduledPercussionNoteEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (PercussionNoteEvent.GetHashCode() * 397) ^ SequenceNumber.GetHashCode();
            }
        }

        public static bool operator ==(
            ScheduledPercussionNoteEvent left,
            ScheduledPercussionNoteEvent right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            ScheduledPercussionNoteEvent left,
            ScheduledPercussionNoteEvent right)
        {
            return !left.Equals(right);
        }
    }
}
