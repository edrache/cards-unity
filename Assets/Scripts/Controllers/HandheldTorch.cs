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
        [Tooltip("Seconds of burn lifetime spent on a single strike. Zero makes hitting free.")]
        [SerializeField, Range(0f, 60f)] private float strikeLifetimeCost = 5f;

        [Tooltip("Seconds of fuel lost on the first collision after each drop. Zero makes dropping free.")]
        [SerializeField, Min(0f)] private float dropLifetimeCost = 10f;
        private Rigidbody dropBody;
        private CapsuleCollider dropCollider;
        private bool impactPending;
        public bool IsHeld => character != null;
        public bool IsSettled => !IsHeld && !impactPending && dropBody != null
            && (dropBody.IsSleeping() || dropBody.linearVelocity.sqrMagnitude < 0.04f);
        public float DropLifetimeCost => dropLifetimeCost;
        public Vector3 GripOffset => gripOffset;

        public void Drop()
        {
            if (!IsHeld) return;
            Transform previousHolder = character;
            character = null;
            transform.SetParent(null, true);
            if (dropCollider == null)
            {
                dropCollider = gameObject.AddComponent<CapsuleCollider>();
                dropCollider.center = new Vector3(0f, 0.3f, 0f);
                dropCollider.height = 0.72f;
                dropCollider.radius = 0.11f;
            }
            dropCollider.enabled = true;
            if (dropBody == null) dropBody = gameObject.AddComponent<Rigidbody>();
            dropBody.mass = 0.6f;
            dropBody.linearDamping = 0.4f;
            dropBody.angularDamping = 1.5f;
            dropBody.isKinematic = false;
            dropBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            dropBody.interpolation = RigidbodyInterpolation.Interpolate;
            dropBody.linearVelocity = previousHolder.forward * 0.65f;
            dropBody.angularVelocity = previousHolder.right * 2.5f;
            foreach (var collider in previousHolder.GetComponentsInChildren<Collider>())
                if (collider != dropCollider) Physics.IgnoreCollision(dropCollider, collider);
            impactPending = true;
            initialized = false;
            poseSuppression = 0f;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!impactPending || IsHeld) return;
            impactPending = false;
            burnAge = Mathf.Min(Mathf.Max(0.1f, lifetime), burnAge + Mathf.Max(0f, dropLifetimeCost));
        }

        public void PickUp(Transform holder, Transform arm, Transform elbow)
        {
            if (holder == null || arm == null || elbow == null) return;
            if (dropBody != null)
            {
                dropBody.linearVelocity = Vector3.zero;
                dropBody.angularVelocity = Vector3.zero;
                dropBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                dropBody.isKinematic = true;
            }
            if (dropCollider != null) dropCollider.enabled = false;
            impactPending = false;
            character = holder;
            upperArm = arm;
            forearm = elbow;
            transform.SetParent(elbow, true);
            transform.localPosition = gripOffset;
            transform.localRotation = restLocalRotation;
            initialized = false;
        }

        public float RemainingLifetime => Mathf.Max(0f, lifetime - burnAge);

        /// <summary>Seconds of burn lifetime a single strike costs.</summary>
        public float StrikeLifetimeCost => strikeLifetimeCost;

        /// <summary>
        /// Charges one strike against the remaining fuel. Ages the torch exactly as burning for
        /// <see cref="StrikeLifetimeCost"/> seconds would, so it can also burn out mid-swing.
        /// </summary>
        public void ConsumeStrikeFuel()
        {
            if (strikeLifetimeCost <= 0f) return;
            burnAge = Mathf.Min(Mathf.Max(0.1f, lifetime), burnAge + strikeLifetimeCost);
        }
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
        private float poseSuppression;
        private Quaternion restLocalRotation = Quaternion.identity;

        public Transform UpperArm => upperArm;
        public Transform Forearm => forearm;
        public Transform Holder => character;

        /// <summary>
        /// Fades out the holding pose and the upright torch lock, so another animation such as
        /// <see cref="TorchAttack"/> can take the arm over and drag the torch along with the forearm.
        /// </summary>
        public void SetPoseSuppression(float weight)
        {
            poseSuppression = Mathf.Clamp01(weight);
        }

        public void SetArmOutwardAngle(float angle)
        {
            armOutwardAngle = Mathf.Clamp(angle, 0f, 90f);
        }

        public void SetGripOffset(Vector3 offset)
        {
            gripOffset = offset;
            transform.localPosition = offset;
        }

        private void Awake()
        {
            restLocalRotation = transform.localRotation;
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
                float pose = holdingPose * (1f - poseSuppression);
                if (upperArm != null)
                    upperArm.rotation = Quaternion.Slerp(upperArm.rotation,
                        character.rotation * Quaternion.Euler(-24f, 0f, armOutwardAngle), pose);
                if (forearm != null)
                    forearm.localRotation = Quaternion.Slerp(forearm.localRotation,
                        Quaternion.Euler(-78f, 0f, 0f), pose);
                transform.localPosition = gripOffset;
                Vector3 targetSway = new Vector3(-localVelocity.z, 0f, localVelocity.x) * (swayAngle / 6f);
                targetSway += new Vector3(Mathf.Sin(time * 2.1f), 0f, Mathf.Sin(time * 1.7f + 2f)) * motion * 2f;
                sway = Vector3.SmoothDamp(sway, targetSway, ref swayVelocity, swayResponse, Mathf.Infinity, dt);
                Quaternion upright = character.rotation * Quaternion.Euler(sway);
                if (poseSuppression <= 0.0001f || transform.parent == null)
                {
                    transform.rotation = upright;
                }
                else
                {
                    // A suppressed pose lets the torch ride the forearm instead of staying upright.
                    Quaternion carried = transform.parent.rotation * restLocalRotation;
                    transform.rotation = Quaternion.Slerp(carried, upright, 1f - poseSuppression);
                }
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
