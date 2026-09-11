using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Contextual treasure collection and torch handling with the available hand.</summary>
    [DefaultExecutionOrder(175)]
    public sealed class TorchInteraction : MonoBehaviour
    {
        private static readonly List<TorchInteraction> activeInteractions = new();

        [SerializeField] private HandheldTorch torch;
        [Header("Interaction")]
        [SerializeField, Range(0.4f, 2f)] private float pickupRange = 1f;
        [SerializeField, Range(0.3f, 2f)] private float pickupDuration = 0.75f;
        [SerializeField, Range(0.15f, 1f)] private float recoveryDuration = 0.45f;
        [SerializeField] private bool showPrompt = true;

        private Transform arm, elbow;
        private Treasure treasure;
        private bool collecting, useLeftHand;
        private CharacterInventory inventory;
        private Vector3 gripOffset;
        private TorchAttack attack;
        private CartoonCharacterGait gait;
        private bool recovering, attachedTorch;
        private float elapsed, weight, crouchWeight;
        private float recoveryStarted, recoveryArmWeight, recoveryCrouchWeight;
        private Vector3 reachTarget, recoveryLocalTarget;
        private Quaternion groundRotation;
        public bool IsBusy { get; private set; }
        /// <summary>The currently reachable item that E would collect, for contextual presentation.</summary>
        public Transform PickupTarget { get; private set; }

        /// <summary>Returns the eligible contextual pickup in the camera's scene without a scene search.</summary>
        public static Transform GetPickupTarget(Camera camera)
        {
            if (!Application.isPlaying || camera == null) return null;
            for (int i = 0; i < activeInteractions.Count; i++)
            {
                var interaction = activeInteractions[i];
                if (interaction == null || !interaction.isActiveAndEnabled
                    || interaction.gameObject.scene != camera.gameObject.scene) continue;
                if (interaction.PickupTarget != null) return interaction.PickupTarget;
            }
            return null;
        }

        public bool CanPickUp
        {
            get
            {
                RefreshPickupTarget();
                return PickupTarget != null && treasure == null;
            }
        }

        private void FindPickupTorch()
        {
            if (torch != null && torch.IsHeld && torch.Holder == transform) return;
            HandheldTorch nearest = null;
            float distance = pickupRange;
            foreach (var candidate in FindObjectsByType<HandheldTorch>(FindObjectsSortMode.None))
            {
                if (!candidate.isActiveAndEnabled || candidate.IsHeld || candidate.gameObject.scene != gameObject.scene) continue;
                float d = Vector3.Distance(transform.position, candidate.transform.position);
                if (d > distance || !HasClearReach(candidate)) continue;
                nearest = candidate;
                distance = d;
            }
            torch = nearest;
        }
        private bool HoldsTorch => torch != null && torch.IsHeld && torch.Holder == transform;

        private bool HasEligiblePickupTorch()
        {
            return !HoldsTorch && torch != null && !torch.IsHeld && torch.isActiveAndEnabled
                && Vector3.Distance(transform.position, torch.transform.position) <= pickupRange && HasClearReach(torch);
        }

        private void RefreshPickupTarget()
        {
            PickupTarget = null;
            if (!isActiveAndEnabled || IsBusy || (attack != null && attack.IsAttacking)
                || GetComponent<CharacterKnockdown>()?.IsDown == true) return;

            FindTreasure();
            if (treasure != null)
            {
                PickupTarget = treasure.transform;
                return;
            }

            FindPickupTorch();
            if (HasEligiblePickupTorch()) PickupTarget = torch.transform;
        }

        private void FindTreasure()
        {
            treasure = null;
            float distance = pickupRange;
            foreach (var candidate in Treasure.ActiveTreasures)
            {
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.gameObject.scene != gameObject.scene) continue;
                float d = Vector3.Distance(transform.position, candidate.transform.position);
                if (d > distance || !HasClearReach(candidate.transform)) continue;
                treasure = candidate;
                distance = d;
            }
        }
        private Transform Part(string partName)
        {
            foreach (var part in GetComponentsInChildren<Transform>())
                if (part.name == partName) return part;
            return null;
        }
        public string ActionLabel
        {
            get
            {
                RefreshPickupTarget();
                if (PickupTarget == null) return string.Empty;
                return treasure != null ? "Podnieś: " + treasure.DisplayName : "Podnieś: pochodnię";
            }
        }

        private void Awake()
        {
            if (torch == null) torch = GetComponentInChildren<HandheldTorch>();
            if (torch != null) { arm = torch.UpperArm; elbow = torch.Forearm; gripOffset = torch.GripOffset; }
            attack = GetComponent<TorchAttack>();
            gait = GetComponent<CartoonCharacterGait>();
            inventory = GetComponent<CharacterInventory>();
            if (inventory == null) inventory = gameObject.AddComponent<CharacterInventory>();
        }

        private void OnEnable()
        {
            activeInteractions.Add(this);
        }

        private bool HasClearReach(HandheldTorch target) => HasClearReach(target.transform);

        private bool HasClearReach(Transform target)
        {
            Vector3 origin = transform.position + Vector3.up * 0.45f;
            Vector3 delta = target.position - origin;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(target)) return false;
            }
            return true;
        }

        public bool TryInteract()
        {
            RefreshPickupTarget();
            if (PickupTarget == null) return false;
            collecting = treasure != null && PickupTarget == treasure.transform;
            useLeftHand = collecting && HoldsTorch;
            string side = useLeftHand ? "Left" : "Right";
            arm = Part(side + " Arm");
            elbow = Part(side + " Forearm");
            var hand = Part(side + " Hand");
            if (arm == null || elbow == null || hand == null) return false;
            gripOffset = hand.localPosition;
            if (!collecting && !HasEligiblePickupTorch()) return false;
            IsBusy = true;
            PickupTarget = null;
            recovering = attachedTorch = false;
            elapsed = weight = crouchWeight = 0f;
            reachTarget = collecting ? treasure.transform.position : torch.transform.position;
            return true;
        }

        private bool HasValidTarget()
        {
            if (!collecting) return HasEligiblePickupTorch();
            return treasure != null && treasure.isActiveAndEnabled
                && Vector3.Distance(transform.position, treasure.transform.position) <= pickupRange
                && HasClearReach(treasure.transform);
        }

        private void BeginRecovery()
        {
            recovering = true;
            recoveryStarted = elapsed;
            recoveryArmWeight = weight;
            recoveryCrouchWeight = crouchWeight;
            // Return with the player instead of dragging the arm toward a world-space point.
            recoveryLocalTarget = transform.InverseTransformPoint(reachTarget);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
            RefreshPickupTarget();
        }

        public void Tick(float dt)
        {
            if (!IsBusy) return;
            elapsed += Mathf.Max(0f, dt);
            // Once lost, this attempt stays cancelled even if the player comes back into range.
            if (!recovering && !HasValidTarget()) BeginRecovery();
            float duration = Mathf.Max(0.0001f, pickupDuration);
            float progress = elapsed / duration;
            float recovery = (elapsed - recoveryStarted) / Mathf.Max(0.0001f, recoveryDuration);
            // Prepare the body before reaching; bring the hand back before fully standing.
            // Both tracks still reach full contact at the original transfer deadline.
            weight = !recovering ? PoseEasing.Window(progress, 0.12f, 1f)
                : recoveryArmWeight * (1f - PoseEasing.Window(recovery, 0f, 0.9f));
            crouchWeight = !recovering ? PoseEasing.Window(progress, 0f, 0.85f)
                : recoveryCrouchWeight * (1f - PoseEasing.Window(recovery, 0.12f, 1f));
            if (!recovering)
                reachTarget = collecting ? treasure.transform.position : torch.transform.position;
            if (gait != null) gait.InteractionCrouch = crouchWeight;
            if (torch != null && !useLeftHand) torch.SetPoseSuppression(weight);
        }

        private void LateUpdate() => ApplyPose();

        public void ApplyPose()
        {
            if (!IsBusy) return;
            // Movement and target availability may have changed since Update.
            if (!recovering && !HasValidTarget()) BeginRecovery();
            // Refresh after Update as Rigidbody interpolation may have moved the visible grip.
            reachTarget = recovering ? transform.TransformPoint(recoveryLocalTarget)
                : collecting ? treasure.transform.position : torch.transform.position;
            // Two-bone reach keeps the hand on the physical grip until it is attached.
            Vector3 shoulder = arm.position;
            float upper = Vector3.Distance(shoulder, elbow.position);
            float lower = gripOffset.magnitude;
            Vector3 delta = reachTarget - shoulder;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(upper - lower) + 0.001f, upper + lower - 0.001f);
            Vector3 direction = delta.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(-transform.forward + transform.right * (useLeftHand ? -0.35f : 0.35f), direction).normalized;
            float along = (upper * upper - lower * lower + distance * distance) / (2f * distance);
            Vector3 joint = shoulder + direction * along + bend * Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
            arm.rotation = Quaternion.Slerp(arm.rotation, Quaternion.FromToRotation(Vector3.down, joint - shoulder), weight);
            elbow.rotation = Quaternion.Slerp(elbow.rotation, Quaternion.FromToRotation(Vector3.down, reachTarget - elbow.position), weight);

            if (!recovering && elapsed >= pickupDuration)
            {
                // Gameplay collection is independent of visual IK accuracy. Keep the selected
                // treasure locked, but recheck availability, range and obstruction at transfer.
                if (collecting)
                {
                    if (treasure != null && treasure.isActiveAndEnabled
                        && Vector3.Distance(transform.position, treasure.transform.position) <= pickupRange
                        && HasClearReach(treasure.transform))
                        inventory.Collect(treasure);
                }
                else if (HasEligiblePickupTorch() && Vector3.Distance(elbow.TransformPoint(gripOffset), reachTarget) < 0.08f)
                {
                    groundRotation = torch.transform.rotation;
                    torch.SetGripOffset(gripOffset);
                    torch.PickUp(transform, arm, elbow);
                    attack?.SetTorch(torch);
                    attachedTorch = true;
                }
                BeginRecovery();
            }
            if (attachedTorch && HoldsTorch)
                torch.transform.rotation = Quaternion.Slerp(groundRotation, transform.rotation, 1f - weight);
            if (recovering && elapsed - recoveryStarted >= recoveryDuration) Finish();
        }

        public void CancelInteraction() => Finish();

        private void Finish()
        {
            IsBusy = false;
            weight = crouchWeight = 0f;
            if (torch != null) torch.SetPoseSuppression(0f);
            if (gait != null) gait.InteractionCrouch = 0f;
        }

        private void OnDisable()
        {
            Finish();
            PickupTarget = null;
            activeInteractions.Remove(this);
        }

        private void OnGUI()
        {
            if (GetComponent<CharacterKnockdown>()?.IsDown == true) return;
            if (!showPrompt || IsBusy || (attack != null && attack.IsAttacking)) return;
            string label = ActionLabel;
            if (string.IsNullOrEmpty(label)) return;
            float width = Mathf.Min(420f, Screen.width - 24f);
            GUI.Box(new Rect((Screen.width - width) * 0.5f, Screen.height - 76f, width, 40f), "[E]  " + label);
        }
    }
}
