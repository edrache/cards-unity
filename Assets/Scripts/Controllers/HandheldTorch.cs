using UnityEngine;

namespace CardsUnity.Controllers
{
    [DefaultExecutionOrder(100)]
    public sealed class HandheldTorch : MonoBehaviour
    {
        [SerializeField] private Transform character;
        [SerializeField] private Transform upperArm;
        [SerializeField] private Transform forearm;
        [SerializeField] private Light flameLight;
        [SerializeField] private ParticleSystem fire;
        [SerializeField] private ParticleSystem smoke;

        [Header("Illumination")]
        [Tooltip("Brightness of the torch. Zero switches off its illumination.")]
        [SerializeField, Range(0f, 15f)] private float brightness = 5f;
        [Tooltip("Maximum light reach in metres.")]
        [SerializeField, Range(1f, 20f)] private float lightRange = 8f;
        [SerializeField] private Color lightColor = new Color(1f, 0.48f, 0.16f);
        [SerializeField, Range(0f, 0.6f)] private float flickerAmount = 0.16f;
        [SerializeField, Range(0.1f, 8f)] private float flickerSpeed = 2.5f;
        [SerializeField, Range(0f, 1f)] private float movementFlicker = 0.3f;

        [Header("Burn lifetime")]
        [Tooltip("Burn duration in seconds of active gameplay.")]
        [SerializeField, Min(0.1f)] private float lifetime = 180f;
        [Tooltip("Fraction of lifetime spent gradually fading out.")]
        [SerializeField, Range(0.05f, 1f)] private float burnoutFraction = 0.4f;
        [SerializeField, Range(0f, 2f)] private float dyingFlickerAmount = 1.4f;

        public float RemainingLifetime => Mathf.Max(0f, lifetime - burnAge);
        public bool IsBurnedOut => RemainingLifetime <= 0f;
        private float burnAge;
        private float smokeEmission;
        private bool emissionCached;

        [Header("Grip and inertia")]
        [SerializeField, Range(0f, 1f)] private float holdingPose = 0.9f;
        [SerializeField, Range(0f, 25f)] private float swayAngle = 12f;
        [SerializeField, Range(0.03f, 0.5f)] private float swayResponse = 0.18f;
        [SerializeField, Range(0f, 2f)] private float smokeTrail = 0.55f;
        [SerializeField] private Vector3 gripOffset = new Vector3(0f, -0.32f, 0f);

        [SerializeField, HideInInspector] private float armOutwardAngle = 22f;

        private Vector3 previousPosition, filteredVelocity, sway, swayVelocity;
        private float flicker = 1f;
        private bool initialized;

        public void SetArmOutwardAngle(float angle)
        {
            armOutwardAngle = Mathf.Clamp(angle, 0f, 90f);
        }

        public void SetGripOffset(Vector3 offset)
        {
            gripOffset = offset;
            transform.localPosition = offset;
        }

        private void OnEnable()
        {
            initialized = false;
            ApplyLight(IsBurnedOut ? 0f : 1f);
        }

        private void OnValidate()
        {
            ApplyLight(IsBurnedOut ? 0f : 1f);
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime, Time.time);
        }

        public void Tick(float dt, float time)
        {
            if (dt <= 0f) return;
            if (!emissionCached)
            {
                smokeEmission = smoke != null ? smoke.emission.rateOverTimeMultiplier : 0f;
                emissionCached = true;
            }
            burnAge = Mathf.Min(Mathf.Max(0.1f, lifetime), burnAge + dt);
            float age = Mathf.Clamp01(burnAge / Mathf.Max(0.1f, lifetime));
            float dying = Mathf.InverseLerp(1f - burnoutFraction, 1f, age);
            float fuel = 1f - Mathf.SmoothStep(0f, 1f, dying);
            Transform motionSource = character != null ? character : transform;
            if (!initialized)
            {
                previousPosition = motionSource.position;
                initialized = true;
            }
            Vector3 rawVelocity = Vector3.ClampMagnitude((motionSource.position - previousPosition) / dt, 10f);
            previousPosition = motionSource.position;
            filteredVelocity = Vector3.Lerp(filteredVelocity, rawVelocity, 1f - Mathf.Exp(-10f * dt));
            Vector3 localVelocity = motionSource.InverseTransformDirection(filteredVelocity);
            float motion = Mathf.Clamp01(filteredVelocity.magnitude / 6f);

            if (character != null)
            {
                // Retain a little gait swing, while keeping the flame clear of the face.
                if (upperArm != null)
                    upperArm.rotation = Quaternion.Slerp(upperArm.rotation,
                        character.rotation * Quaternion.Euler(-24f, 0f, armOutwardAngle), holdingPose);
                if (forearm != null)
                    forearm.localRotation = Quaternion.Slerp(forearm.localRotation,
                        Quaternion.Euler(-78f, 0f, 0f), holdingPose);
                transform.localPosition = gripOffset;
                Vector3 targetSway = new Vector3(-localVelocity.z, 0f, localVelocity.x) * (swayAngle / 6f);
                targetSway += new Vector3(Mathf.Sin(time * 2.1f), 0f, Mathf.Sin(time * 1.7f + 2f)) * motion * 2f;
                sway = Vector3.SmoothDamp(sway, targetSway, ref swayVelocity, swayResponse, Mathf.Infinity, dt);
                transform.rotation = character.rotation * Quaternion.Euler(sway);

            }

            float effectiveSpeed = flickerSpeed * (1f + 3f * dying);
            float slow = Mathf.PerlinNoise(17.4f, time * effectiveSpeed) * 2f - 1f;
            float fast = Mathf.PerlinNoise(time * effectiveSpeed * 2.7f, 41.2f) * 2f - 1f;
            float strength = Mathf.Lerp(flickerAmount, dyingFlickerAmount, dying) * (1f + movementFlicker * motion);
            float targetFlicker = Mathf.Clamp(1f + (slow * 0.75f + fast * 0.25f) * strength, 0.02f, 1.7f);
            flicker = Mathf.Lerp(flicker, targetFlicker, 1f - Mathf.Exp(-14f * dt));
            ApplyLight(flicker * fuel);

            if (fire != null)
            {
                var emission = fire.emission;
                emission.rateOverTime = 42f * flicker * fuel;
                var wind = fire.velocityOverLifetime;
                wind.x = -filteredVelocity.x * 0.12f;
                wind.z = -filteredVelocity.z * 0.12f;
            }
            if (smoke != null)
            {
                var emission = smoke.emission;
                emission.rateOverTimeMultiplier = smokeEmission * fuel;
                var wind = smoke.velocityOverLifetime;
                wind.x = -filteredVelocity.x * smokeTrail;
                wind.z = -filteredVelocity.z * smokeTrail;
            }
        }

        private void ApplyLight(float variation)
        {
            if (flameLight == null) return;
            flameLight.intensity = brightness * variation;
            flameLight.range = lightRange;
            flameLight.color = lightColor;
        }
    }
}
