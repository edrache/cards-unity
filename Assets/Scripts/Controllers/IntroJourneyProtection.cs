using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>
    /// Instance-scoped safety for the scripted cave entrance. The intro owner explicitly begins
    /// protection and releases it after the player reaches the first room.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IntroJourneyProtection : MonoBehaviour
    {
        private CharacterKnockdown knockdown;
        private CharacterStress stress;
        private HandheldTorch protectedTorch;
        private bool knockdownWasEnabled;
        private bool stressWasEnabled;
        private bool torchFuelWasSuspended;

        public bool IsProtectionActive { get; private set; }

        public void BeginProtection()
        {
            if (IsProtectionActive) return;
            IsProtectionActive = true;

            knockdown = GetComponent<CharacterKnockdown>();
            stress = GetComponent<CharacterStress>();
            if (knockdown != null)
            {
                knockdownWasEnabled = knockdown.enabled;
                knockdown.enabled = false;
            }
            if (stress != null)
            {
                stressWasEnabled = stress.enabled;
                stress.enabled = false;
            }
            ProtectCurrentTorch();
        }

        public void ReleaseProtection()
        {
            if (!IsProtectionActive) return;
            IsProtectionActive = false;

            if (knockdown != null) knockdown.enabled = knockdownWasEnabled;
            if (stress != null) stress.enabled = stressWasEnabled;
            RestoreProtectedTorch();
        }

        private void LateUpdate()
        {
            if (IsProtectionActive) ProtectCurrentTorch();
        }

        private void ProtectCurrentTorch()
        {
            var current = GetComponentInChildren<HandheldTorch>(true);
            // Keep protecting a torch that was detached unexpectedly; its pending impact must
            // remain free until the journey owner explicitly releases protection.
            if (current == null || current == protectedTorch) return;
            RestoreProtectedTorch();
            protectedTorch = current;
            if (protectedTorch == null) return;
            torchFuelWasSuspended = protectedTorch.FuelConsumptionSuspended;
            protectedTorch.SetFuelConsumptionSuspended(true);
        }

        private void RestoreProtectedTorch()
        {
            if (protectedTorch != null)
                protectedTorch.SetFuelConsumptionSuspended(torchFuelWasSuspended);
            protectedTorch = null;
            torchFuelWasSuspended = false;
        }

        private void OnDisable() => ReleaseProtection();
        private void OnDestroy() => ReleaseProtection();
    }
}
