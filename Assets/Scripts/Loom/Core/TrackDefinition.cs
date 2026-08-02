using System;

namespace Loom.Core
{
    /// <summary>
    /// Owns the immutable identity and resolved-note pattern for one deterministic track.
    /// </summary>
    public sealed class TrackDefinition
    {
        public TrackDefinition(
            TrackId id,
            PatternId patternId,
            StepPattern pattern)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException(
                    "Track definition requires a valid track ID.",
                    nameof(id));
            }

            if (!patternId.IsValid)
            {
                throw new ArgumentException(
                    "Track definition requires a valid pattern ID.",
                    nameof(patternId));
            }

            Id = id;
            PatternId = patternId;
            Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        }

        public TrackId Id { get; }

        public PatternId PatternId { get; }

        public StepPattern Pattern { get; }

        public long PatternLengthTicks => Pattern.LengthTicks;

        /// <summary>
        /// Creates a new sequencer whose random stream is permanently derived from this track identity.
        /// </summary>
        public StepSequencer CreateSequencer(ulong seed)
        {
            return new StepSequencer(Pattern, seed, Id.Value);
        }
    }
}
