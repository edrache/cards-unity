using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Temporary knee posture layered over the existing locomotion mix.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-50)]
    public sealed class CharacterKnockdown : MonoBehaviour
    {
        [Header("Balance")]
        [Tooltip("Optional shared player balance. Existing fields below are used when empty.")]
        [SerializeField] private PlayerGameplayBalanceProfile balanceProfile;

        [Header("Local fallback — Knockdown")]
        [SerializeField, Min(0.05f)] private float fallDuration = 0.25f;
        [Tooltip("Seconds of actual movement on the knees before starting to stand. Standing still pauses recovery.")]
        [SerializeField, Min(0f)] private float kneelDuration = 1.4f;
        [SerializeField, Min(0.05f)] private float standDuration = 0.65f;
        [SerializeField, Min(0f)] private float immunityAfterStanding = 1.5f;
        private CartoonCharacterGait gait;
        private float elapsed, immunity, movingTime, reportedDistance;
        public PlayerGameplayBalanceProfile BalanceProfile => balanceProfile;
        private PlayerKnockdownBalance KnockdownBalance => balanceProfile != null ? balanceProfile.Knockdown : null;
        public bool IsDown { get; private set; }
        public bool CanBeHit => isActiveAndEnabled && !IsDown && immunity <= 0f;

        private void Awake() => gait = GetComponent<CartoonCharacterGait>();

        public bool TryKnockDown()
        {
            if (!CanBeHit) return false;
            if (gait == null) gait = GetComponent<CartoonCharacterGait>();
            if (gait == null) return false;
            IsDown = true;
            elapsed = movingTime = reportedDistance = 0f;
            GetComponent<TorchAttack>()?.CancelAttack();
            GetComponent<TorchInteraction>()?.CancelInteraction();
            var torch = GetComponentInChildren<HandheldTorch>();
            if (torch != null && torch.IsHeld) torch.Drop();
            return true;
        }

        public void ReportMovement(float distance) => reportedDistance += Mathf.Max(0f, distance);

        private void Update()
        {
            Tick(Time.deltaTime, reportedDistance);
            reportedDistance = 0f;
        }

        public void Tick(float dt, float movementDistance = 0f)
        {
            dt = Mathf.Max(0f, dt);
            immunity = Mathf.Max(0f, immunity - dt);
            if (!IsDown) return;
            elapsed += dt;
            float fall = Mathf.Max(0.05f, KnockdownBalance?.FallDuration ?? fallDuration);
            float stand = Mathf.Max(0.05f, KnockdownBalance?.StandDuration ?? standDuration);
            if (elapsed >= fall && movementDistance > 0.0001f) movingTime += dt;
            float holdEnd = Mathf.Max(0f, KnockdownBalance?.KneelDuration ?? kneelDuration);
            gait.KnockdownWeight = elapsed < fall ? Mathf.SmoothStep(0f, 1f, elapsed / fall)
                : 1f - PoseEasing.SmootherStep((movingTime - holdEnd) / stand);
            if (movingTime >= holdEnd + stand)
            {
                IsDown = false;
                gait.KnockdownWeight = 0f;
                immunity = KnockdownBalance?.ImmunityAfterStanding ?? immunityAfterStanding;
            }
        }

        private void OnDisable()
        {
            IsDown = false;
            if (gait != null) gait.KnockdownWeight = 0f;
        }

#if UNITY_EDITOR
        internal void CopyLegacyBalanceTo(PlayerKnockdownBalance destination)
        {
            destination.Capture(fallDuration, kneelDuration, standDuration, immunityAfterStanding);
        }

        internal void AssignBalanceProfile(PlayerGameplayBalanceProfile profile)
        {
            balanceProfile = profile;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
