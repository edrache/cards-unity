using System;

namespace Loom.Core
{
    /// <summary>
    /// Carries one typed track event and its canonical cross-track ordering keys.
    /// </summary>
    public readonly struct MultiTrackScheduledEvent : IEquatable<MultiTrackScheduledEvent>
    {
        private readonly ScheduledNoteEvent pitchedEvent;
        private readonly ScheduledPercussionNoteEvent percussionEvent;

        private MultiTrackScheduledEvent(
            TrackId trackId,
            ScheduledNoteEvent scheduledEvent)
        {
            if (!trackId.IsValid)
            {
                throw new ArgumentException("Track event requires a valid track ID.", nameof(trackId));
            }

            TrackId = trackId;
            Kind = MultiTrackEventKind.Pitched;
            pitchedEvent = scheduledEvent;
            percussionEvent = default;
        }

        private MultiTrackScheduledEvent(
            TrackId trackId,
            ScheduledPercussionNoteEvent scheduledEvent)
        {
            if (!trackId.IsValid)
            {
                throw new ArgumentException("Track event requires a valid track ID.", nameof(trackId));
            }

            TrackId = trackId;
            Kind = MultiTrackEventKind.Percussion;
            pitchedEvent = default;
            percussionEvent = scheduledEvent;
        }

        public TrackId TrackId { get; }

        public MultiTrackEventKind Kind { get; }

        public long StartTick => Kind == MultiTrackEventKind.Pitched
            ? pitchedEvent.StartTick
            : percussionEvent.StartTick;

        public ulong TrackSequenceNumber => Kind == MultiTrackEventKind.Pitched
            ? pitchedEvent.SequenceNumber
            : percussionEvent.SequenceNumber;

        public static MultiTrackScheduledEvent FromPitched(
            TrackId trackId,
            ScheduledNoteEvent scheduledEvent)
        {
            return new MultiTrackScheduledEvent(trackId, scheduledEvent);
        }

        public static MultiTrackScheduledEvent FromPercussion(
            TrackId trackId,
            ScheduledPercussionNoteEvent scheduledEvent)
        {
            return new MultiTrackScheduledEvent(trackId, scheduledEvent);
        }

        public bool TryGetPitchedEvent(out ScheduledNoteEvent scheduledEvent)
        {
            scheduledEvent = pitchedEvent;
            return Kind == MultiTrackEventKind.Pitched;
        }

        public bool TryGetPercussionEvent(
            out ScheduledPercussionNoteEvent scheduledEvent)
        {
            scheduledEvent = percussionEvent;
            return Kind == MultiTrackEventKind.Percussion;
        }

        public bool Equals(MultiTrackScheduledEvent other)
        {
            return TrackId.Equals(other.TrackId)
                && Kind == other.Kind
                && pitchedEvent.Equals(other.pitchedEvent)
                && percussionEvent.Equals(other.percussionEvent);
        }

        public override bool Equals(object obj)
        {
            return obj is MultiTrackScheduledEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = TrackId.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Kind;
                hashCode = (hashCode * 397) ^ pitchedEvent.GetHashCode();
                return (hashCode * 397) ^ percussionEvent.GetHashCode();
            }
        }

        public static bool operator ==(
            MultiTrackScheduledEvent left,
            MultiTrackScheduledEvent right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            MultiTrackScheduledEvent left,
            MultiTrackScheduledEvent right)
        {
            return !left.Equals(right);
        }
    }
}
