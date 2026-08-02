using System;

namespace Loom.Core
{
    /// <summary>
    /// Identifies one instrument-local percussion sample slot without assigning a MIDI pitch.
    /// </summary>
    public readonly struct PercussionNote : IEquatable<PercussionNote>
    {
        public PercussionNote(ulong sampleSlotId)
        {
            if (sampleSlotId == 0UL)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleSlotId),
                    sampleSlotId,
                    "Percussion sample slot ID must be non-zero.");
            }

            SampleSlotId = sampleSlotId;
        }

        public ulong SampleSlotId { get; }

        public bool IsValid => SampleSlotId != 0UL;

        public bool Equals(PercussionNote other)
        {
            return SampleSlotId == other.SampleSlotId;
        }

        public override bool Equals(object obj)
        {
            return obj is PercussionNote other && Equals(other);
        }

        public override int GetHashCode()
        {
            return SampleSlotId.GetHashCode();
        }

        public override string ToString()
        {
            return IsValid ? SampleSlotId.ToString() : "Invalid";
        }

        public static bool operator ==(PercussionNote left, PercussionNote right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PercussionNote left, PercussionNote right)
        {
            return !left.Equals(right);
        }
    }
}
