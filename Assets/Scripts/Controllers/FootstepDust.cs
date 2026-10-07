using UnityEngine;

namespace CardsUnity.Controllers
{
    // Puffs of dust under the planted foot so the step rhythm is visible. One world-space particle
    // system is instantiated from the designer's prefab and emits in place for every step.
    [RequireComponent(typeof(CartoonCharacterGait))]
    public sealed class FootstepDust : MonoBehaviour
    {
        [Tooltip("Particle system prefab. Emission rate is ignored; particles are emitted per step.")]
        [SerializeField] private ParticleSystem dustPrefab;
        [Tooltip("Particles for a full-intensity right-foot step.")]
        [SerializeField, Range(0, 64)] private int particlesPerStep = 8;
        [Tooltip("Share of the particles that a very light step still emits.")]
        [SerializeField, Range(0f, 1f)] private float lightStepShare = 0.35f;
        [Tooltip("Multiplier for the accented left-foot step.")]
        [SerializeField, Range(1f, 4f)] private float accentMultiplier = 1.6f;
        [Tooltip("Offset from the foot contact, in world space.")]
        [SerializeField] private Vector3 contactOffset = new Vector3(0f, 0.03f, 0f);

        private CartoonCharacterGait gait;
        private ParticleSystem dust;

        private void Awake()
        {
            gait = GetComponent<CartoonCharacterGait>();
            if (dustPrefab == null) return;
            dust = Instantiate(dustPrefab);
            dust.name = dustPrefab.name + " (" + name + ")";
            var main = dust.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            var emission = dust.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            dust.Play();
        }

        private void OnEnable()
        {
            if (gait != null) gait.Footstep += OnFootstep;
        }

        private void OnDisable()
        {
            if (gait != null) gait.Footstep -= OnFootstep;
        }

        private void OnDestroy()
        {
            if (dust != null) Destroy(dust.gameObject);
        }

        private void OnFootstep(GaitFootstep step)
        {
            if (dust == null) return;
            float share = Mathf.Lerp(lightStepShare, 1f, Mathf.Clamp01(step.Intensity));
            int count = Mathf.RoundToInt(particlesPerStep * share * (step.LeftFoot ? accentMultiplier : 1f));
            if (count <= 0) return;
            var emit = new ParticleSystem.EmitParams
            {
                position = step.Position + contactOffset,
                applyShapeToPosition = true
            };
            dust.Emit(emit, count);
        }
    }
}
