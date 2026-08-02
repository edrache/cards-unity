namespace Loom.Core
{
    /// <summary>
    /// Identifies an independent deterministic random decision domain.
    /// </summary>
    /// <remarks>
    /// Values are serialized hash inputs. Existing values must never be renumbered or reused.
    /// </remarks>
    public enum RandomSlot : ulong
    {
        StepTrigger = 1UL,
        StepVelocity = 2UL,
        StepDuration = 3UL,
        StepRatchetCount = 4UL,
        StepMicrotiming = 5UL
    }
}
