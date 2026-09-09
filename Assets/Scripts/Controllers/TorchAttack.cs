using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>
    /// Procedural overhead torch swing for the right arm. Runs after <see cref="HandheldTorch"/>
    /// so the strike wins over the holding pose, and suppresses that pose while it plays so the
    /// torch follows the forearm instead of staying locked upright.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class TorchAttack : MonoBehaviour
    {
        [Header("Rig")]
        [Tooltip("Torch whose holding pose is suppressed during a swing. Resolved from the children when empty.")]
        [SerializeField] private HandheldTorch torch;
        [Tooltip("Right upper arm. Taken from the torch when empty.")]
        [SerializeField] private Transform upperArm;
        [Tooltip("Right forearm. Taken from the torch when empty.")]
        [SerializeField] private Transform forearm;
        [Tooltip("Character root used as the pose reference frame. Falls back to this transform.")]
        [SerializeField] private Transform character;
        [Tooltip("Optional chest joint. A small twist is layered on top of the procedural spine.")]
        [SerializeField] private Transform chest;

        [Header("Timing")]
        [Tooltip("Seconds spent raising the torch behind the head.")]
        [SerializeField, Range(0.02f, 1f)] private float windupDuration = 0.14f;
        [Tooltip("Seconds of the downward strike. The hit window is open for exactly this long.")]
        [SerializeField, Range(0.02f, 1f)] private float strikeDuration = 0.10f;
        [Tooltip("Seconds spent handing the arm back to the torch holding pose. No new swing starts before it ends.")]
        [SerializeField, Range(0.02f, 2f)] private float recoverDuration = 0.30f;

        [Header("Pose")]
        [Tooltip("Upper arm pitch at the top of the windup, in degrees. Measured on the Shadow Knight rig, this puts the hand above and behind the head.")]
        [SerializeField, Range(-180f, 180f)] private float windupPitch = -170f;
        [Tooltip("Upper arm pitch at the end of the strike, in degrees. Measured on the Shadow Knight rig, this puts the hand low and in front.")]
        [SerializeField, Range(-180f, 180f)] private float strikePitch = -40f;
        [Tooltip("Elbow bend at the top of the windup, in degrees.")]
        [SerializeField, Range(0f, 145f)] private float windupElbow = 110f;
        [Tooltip("Elbow bend at the end of the strike, in degrees. Low values extend the arm.")]
        [SerializeField, Range(0f, 145f)] private float strikeElbow = 10f;
        [Tooltip("Outward elevation of the swinging arm, in degrees.")]
        [SerializeField, Range(0f, 90f)] private float swingOutwardAngle = 8f;
        [Tooltip("Chest twist supporting the swing, in degrees. Zero keeps the spine untouched.")]
        [SerializeField, Range(0f, 30f)] private float chestTwist = 8f;

        /// <summary>Raised once, when the strike phase begins. Intended as the future hit window opening.</summary>
        public event System.Action Strike;

        /// <summary>True while a swing is playing, including the recovery that blocks the next one.</summary>
        public bool IsAttacking => active;

        /// <summary>True only during the strike phase, between windup and recovery.</summary>
        public bool HitWindowOpen => active && elapsed >= windupDuration && elapsed < windupDuration + strikeDuration;

        /// <summary>Progress through the whole swing, from zero at the first frame to one when it ends.</summary>
        public float NormalizedTime => active ? Mathf.Clamp01(elapsed / TotalDuration) : 0f;

        /// <summary>Blend weight of the attack pose over the torch holding pose.</summary>
        public float PoseWeight => weight;

        private float TotalDuration => windupDuration + strikeDuration + recoverDuration;

        private bool active;
        private bool strikeRaised;
        private bool chestWritten;
        private Quaternion chestBase = Quaternion.identity;
        private Quaternion chestApplied = Quaternion.identity;
        private float elapsed;
        private float weight;
        private float swing;

        private void Awake()
        {
            ResolveRig();
        }

        private void OnValidate()
        {
            ResolveRig();
        }

        private void ResolveRig()
        {
            if (torch == null) torch = GetComponentInChildren<HandheldTorch>(true);
            if (torch != null)
            {
                if (upperArm == null) upperArm = torch.UpperArm;
                if (forearm == null) forearm = torch.Forearm;
                if (character == null) character = torch.Holder;
            }
            if (character == null) character = transform;
        }

        /// <summary>
        /// Starts a swing. Presses during an ongoing swing are ignored rather than buffered.
        /// </summary>
        public bool TryStrike()
        {
            if (active) return false;
            active = true;
            strikeRaised = false;
            elapsed = 0f;
            weight = 0f;
            swing = 0f;
            return true;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Advances the swing and pushes the pose suppression to the torch. Kept public so the
        /// timeline can be stepped deterministically without depending on frames.
        /// </summary>
        public void Tick(float dt)
        {
            if (active && dt > 0f)
            {
                elapsed += dt;
                if (!strikeRaised && elapsed >= windupDuration)
                {
                    strikeRaised = true;
                    Strike?.Invoke();
                }
                if (elapsed >= TotalDuration)
                {
                    active = false;
                    elapsed = 0f;
                    weight = 0f;
                    swing = 0f;
                }
                else
                {
                    Evaluate();
                }
            }
            if (torch != null) torch.SetPoseSuppression(weight);
        }

        private void Evaluate()
        {
            if (elapsed < windupDuration)
            {
                // The weight ramp itself carries the arm from the holding pose up into the windup.
                weight = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(0.0001f, windupDuration));
                swing = 0f;
                return;
            }
            float sinceWindup = elapsed - windupDuration;
            if (sinceWindup < strikeDuration)
            {
                weight = 1f;
                float u = sinceWindup / Mathf.Max(0.0001f, strikeDuration);
                // Ease out, so the torch leaves the top fast and settles into the follow-through.
                float remaining = 1f - u;
                swing = 1f - remaining * remaining * remaining;
                return;
            }
            float recovery = (sinceWindup - strikeDuration) / Mathf.Max(0.0001f, recoverDuration);
            weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(recovery));
            swing = 1f;
        }

        private void LateUpdate()
        {
            ApplyPose();
        }

        /// <summary>Writes the swing pose over whatever the gait and the torch produced this frame.</summary>
        public void ApplyPose()
        {
            if (weight <= 0.0001f)
            {
                RestoreChest();
                return;
            }
            Quaternion reference = character != null ? character.rotation : transform.rotation;
            if (upperArm != null)
            {
                float pitch = Mathf.Lerp(windupPitch, strikePitch, swing);
                Quaternion target = reference * Quaternion.Euler(pitch, 0f, swingOutwardAngle);
                upperArm.rotation = Quaternion.Slerp(upperArm.rotation, target, weight);
            }
            if (forearm != null)
            {
                float elbow = Mathf.Lerp(windupElbow, strikeElbow, swing);
                forearm.localRotation = Quaternion.Slerp(forearm.localRotation,
                    Quaternion.Euler(-elbow, 0f, 0f), weight);
            }
            if (chest != null && chestTwist > 0f)
            {
                // Layered on top of the procedural spine, which normally writes this joint every
                // frame. When nothing else did, the previous overlay is undone first, otherwise the
                // twist would accumulate and drag the shoulder along with it.
                Quaternion incoming = chest.localRotation;
                if (chestWritten && Quaternion.Angle(incoming, chestApplied) < 0.01f) incoming = chestBase;
                float twist = Mathf.Lerp(chestTwist, -chestTwist, swing) * weight;
                chestBase = incoming;
                chestApplied = incoming * Quaternion.Euler(0f, twist, 0f);
                chest.localRotation = chestApplied;
                chestWritten = true;
            }
        }

        private void RestoreChest()
        {
            if (!chestWritten || chest == null) return;
            if (Quaternion.Angle(chest.localRotation, chestApplied) < 0.01f) chest.localRotation = chestBase;
            chestWritten = false;
        }
    }
}
