using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Threat-driven stress, shared by gait and spine without changing authored style weights.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-60)]
    public sealed class CharacterStress : MonoBehaviour
    {
        [SerializeField, Range(0f, 100f)] private float stress;
        [SerializeField, Min(0f)] private float encounterStress = 8f;
        [SerializeField, Min(0f)] private float hitStress = 25f;
        [SerializeField, Min(0.1f)] private float encounterRange = 7f;
        [Tooltip("A creature must remain unseen this long before meeting it counts again.")]
        [SerializeField, Min(1f)] private float encounterResetTime = 10f;
        [Tooltip("Optional recovery per second after threats stop. Zero keeps accumulated stress.")]
        [SerializeField, Min(0f)] private float recoveryPerSecond;
        [SerializeField, Min(0f)] private float recoveryDelay = 10f;
        [SerializeField, Range(0f, 45f)] private float hunchAngle = 24f;
        [SerializeField, Range(0f, 80f)] private float headLookAngle = 60f;
        [SerializeField, Range(0f, 2f)] private float motionChaos = 1f;
        private readonly Dictionary<int, float> lastSeen = new Dictionary<int, float>();
        private float clock, lastThreat, weight, scanPhase;
        public float Stress { get => stress; set => stress = Mathf.Clamp(value, 0f, 100f); }
        public float Weight => isActiveAndEnabled ? weight : 0f;
        public float Chaos => Weight * motionChaos;
        public float Hunch => Weight * hunchAngle;
        public Quaternion HeadLook => Quaternion.Euler(Noise(31f) * 9f * Weight,
            Noise(73f) * headLookAngle * Weight, Noise(97f) * 5f * Weight);

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            clock += dt;
            scanPhase += dt * Mathf.Lerp(0.5f, 2.2f, Weight);
            if (clock - lastThreat > recoveryDelay) Stress -= recoveryPerSecond * dt;
            weight = Mathf.Lerp(weight, stress / 100f, 1f - Mathf.Exp(-dt / 0.45f));
        }

        private float Noise(float channel) => Mathf.PerlinNoise(channel, scanPhase) * 2f - 1f;

        public void RegisterHit()
        {
            if (!isActiveAndEnabled) return;
            Stress += hitStress;
            lastThreat = clock;
        }

        public void Observe(ProceduralCentipede creature)
        {
            if (!isActiveAndEnabled || creature == null || !creature.isActiveAndEnabled) return;
            Vector3 origin = transform.position + Vector3.up * 1.2f;
            Vector3 delta = creature.transform.position + creature.transform.up * 0.25f - origin;
            if (delta.sqrMagnitude > encounterRange * encounterRange) return;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(creature.transform)) continue;
                return;
            }
            int id = creature.GetInstanceID();
            if (!lastSeen.TryGetValue(id, out float previous) || clock - previous >= encounterResetTime)
                Stress += encounterStress;
            lastSeen[id] = lastThreat = clock;
        }
    }
}
