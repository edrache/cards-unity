using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>One contextual action: release the held torch or reach down for it.</summary>
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
        private TorchAttack attack;
        private CartoonCharacterGait gait;
        private bool pickingUp, transferred;
        private float elapsed, weight;
        private Vector3 reachTarget;
        private Quaternion pickupRotation, groundRotation;
        public bool IsBusy { get; private set; }
        public bool CanPickUp => torch != null && !torch.IsHeld
            && Vector3.Distance(transform.position, torch.transform.position) <= pickupRange && HasClearReach();
        public string ActionLabel => torch != null && torch.IsHeld ? "Upuść" : "Podnieś";

        private void Awake()
        {
            if (torch == null) torch = GetComponentInChildren<HandheldTorch>();
            if (torch != null) { arm = torch.UpperArm; elbow = torch.Forearm; }
            attack = GetComponent<TorchAttack>();
            gait = GetComponent<CartoonCharacterGait>();
        }

        private bool HasClearReach()
        {
            Vector3 origin = transform.position + Vector3.up * 0.45f;
            Vector3 delta = torch.transform.position - origin;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(torch.transform)) return false;
            }
            return true;
        }

        public bool TryInteract()
        {
            if (!isActiveAndEnabled || IsBusy || torch == null || arm == null || elbow == null
                || (attack != null && attack.IsAttacking)) return false;
            pickingUp = !torch.IsHeld;
            if (pickingUp && !CanPickUp) return false;
            IsBusy = true;
            transferred = false;
            elapsed = weight = 0f;
            if (pickingUp)
            {
                reachTarget = torch.transform.position;
                Vector3 direction = Vector3.ProjectOnPlane(reachTarget - transform.position, Vector3.up);
                pickupRotation = direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direction) : transform.rotation;
            }
            return true;
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float dt)
        {
            if (!IsBusy) return;
            if (torch == null) { Finish(); return; }
            elapsed += Mathf.Max(0f, dt);
            float duration = pickingUp ? pickupDuration : dropDuration;
            weight = elapsed <= duration ? Mathf.SmoothStep(0f, 1f, elapsed / duration)
                : 1f - Mathf.SmoothStep(0f, 1f, (elapsed - duration) / recoveryDuration);
            if (pickingUp && !transferred)
            {
                reachTarget = torch.transform.position;
                Vector3 direction = Vector3.ProjectOnPlane(reachTarget - transform.position, Vector3.up);
                if (direction.sqrMagnitude > 0.001f) pickupRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, pickupRotation, 360f * dt);
            }
            if (gait != null) gait.InteractionCrouch = pickingUp ? weight : 0f;
            torch.SetPoseSuppression(weight);
        }

        private void LateUpdate() => ApplyPose();

        public void ApplyPose()
        {
            if (!IsBusy || torch == null) return;
            if (pickingUp)
            {
                // Refresh after Update as Rigidbody interpolation may have moved the visible grip.
                if (!transferred) reachTarget = torch.transform.position;
                // Two-bone reach keeps the hand on the physical grip until it is attached.
                Vector3 shoulder = arm.position;
                float upper = Vector3.Distance(shoulder, elbow.position);
                float lower = torch.GripOffset.magnitude;
                Vector3 delta = reachTarget - shoulder;
                float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(upper - lower) + 0.001f, upper + lower - 0.001f);
                Vector3 direction = delta.normalized;
                Vector3 bend = Vector3.ProjectOnPlane(-transform.forward + transform.right * 0.35f, direction).normalized;
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
                    if (CanPickUp && Vector3.Distance(elbow.TransformPoint(torch.GripOffset), reachTarget) < 0.08f)
                    {
                        groundRotation = torch.transform.rotation;
                        torch.PickUp(transform, arm, elbow);
                    }
                }
                else torch.Drop();
                transferred = true;
            }
            if (pickingUp && transferred && torch.IsHeld)
                torch.transform.rotation = Quaternion.Slerp(groundRotation, transform.rotation, 1f - weight);
            if (elapsed >= duration + recoveryDuration) Finish();
        }

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
            if (!showPrompt || torch == null || IsBusy || (attack != null && attack.IsAttacking)) return;
            if (!torch.IsHeld && !CanPickUp) return;
            GUI.Box(new Rect(Screen.width * 0.5f - 85f, Screen.height - 66f, 170f, 30f), "E  ·  " + ActionLabel);
        }
    }
}
