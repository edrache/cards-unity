using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Threat-driven stress, shared by gait and spine without changing authored style weights.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-60)]
    public sealed class CharacterStress : MonoBehaviour
    {
        [Header("Balance")]
        [Tooltip("Optional shared player balance. Existing fields below are used when empty.")]
        [SerializeField] private PlayerGameplayBalanceProfile balanceProfile;
        [Tooltip("Optional light exposure source. The component on this GameObject is used when empty.")]
        [SerializeField] private PlayerLightExposure lightExposure;

        [Header("Local fallback — Stress")]
        [SerializeField, Range(0f, 100f)] private float stress;
        [SerializeField, Min(0f)] private float encounterStress = 8f;
        [SerializeField, Min(0f)] private float hitStress = 25f;
        [SerializeField, Min(0.1f)] private float encounterRange = 7f;
        [Tooltip("A creature must remain unseen this long before meeting it counts again.")]
        [SerializeField, Min(1f)] private float encounterResetTime = 10f;
        [Tooltip("Optional recovery per second after threats stop. Zero keeps accumulated stress.")]
        [SerializeField, Min(0f)] private float recoveryPerSecond;
        [SerializeField, Min(0f)] private float recoveryDelay = 10f;
        [Tooltip("Stress added each second at full darkness.")]
        [SerializeField, Min(0f)] private float darknessStressPerSecond = 5f;
        [Header("Per-instance stress pose")]
        [SerializeField, Range(0f, 45f)] private float hunchAngle = 24f;
        [SerializeField, Range(0f, 80f)] private float headLookAngle = 60f;
        [SerializeField, Range(0f, 2f)] private float motionChaos = 1f;
        private readonly Dictionary<int, float> lastSeen = new Dictionary<int, float>();
        private float clock, lastThreat, weight, scanPhase;
        public PlayerGameplayBalanceProfile BalanceProfile => balanceProfile;
        private PlayerStressBalance StressBalance => balanceProfile != null ? balanceProfile.Stress : null;
        public float Stress { get => stress; set => stress = Mathf.Clamp(value, 0f, 100f); }
        public float Weight => isActiveAndEnabled ? weight : 0f;
        public float Chaos => Weight * motionChaos;
        public float Hunch => Weight * hunchAngle;
        public Quaternion HeadLook => Quaternion.Euler(Noise(31f) * 9f * Weight,
            Noise(73f) * headLookAngle * Weight, Noise(97f) * 5f * Weight);

        private void Awake()
        {
            lightExposure ??= GetComponent<PlayerLightExposure>();
            if (StressBalance != null) Stress = StressBalance.InitialStress;
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            clock += dt;
            scanPhase += dt * Mathf.Lerp(0.5f, 2.2f, Weight);
            float darknessStress = GetDarknessStressPerSecond();
            if (darknessStress > 0f)
            {
                Stress += darknessStress * dt;
                lastThreat = clock;
            }
            if (clock - lastThreat > (StressBalance?.RecoveryDelay ?? recoveryDelay))
                Stress -= (StressBalance?.RecoveryPerSecond ?? recoveryPerSecond) * dt;
            weight = Mathf.Lerp(weight, stress / 100f, 1f - Mathf.Exp(-dt / 0.45f));
        }

        private float Noise(float channel) => Mathf.PerlinNoise(channel, scanPhase) * 2f - 1f;

        private float GetDarknessStressPerSecond()
        {
            if (lightExposure == null || !lightExposure.isActiveAndEnabled) return 0f;
            float darkness = Mathf.Clamp01(lightExposure.Darkness);
            return darkness * (StressBalance?.DarknessStressPerSecond ?? darknessStressPerSecond);
        }

        public void RegisterHit()
        {
            if (!isActiveAndEnabled) return;
            Stress += StressBalance?.HitStress ?? hitStress;
            lastThreat = clock;
        }

        public void Observe(ProceduralCentipede creature)
        {
            if (creature == null || !creature.isActiveAndEnabled) return;
            Observe(creature.transform);
        }

        /// <summary>Registers a visible nearby threat without coupling future enemies to centipede code.</summary>
        public void Observe(Transform threat)
        {
            if (!isActiveAndEnabled || threat == null || !threat.gameObject.activeInHierarchy) return;
            Vector3 origin = transform.position + Vector3.up * 1.2f;
            Vector3 delta = threat.position + threat.up * 0.25f - origin;
            float range = StressBalance?.EncounterRange ?? encounterRange;
            if (delta.sqrMagnitude > range * range) return;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(threat)) continue;
                return;
            }
            int id = threat.GetInstanceID();
            if (!lastSeen.TryGetValue(id, out float previous)
                || clock - previous >= (StressBalance?.EncounterResetTime ?? encounterResetTime))
                Stress += StressBalance?.EncounterStress ?? encounterStress;
            lastSeen[id] = lastThreat = clock;
        }

#if UNITY_EDITOR
        internal void CopyLegacyBalanceTo(PlayerStressBalance destination)
        {
            destination.Capture(stress, encounterStress, hitStress, encounterRange,
                encounterResetTime, recoveryPerSecond, recoveryDelay, darknessStressPerSecond);
        }

        internal void AssignBalanceProfile(PlayerGameplayBalanceProfile profile)
        {
            balanceProfile = profile;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
