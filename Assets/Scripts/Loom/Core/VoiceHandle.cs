using System;

namespace Loom.Core
{
    public readonly struct VoiceHandle : IEquatable<VoiceHandle>
    {
        public VoiceHandle(ulong id)
        {
            if (id == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(id),
                    id,
                    "Voice handle ID must be non-zero.");
            }

            Id = id;
        }

        public ulong Id { get; }

        public bool IsValid => Id != 0;

        public bool Equals(VoiceHandle other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is VoiceHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override string ToString()
        {
            return IsValid ? Id.ToString() : "Invalid";
        }

        public static bool operator ==(VoiceHandle left, VoiceHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VoiceHandle left, VoiceHandle right)
        {
            return !left.Equals(right);
        }
    }
}
