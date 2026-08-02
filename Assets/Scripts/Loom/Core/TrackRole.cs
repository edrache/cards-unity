namespace Loom.Core
{
    /// <summary>
    /// Selects one deterministic pitched-track harmony policy.
    /// Percussion follows its separate <see cref="PercussionNote"/> contract.
    /// </summary>
    public enum TrackRole
    {
        Bass = 1,
        Lead = 2,
        Pad = 3
    }
}
