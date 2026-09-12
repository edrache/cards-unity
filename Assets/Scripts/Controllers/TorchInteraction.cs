using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CardsUnity.Controllers
{
    /// <summary>Contextual treasure, torch and boulder handling with the available hand.</summary>
    [DefaultExecutionOrder(175)]
    public sealed class TorchInteraction : MonoBehaviour
    {
        private static readonly List<TorchInteraction> activeInteractions = new();

        [SerializeField] private HandheldTorch torch;
        [Header("Balance")]
        [Tooltip("Optional shared player balance. Carry placement and prompt presentation stay on this instance.")]
        [SerializeField] private PlayerGameplayBalanceProfile balanceProfile;
        [Header("Local fallback — Interaction")]
        [SerializeField, Range(0.4f, 2f)] private float pickupRange = 1f;
        [SerializeField, Range(0.3f, 2f)] private float pickupDuration = 0.75f;
        [SerializeField, Range(0.15f, 1f)] private float recoveryDuration = 0.45f;
        [Header("Per-instance presentation")]
        [SerializeField] private bool showPrompt = true;

        [Header("Boulder carrying")]
        [SerializeField, Range(0.2f, 1.5f)] private float boulderCarryHeight = 0.72f;
        [SerializeField, Range(0f, 0.8f)] private float boulderCarryForward = 0.3f;
        [SerializeField, Range(0f, 0.5f)] private float boulderSideClearance = 0.16f;

        private TextMeshProUGUI prompt;
        private RectTransform promptPanel;
        private Transform arm, elbow;
        private Treasure treasure;
        private CaveBoulder boulder;
        private bool collecting, liftingBoulder, carryingBoulder, useLeftHand, interactHeld;
        private CharacterInventory inventory;
        private CaveExit caveExit;
        private Vector3 gripOffset;
        private TorchAttack attack;
        private CartoonCharacterGait gait;
        private bool recovering, attachedTorch;
        private float elapsed, weight, crouchWeight;
        private float recoveryStarted, recoveryArmWeight, recoveryCrouchWeight;
        private Vector3 reachTarget, recoveryLocalTarget;
        private Quaternion groundRotation;
        private Vector3 boulderLiftStart;
        private Quaternion boulderCarryLocalRotation;
        public bool IsBusy { get; private set; }
        public PlayerGameplayBalanceProfile BalanceProfile => balanceProfile;
        private PlayerInteractionBalance InteractionBalance => balanceProfile != null ? balanceProfile.Interaction : null;
        private float PickupRange => InteractionBalance?.PickupRange ?? pickupRange;
        private float PickupDuration => InteractionBalance?.PickupDuration ?? pickupDuration;
        private float RecoveryDuration => InteractionBalance?.RecoveryDuration ?? recoveryDuration;
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

        /// <summary>True when E would finish the run at the cave entrance.</summary>
        public bool CanExit
        {
            get
            {
                RefreshPickupTarget();
                return PickupTarget == null && CanUseExit();
            }
        }

        private void FindPickupTorch()
        {
            if (torch != null && torch.IsHeld && torch.Holder == transform) return;
            HandheldTorch nearest = null;
            float distance = PickupRange;
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
                && Vector3.Distance(transform.position, torch.transform.position) <= PickupRange && HasClearReach(torch);
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
            if (HasEligiblePickupTorch())
            {
                PickupTarget = torch.transform;
                return;
            }

            FindBoulder();
            if (boulder != null) PickupTarget = boulder.transform;
        }

        private void FindTreasure()
        {
            treasure = null;
            float distance = PickupRange;
            foreach (var candidate in Treasure.ActiveTreasures)
            {
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.gameObject.scene != gameObject.scene) continue;
                float d = Vector3.Distance(transform.position, candidate.transform.position);
                if (d > distance || !HasClearReach(candidate.transform)) continue;
                treasure = candidate;
                distance = d;
            }
        }

        private void FindBoulder()
        {
            boulder = null;
            float distance = PickupRange;
            foreach (var candidate in CaveBoulder.ActiveBoulders)
            {
                if (candidate == null || !candidate.isActiveAndEnabled || !candidate.CanPickUp
                    || candidate.IsHeld || candidate.gameObject.scene != gameObject.scene) continue;
                float d = DistanceToBoulder(candidate);
                if (d > distance || !HasClearReach(candidate.transform)) continue;
                boulder = candidate;
                distance = d;
            }
        }

        private float DistanceToBoulder(CaveBoulder candidate)
        {
            return Mathf.Max(0f, Vector3.Distance(transform.position, candidate.ReachPoint) - candidate.Radius);
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
                if (carryingBoulder) return "Puść, aby upuścić głaz";
                RefreshPickupTarget();
                if (PickupTarget == null) return CanUseExit() ? "Wyjdź z jaskini" : string.Empty;
                if (treasure != null) return "Podnieś: " + treasure.DisplayName;
                return boulder != null && PickupTarget == boulder.transform
                    ? "Przytrzymaj, aby podnieść głaz" : "Podnieś: pochodnię";
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
            caveExit = GetComponent<CaveExit>();
            if (caveExit == null) caveExit = gameObject.AddComponent<CaveExit>();
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
            if (PickupTarget == null) return CanUseExit() && caveExit.TryExit(inventory);
            collecting = treasure != null && PickupTarget == treasure.transform;
            liftingBoulder = boulder != null && PickupTarget == boulder.transform;
            useLeftHand = (collecting || liftingBoulder) && HoldsTorch;
            string side = useLeftHand ? "Left" : "Right";
            arm = Part(side + " Arm");
            elbow = Part(side + " Forearm");
            var hand = Part(side + " Hand");
            if (arm == null || elbow == null || hand == null) return false;
            gripOffset = hand.localPosition;
            if (!collecting && !liftingBoulder && !HasEligiblePickupTorch()) return false;
            IsBusy = true;
            PickupTarget = null;
            recovering = attachedTorch = carryingBoulder = false;
            elapsed = weight = crouchWeight = 0f;
            reachTarget = CurrentGroundTarget();
            return true;
        }

        /// <summary>Supplies the current state of the contextual interaction button.</summary>
        public void SetInteractHeld(bool held)
        {
            interactHeld = held;
        }

        private bool CanUseExit()
        {
            return isActiveAndEnabled && !IsBusy && (attack == null || !attack.IsAttacking)
                && GetComponent<CharacterKnockdown>()?.IsDown != true
                && caveExit != null && caveExit.CanExit;
        }

        private bool HasValidTarget()
        {
            if (liftingBoulder)
                return boulder != null && boulder.isActiveAndEnabled && boulder.CanPickUp && !boulder.IsHeld
                    && DistanceToBoulder(boulder) <= PickupRange
                    && HasClearReach(boulder.transform);
            if (!collecting) return HasEligiblePickupTorch();
            return treasure != null && treasure.isActiveAndEnabled
                   && Vector3.Distance(transform.position, treasure.transform.position) <= PickupRange
                   && HasClearReach(treasure.transform);
        }

        private Vector3 CurrentGroundTarget()
        {
            if (collecting) return treasure.transform.position;
            return liftingBoulder ? boulder.ReachPoint : torch.transform.position;
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
            UpdatePrompt();
        }

        public void Tick(float dt)
        {
            if (!IsBusy) return;
            elapsed += Mathf.Max(0f, dt);
            if (liftingBoulder && !recovering && !interactHeld)
            {
                ReleaseBoulder();
                BeginRecovery();
            }
            // Once lost, this attempt stays cancelled even if the player comes back into range.
            if (!recovering && !carryingBoulder && !HasValidTarget()) BeginRecovery();
            float duration = Mathf.Max(0.0001f, PickupDuration);
            float progress = elapsed / duration;
            float recovery = (elapsed - recoveryStarted) / Mathf.Max(0.0001f, RecoveryDuration);
            // Prepare the body before reaching; bring the hand back before fully standing.
            // Both tracks still reach full contact at the original transfer deadline.
            weight = !recovering ? PoseEasing.Window(progress, 0.12f, 1f)
                : recoveryArmWeight * (1f - PoseEasing.Window(recovery, 0f, 0.9f));
            float lift = carryingBoulder
                ? PoseEasing.Window((elapsed - PickupDuration) / Mathf.Max(0.0001f, RecoveryDuration), 0f, 1f) : 0f;
            crouchWeight = !recovering ? PoseEasing.Window(progress, 0f, 0.85f) * (1f - lift)
                : recoveryCrouchWeight * (1f - PoseEasing.Window(recovery, 0.12f, 1f));
            if (!recovering && !carryingBoulder) reachTarget = CurrentGroundTarget();
            if (gait != null) gait.InteractionCrouch = crouchWeight;
            if (torch != null && !useLeftHand) torch.SetPoseSuppression(weight);
        }

        private void LateUpdate() => ApplyPose();

        public void ApplyPose()
        {
            if (!IsBusy) return;
            // Movement and target availability may have changed since Update.
            if (!recovering && !carryingBoulder && !HasValidTarget()) BeginRecovery();
            if (!recovering && carryingBoulder && !OwnsCarriedBoulder())
            {
                carryingBoulder = false;
                BeginRecovery();
            }
            else if (!recovering && carryingBoulder)
            {
                // A blocked sweep leaves the boulder at its last safe position. Keep the
                // hold active while it remains within reach, so the player can step back
                // and continue without clipping it or holding it remotely through a wall.
                bool moved = MoveCarriedBoulder();
                if (!moved && !IsCarriedBoulderWithinReach())
                {
                    ReleaseBoulder();
                    BeginRecovery();
                }
            }
            // Refresh after Update as Rigidbody interpolation may have moved the visible grip.
            reachTarget = recovering ? transform.TransformPoint(recoveryLocalTarget)
                : carryingBoulder ? BoulderHandTarget() : CurrentGroundTarget();
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

            if (!recovering && !carryingBoulder && elapsed >= PickupDuration)
            {
                // Gameplay collection is independent of visual IK accuracy. Keep the selected
                // treasure locked, but recheck availability, range and obstruction at transfer.
                if (liftingBoulder)
                {
                    if (HasValidTarget() && boulder.TryCarry(transform))
                    {
                        carryingBoulder = true;
                        boulderLiftStart = boulder.ReachPoint;
                        boulderCarryLocalRotation = Quaternion.Inverse(transform.rotation) * boulder.transform.rotation;
                    }
                    else BeginRecovery();
                }
                else if (collecting)
                {
                    if (treasure != null && treasure.isActiveAndEnabled
                        && Vector3.Distance(transform.position, treasure.transform.position) <= PickupRange
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
                if (!liftingBoulder) BeginRecovery();
            }
            if (attachedTorch && HoldsTorch)
                torch.transform.rotation = Quaternion.Slerp(groundRotation, transform.rotation, 1f - weight);
            if (recovering && elapsed - recoveryStarted >= RecoveryDuration) Finish();
        }

        public void CancelInteraction() => Finish();

        private bool MoveCarriedBoulder()
        {
            if (!OwnsCarriedBoulder()) return false;
            float lift = PoseEasing.Window((elapsed - PickupDuration) / Mathf.Max(0.0001f, RecoveryDuration), 0f, 1f);
            float side = useLeftHand ? -1f : 1f;
            Vector3 carryLocal = new Vector3(side * (boulderSideClearance + boulder.Radius * 0.25f),
                Mathf.Max(boulderCarryHeight, boulder.Radius + 0.25f), boulder.Radius + boulderCarryForward);
            Vector3 position = Vector3.Lerp(boulderLiftStart, transform.TransformPoint(carryLocal), lift);
            Quaternion rotation = Quaternion.Slerp(boulder.transform.rotation,
                transform.rotation * boulderCarryLocalRotation, lift);
            return boulder.MoveCarried(position, rotation);
        }

        private bool OwnsCarriedBoulder()
        {
            return boulder != null && boulder.IsHeld && boulder.Holder == transform;
        }

        private bool IsCarriedBoulderWithinReach()
        {
            if (!OwnsCarriedBoulder()) return false;
            Vector3 reachOrigin = transform.position + transform.up * 0.72f;
            return Vector3.Distance(reachOrigin, boulder.ReachPoint) <= boulder.Radius + 1.1f;
        }

        private Vector3 BoulderHandTarget()
        {
            Vector3 center = boulder != null ? boulder.ReachPoint : reachTarget;
            Vector3 towardShoulder = arm != null ? arm.position - center : Vector3.up;
            return center + towardShoulder.normalized * (boulder != null ? boulder.Radius * 0.72f : 0f);
        }

        private void ReleaseBoulder()
        {
            if (carryingBoulder && OwnsCarriedBoulder())
                boulder.Drop();
            carryingBoulder = false;
        }

        private void Finish()
        {
            ReleaseBoulder();
            IsBusy = false;
            collecting = liftingBoulder = recovering = attachedTorch = false;
            weight = crouchWeight = 0f;
            if (torch != null) torch.SetPoseSuppression(0f);
            if (gait != null) gait.InteractionCrouch = 0f;
        }

        private void OnDisable()
        {
            Finish();
            interactHeld = false;
            PickupTarget = null;
            activeInteractions.Remove(this);
            if (promptPanel != null) promptPanel.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            Finish();
            if (promptPanel != null) Destroy(promptPanel.gameObject);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && liftingBoulder) Finish();
        }

#if UNITY_EDITOR
        internal void CopyLegacyBalanceTo(PlayerInteractionBalance destination)
        {
            destination.Capture(pickupRange, pickupDuration, recoveryDuration);
        }

        internal void AssignBalanceProfile(PlayerGameplayBalanceProfile profile)
        {
            balanceProfile = profile;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void UpdatePrompt()
        {
            string label = showPrompt ? ActionLabel : string.Empty;
            if (string.IsNullOrEmpty(label))
            {
                if (promptPanel != null) promptPanel.gameObject.SetActive(false);
                return;
            }
            if (prompt == null)
            {
                if (inventory == null || inventory.UiCanvas == null) return;
                var panel = new GameObject("Pickup Prompt", typeof(RectTransform), typeof(Image));
                panel.layer = LayerMask.NameToLayer("UI");
                promptPanel = panel.GetComponent<RectTransform>();
                promptPanel.SetParent(inventory.UiCanvas.transform, false);
                promptPanel.anchorMin = promptPanel.anchorMax = new Vector2(0.5f, 0);
                promptPanel.pivot = new Vector2(0.5f, 0);
                promptPanel.anchoredPosition = new Vector2(0, 24);
                var background = panel.GetComponent<Image>();
                background.color = new Color(0, 0, 0, 0.8f);
                background.raycastTarget = false;
                var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObject.layer = panel.layer;
                prompt = textObject.GetComponent<TextMeshProUGUI>();
                prompt.rectTransform.SetParent(promptPanel, false);
                prompt.rectTransform.anchorMin = Vector2.zero;
                prompt.rectTransform.anchorMax = Vector2.one;
                prompt.rectTransform.offsetMin = new Vector2(12, 4);
                prompt.rectTransform.offsetMax = new Vector2(-12, -4);
                prompt.font = inventory.UiFont;
                prompt.fontSize = 18;
                prompt.alignment = TextAlignmentOptions.Center;
                prompt.richText = false;
                prompt.raycastTarget = false;
            }
            float width = ((RectTransform)inventory.UiCanvas.transform).rect.width;
            promptPanel.sizeDelta = new Vector2(Mathf.Min(420, Mathf.Max(1, width - 24)), 48);
            promptPanel.gameObject.SetActive(true);
            prompt.text = "[E]  " + label;
        }
    }
}
