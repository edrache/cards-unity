using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Contextual treasure collection and torch handling with the available hand.</summary>
    [DefaultExecutionOrder(175)]
    public sealed class TorchInteraction : MonoBehaviour
    {
        [SerializeField] private HandheldTorch torch;
        [Header("Interaction")]
        [SerializeField, Range(0.4f, 2f)] private float pickupRange = 1f;
        [SerializeField, Range(0.15f, 1f)] private float dropDuration = 0.35f;
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
        private bool pickingUp, transferred;
        private float elapsed, weight;
        private Vector3 reachTarget;
        private Quaternion pickupRotation, groundRotation;
        public bool IsBusy { get; private set; }
        public bool CanPickUp
        {
            get
            {
                if (!IsBusy) FindPickupTorch();
                return torch != null && !torch.IsHeld && torch.isActiveAndEnabled
                    && Vector3.Distance(transform.position, torch.transform.position) <= pickupRange && HasClearReach(torch);
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
        private void FindTreasure()
        {
            treasure = null;
            float distance = pickupRange;
            foreach (var candidate in FindObjectsByType<Treasure>(FindObjectsSortMode.None))
            {
                if (!candidate.isActiveAndEnabled || candidate.gameObject.scene != gameObject.scene) continue;
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
        public string ActionLabel => treasure != null ? "Podnieś " + treasure.DisplayName : HoldsTorch ? "Upuść" : "Podnieś";

        private void Awake()
        {
            if (torch == null) torch = GetComponentInChildren<HandheldTorch>();
            if (torch != null) { arm = torch.UpperArm; elbow = torch.Forearm; gripOffset = torch.GripOffset; }
            attack = GetComponent<TorchAttack>();
            gait = GetComponent<CartoonCharacterGait>();
            inventory = GetComponent<CharacterInventory>();
            if (inventory == null) inventory = gameObject.AddComponent<CharacterInventory>();
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
            if (GetComponent<CharacterKnockdown>()?.IsDown == true) return false;
            if (!isActiveAndEnabled || IsBusy || (attack != null && attack.IsAttacking)) return false;
            FindPickupTorch();
            FindTreasure();
            collecting = treasure != null;
            useLeftHand = collecting && HoldsTorch;
            string side = useLeftHand ? "Left" : "Right";
            arm = Part(side + " Arm");
            elbow = Part(side + " Forearm");
            var hand = Part(side + " Hand");
            if (arm == null || elbow == null || hand == null) return false;
            gripOffset = !collecting && torch != null && HoldsTorch ? torch.GripOffset : hand.localPosition;
            pickingUp = collecting || !HoldsTorch;
            if (!collecting && (torch == null || (pickingUp && !CanPickUp))) return false;
            IsBusy = true;
            transferred = false;
            elapsed = weight = 0f;
            if (pickingUp)
            {
                reachTarget = collecting ? treasure.transform.position : torch.transform.position;
                Vector3 direction = Vector3.ProjectOnPlane(reachTarget - transform.position, Vector3.up);
                pickupRotation = direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direction) : transform.rotation;
            }
            return true;
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float dt)
        {
            if (!IsBusy) return;
            if (collecting ? (!transferred && (treasure == null || !treasure.isActiveAndEnabled)) : torch == null) { Finish(); return; }
            elapsed += Mathf.Max(0f, dt);
            float duration = pickingUp ? pickupDuration : dropDuration;
            weight = elapsed <= duration ? Mathf.SmoothStep(0f, 1f, elapsed / duration)
                : 1f - Mathf.SmoothStep(0f, 1f, (elapsed - duration) / recoveryDuration);
            if (pickingUp && !transferred)
            {
                reachTarget = collecting ? treasure.transform.position : torch.transform.position;
                Vector3 direction = Vector3.ProjectOnPlane(reachTarget - transform.position, Vector3.up);
                if (direction.sqrMagnitude > 0.001f) pickupRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, pickupRotation, 360f * dt);
            }
            if (gait != null) gait.InteractionCrouch = pickingUp ? weight : 0f;
            if (torch != null && !useLeftHand) torch.SetPoseSuppression(weight);
        }

        private void LateUpdate() => ApplyPose();

        public void ApplyPose()
        {
            if (!IsBusy) return;
            if (collecting ? (!transferred && (treasure == null || !treasure.isActiveAndEnabled)) : torch == null) { Finish(); return; }
            if (pickingUp)
            {
                // Refresh after Update as Rigidbody interpolation may have moved the visible grip.
                if (!transferred) reachTarget = collecting ? treasure.transform.position : torch.transform.position;
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
            }
            else
            {
                // Lower and extend the arm before opening the grip.
                arm.rotation = Quaternion.Slerp(arm.rotation, transform.rotation * Quaternion.Euler(Mathf.Lerp(-32f, -82f,
                    gait != null ? gait.CrawlWeight : 0f), 0f, 20f), weight);
                elbow.localRotation = Quaternion.Slerp(elbow.localRotation, Quaternion.Euler(-8f, 0f, 0f), weight);
            }

            float duration = pickingUp ? pickupDuration : dropDuration;
            if (!transferred && elapsed >= duration)
            {
                if (pickingUp)
                {
                    // Recheck after reaching: do not pull a moving torch through a wall or across the room.
                    if (collecting)
                    {
                        if (treasure != null && treasure.isActiveAndEnabled
                            && Vector3.Distance(transform.position, treasure.transform.position) <= pickupRange
                            && HasClearReach(treasure.transform)
                            && Vector3.Distance(elbow.TransformPoint(gripOffset), reachTarget) < 0.12f)
                            inventory.Collect(treasure);
                    }
                    else if (CanPickUp && Vector3.Distance(elbow.TransformPoint(gripOffset), reachTarget) < 0.08f)
                    {
                        groundRotation = torch.transform.rotation;
                        torch.SetGripOffset(gripOffset);
                        torch.PickUp(transform, arm, elbow);
                        attack?.SetTorch(torch);
                    }
                }
                else torch.Drop();
                transferred = true;
            }
            if (!collecting && pickingUp && transferred && torch.IsHeld)
                torch.transform.rotation = Quaternion.Slerp(groundRotation, transform.rotation, 1f - weight);
            if (elapsed >= duration + recoveryDuration) Finish();
        }

        public void CancelInteraction() => Finish();

        private void Finish()
        {
            IsBusy = false;
            weight = 0f;
            if (torch != null) torch.SetPoseSuppression(0f);
            if (gait != null) gait.InteractionCrouch = 0f;
        }

        private void OnDisable() => Finish();

        private void OnGUI()
        {
            if (GetComponent<CharacterKnockdown>()?.IsDown == true) return;
            if (!showPrompt || IsBusy || (attack != null && attack.IsAttacking)) return;
            FindPickupTorch();
            FindTreasure();
            if (treasure == null && !HoldsTorch && !CanPickUp) return;
            GUI.Box(new Rect(Screen.width * 0.5f - 160f, Screen.height - 66f, 320f, 30f), "E  ·  " + ActionLabel);
        }
    }
}
