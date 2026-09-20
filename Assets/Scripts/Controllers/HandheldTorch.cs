using UnityEngine;

namespace CardsUnity.Controllers
{
    [DefaultExecutionOrder(100)]
    public sealed class HandheldTorch : MonoBehaviour
    {
        [Header("Balance")]
        [Tooltip("Optional shared torch balance. Existing fields below are used when empty.")]
        [SerializeField] private TorchBalanceProfile balanceProfile;

        [SerializeField] private Transform character;
        [SerializeField] private Transform upperArm;
        [SerializeField] private Transform forearm;
        [SerializeField] private Light flameLight;
        [SerializeField] private ParticleSystem fire;
        [SerializeField] private ParticleSystem smoke;

        [Header("Local fallback — Illumination")]
        [Tooltip("Brightness of the torch. Zero switches off its illumination.")]
        [SerializeField, Range(0f, 15f)] private float brightness = 5f;
        [Tooltip("Maximum light reach in metres.")]
        [SerializeField, Range(1f, 20f)] private float lightRange = 8f;
        [Header("Per-instance flame appearance")]
        [SerializeField] private Color lightColor = new Color(1f, 0.48f, 0.16f);
        [SerializeField, Range(0f, 0.6f)] private float flickerAmount = 0.16f;
        [SerializeField, Range(0.1f, 8f)] private float flickerSpeed = 2.5f;
        [SerializeField, Range(0f, 1f)] private float movementFlicker = 0.3f;

        [Header("Local fallback — Burn lifetime")]
        [Tooltip("Burn duration in seconds of active gameplay.")]
        [SerializeField, Min(0.1f)] private float lifetime = 180f;
        [Tooltip("Fraction of lifetime spent gradually fading out.")]
        [SerializeField, Range(0.05f, 1f)] private float burnoutFraction = 0.4f;
        [Header("Per-instance burnout appearance")]
        [SerializeField, Range(0f, 2f)] private float dyingFlickerAmount = 1.4f;
        [Header("Local fallback — Impact fuel costs")]
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
        public TorchBalanceProfile BalanceProfile => balanceProfile;
        private TorchIlluminationBalance IlluminationBalance => balanceProfile != null ? balanceProfile.Illumination : null;
        private TorchFuelBalance FuelBalance => balanceProfile != null ? balanceProfile.Fuel : null;
        private float Lifetime => FuelBalance?.Lifetime ?? lifetime;
        public float DropLifetimeCost => FuelBalance?.DropLifetimeCost ?? dropLifetimeCost;
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
            burnAge = Mathf.Min(Mathf.Max(0.1f, Lifetime), burnAge + Mathf.Max(0f, DropLifetimeCost));
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

        private static readonly System.Collections.Generic.List<HandheldTorch> activeTorches = new();

        /// <summary>Per-camera presentation input; corpse and dropped torches never own the effect.</summary>
        public static Vector4 GetLivingShadowSource() => GetLivingShadowSource(out _);

        public static Vector4 GetLivingShadowSource(out float lightFraction)
        {
            lightFraction = 1f;
            foreach (var torch in activeTorches)
            {
                if (torch == null || !torch.isActiveAndEnabled || !torch.IsHeld ||
                    torch.character.GetComponent<ProceduralCharacter>() == null) continue;
                lightFraction = 1f - Mathf.SmoothStep(0f, 1f, torch.BurnoutProgress);
                var position = torch.character.position;
                return new Vector4(position.x, position.y, position.z,
                    Mathf.Clamp01(torch.RemainingLifetime / Mathf.Max(0.1f, torch.Lifetime)));
            }
            return new Vector4(0, 0, 0, -1);
        }

        private void OnDisable() => activeTorches.Remove(this);

        private bool waterExtinguished;
        private Vector3 previousFlamePosition;
        private int previousWaterFrame = -2;
        private static int waterCheckFrame = -1;

        /// <summary>Checks final flame poses once per frame, after attack and pickup overlays.</summary>
        public static void CheckWaterContacts()
        {
            if (waterCheckFrame == Time.frameCount) return;
            waterCheckFrame = Time.frameCount;
            foreach (var torch in activeTorches)
            {
                if (torch == null || torch.IsBurnedOut) continue;
                Vector3 current = torch.fire != null ? torch.fire.transform.position
                    : torch.flameLight != null ? torch.flameLight.transform.position
                    : torch.transform.TransformPoint(new Vector3(0f, 0.62f, 0f));
                Vector3 previous = torch.previousWaterFrame == Time.frameCount - 1
                    ? torch.previousFlamePosition : current;
                if (CaveWater.TouchesWater(previous, current, 0.08f)) torch.Extinguish();
                torch.previousFlamePosition = current;
                torch.previousWaterFrame = Time.frameCount;
            }
        }

        /// <summary>Water permanently extinguishes this instance for the current session.</summary>
        public void Extinguish()
        {
            waterExtinguished = true;
            ApplyLight(0f);
            if (fire != null) fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private float BurnoutProgress => waterExtinguished ? 1f : Mathf.InverseLerp(1f - (FuelBalance?.BurnoutFraction ?? burnoutFraction),
            1f, Mathf.Clamp01(burnAge / Mathf.Max(0.1f, Lifetime)));

        public float RemainingLifetime => waterExtinguished ? 0f : Mathf.Max(0f, Lifetime - burnAge);

        /// <summary>Seconds of burn lifetime a single strike costs.</summary>
        public float StrikeLifetimeCost => FuelBalance?.StrikeLifetimeCost ?? strikeLifetimeCost;

        /// <summary>
        /// Charges one strike against the remaining fuel. Ages the torch exactly as burning for
        /// <see cref="StrikeLifetimeCost"/> seconds would, so it can also burn out mid-swing.
        /// </summary>
        public void ConsumeStrikeFuel()
        {
            float cost = StrikeLifetimeCost;
            if (cost <= 0f) return;
            burnAge = Mathf.Min(Mathf.Max(0.1f, Lifetime), burnAge + cost);
        }
        public bool IsBurnedOut => RemainingLifetime <= 0f;
        private float burnAge;
        private float smokeEmission;
        private bool emissionCached;
        private float presentationMultiplier = 1f;
        private float lastLightVariation = 1f;

        /// <summary>
        /// Visual-only multiplier used by presentations such as the story intro. It affects the
        /// light, flame and smoke without changing fuel, balance values or remaining lifetime.
        /// </summary>
        public float PresentationMultiplier => presentationMultiplier;

        public void SetPresentationMultiplier(float multiplier)
        {
            presentationMultiplier = Mathf.Clamp01(multiplier);
            CacheEmissionRates();
            ApplyLight(lastLightVariation);
            ApplyParticlePresentation();
        }

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
            CacheEmissionRates();
        }

        private void OnEnable()
        {
            if (!activeTorches.Contains(this)) activeTorches.Add(this);
            initialized = false;
            previousWaterFrame = -2;
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
            CacheEmissionRates();
            float effectiveLifetime = Mathf.Max(0.1f, Lifetime);
            burnAge = Mathf.Min(effectiveLifetime, burnAge + dt);
            float dying = BurnoutProgress;
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

            ApplyParticlePresentation();
            if (fire != null)
            {
                var wind = fire.velocityOverLifetime;
                wind.x = -filteredVelocity.x * 0.12f;
                wind.z = -filteredVelocity.z * 0.12f;
            }
            if (smoke != null)
            {
                var wind = smoke.velocityOverLifetime;
                wind.x = -filteredVelocity.x * smokeTrail;
                wind.z = -filteredVelocity.z * smokeTrail;
            }
        }

        private void ApplyLight(float variation)
        {
            lastLightVariation = variation;
            if (flameLight == null) return;
            flameLight.intensity = (IlluminationBalance?.Brightness ?? brightness) * variation * presentationMultiplier;
            flameLight.range = IlluminationBalance?.LightRange ?? lightRange;
            flameLight.color = lightColor;
        }

        private void CacheEmissionRates()
        {
            if (emissionCached) return;
            smokeEmission = smoke != null ? smoke.emission.rateOverTimeMultiplier : 0f;
            emissionCached = true;
        }

        private void ApplyParticlePresentation()
        {
            float fuel = 1f - Mathf.SmoothStep(0f, 1f, BurnoutProgress);
            if (fire != null)
            {
                var emission = fire.emission;
                emission.rateOverTime = 42f * flicker * fuel * presentationMultiplier;
            }
            if (smoke != null)
            {
                var emission = smoke.emission;
                emission.rateOverTimeMultiplier = smokeEmission * fuel * presentationMultiplier;
            }
        }

#if UNITY_EDITOR
        internal void CopyLegacyBalanceTo(TorchIlluminationBalance illumination, TorchFuelBalance fuel)
        {
            illumination.Capture(brightness, lightRange);
            fuel.Capture(lifetime, burnoutFraction, strikeLifetimeCost, dropLifetimeCost);
        }

        internal void AssignBalanceProfile(TorchBalanceProfile profile)
        {
            balanceProfile = profile;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
