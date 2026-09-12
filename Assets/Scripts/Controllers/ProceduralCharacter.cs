using Rewired;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CardsUnity.Controllers
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ProceduralCharacter : MonoBehaviour
    {
        [Header("Balance")]
        [Tooltip("Optional shared balance. When empty, the values below keep their original behaviour.")]
        [SerializeField] private PlayerGameplayBalanceProfile balanceProfile;

        [Header("Rewired input")]
        [SerializeField] private string rewiredPlayerName = "Player0";

        [Header("Local fallback — Analog locomotion")]
        [Tooltip("Stick magnitude below which the stick counts as centred. The character stops and the manual style mix returns.")]
        [SerializeField, Range(0.01f, 0.5f)] private float idleThreshold = 0.08f;
        [Tooltip("Stick magnitude at which walking starts. Between Idle Threshold and this value the character sneaks. Running stays on the held Run action.")]
        [SerializeField, Range(0.02f, 0.99f)] private float walkThreshold = 0.35f;

        [Header("Local fallback — Movement")]
        [SerializeField] private float speed = 3.5f;
        [SerializeField] private float acceleration = 9f;
        [SerializeField] private float braking = 7f;
        [SerializeField] private float runSpeed = 6f;
        [SerializeField] private float turnSpeed = 540f;
        [SerializeField] private Transform movementCamera;

        [Header("Procedural pose")]
        [SerializeField] private Transform body;
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private float strideLength = 1.6f;
        [SerializeField] private float legSwing = 28f;

        private CharacterController controller;
        private Vector3 velocity;
        private Vector3 bodyOrigin;
        private float verticalSpeed;
        private float phase;
        private float blend;
        private CartoonCharacterGait gait;
        private TorchAttack torchAttack;
        private TorchInteraction torchInteraction;
        private Vector3 previousActualVelocity;
        private bool sneaking;
        private bool analogMovement;

        public PlayerGameplayBalanceProfile BalanceProfile => balanceProfile;
        private PlayerMovementBalance MovementBalance => balanceProfile != null ? balanceProfile.Movement : null;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            gait = GetComponent<CartoonCharacterGait>();
            torchAttack = GetComponent<TorchAttack>();
            torchInteraction = GetComponent<TorchInteraction>();
            if (body != null) bodyOrigin = body.localPosition;
        }

        private void Update()
        {
            Vector2 input;
            bool running;
            bool attackHeld;
            bool interactPressed;
            bool interactHeld;
            if (ReInput.isReady)
            {
                Player player = ReInput.players.GetPlayer(rewiredPlayerName);
                input = new Vector2(player.GetAxis("MoveHorizontal"), player.GetAxis("MoveVertical"));
                bool keyboardMovement = player.IsCurrentInputSource("MoveHorizontal", ControllerType.Keyboard)
                    || player.IsCurrentInputSource("MoveVertical", ControllerType.Keyboard);
                bool joystickMovement = player.IsCurrentInputSource("MoveHorizontal", ControllerType.Joystick)
                    || player.IsCurrentInputSource("MoveVertical", ControllerType.Joystick);
                if (keyboardMovement || joystickMovement) analogMovement = joystickMovement && !keyboardMovement;
                // Run stays a held action on every controller, so it also works alongside the stick.
                running = player.GetButton("Run");
                attackHeld = player.GetButton("Attack");
                bool hasInteractAction = ReInput.mapping.GetActionId("TorchInteract") >= 0;
                interactPressed = hasInteractAction && player.GetButtonDown("TorchInteract");
                interactHeld = hasInteractAction && player.GetButton("TorchInteract");
                if (player.GetButtonDown("Sneak") && player.IsCurrentInputSource("Sneak", ControllerType.Keyboard))
                {
                    sneaking = !sneaking;
                    analogMovement = false;
                }
            }
            else
            {
                // Keep the daytime playground usable without a Rewired manager.
                ReadLegacyInput(out input, out running, out analogMovement);
                if (UnityEngine.InputSystem.Keyboard.current?.cKey.wasPressedThisFrame == true) sneaking = !sneaking;
                attackHeld = UnityEngine.InputSystem.Keyboard.current?.spaceKey.isPressed == true;
                interactPressed = UnityEngine.InputSystem.Keyboard.current?.eKey.wasPressedThisFrame == true;
                interactHeld = UnityEngine.InputSystem.Keyboard.current?.eKey.isPressed == true;
            }
            var knockdown = GetComponent<CharacterKnockdown>();
            if (knockdown != null && knockdown.IsDown)
            {
                running = attackHeld = interactPressed = interactHeld = false;
            }
            if (torchInteraction != null) torchInteraction.SetInteractHeld(interactHeld);
            if (interactPressed && torchInteraction != null) torchInteraction.TryInteract();
            // Completing the expedition disables control during this interaction.
            if (!enabled) return;
            // Holding cocks the arm, releasing strikes, so the button state is fed every frame.
            if (torchAttack != null) torchAttack.SetAttackHeld(attackHeld);
            input = Vector2.ClampMagnitude(input, 1f);
            float stickMagnitude = input.magnitude;
            float effectiveIdleThreshold = MovementBalance?.IdleThreshold ?? idleThreshold;
            float effectiveWalkThreshold = MovementBalance?.WalkThreshold ?? walkThreshold;
            if (analogMovement && stickMagnitude < effectiveIdleThreshold)
            {
                // A centred stick must release the automatic style, otherwise the manual
                // style mixer would stay overwritten for as long as a gamepad is connected.
                analogMovement = false;
                input = Vector2.zero;
                stickMagnitude = 0f;
            }

            bool activeSneak = sneaking;
            bool activeWalk = false;
            if (analogMovement)
            {
                // Stick deflection only chooses between Sneak and Walk; Run overrides both.
                activeSneak = !running && stickMagnitude < effectiveWalkThreshold;
                activeWalk = !running && !activeSneak;
            }
            // The input vector still carries the stick magnitude, so speed scales with deflection.
            float walkSpeed = MovementBalance?.WalkSpeed ?? speed;
            float sprintSpeed = MovementBalance?.RunSpeed ?? runSpeed;
            float movementSpeed = running ? sprintSpeed : walkSpeed;
            if (knockdown != null && knockdown.IsDown)
                movementSpeed *= MovementBalance?.KnockedDownSpeedMultiplier ?? 0.4f;

            Vector3 forward = movementCamera != null
                ? Vector3.ProjectOnPlane(movementCamera.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 desired = (forward * input.y + right * input.x) * movementSpeed;
            velocity = Vector3.MoveTowards(velocity, desired,
                (input.sqrMagnitude > 0.001f
                    ? MovementBalance?.Acceleration ?? acceleration
                    : MovementBalance?.Braking ?? braking) * Time.deltaTime);
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            Vector3 before = transform.position;
            Move((velocity + Vector3.up * verticalSpeed) * Time.deltaTime);
            Vector3 displacement = Vector3.ProjectOnPlane(transform.position - before, Vector3.up);
            if (knockdown != null) knockdown.ReportMovement(displacement.magnitude);
            if ((controller.collisionFlags & CollisionFlags.Above) != 0) verticalSpeed = Mathf.Min(verticalSpeed, 0f);
            if (velocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(velocity), (MovementBalance?.TurnSpeed ?? turnSpeed) * Time.deltaTime);

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 actualVelocity = displacement / dt;
            float actualSpeed = actualVelocity.magnitude;
            if (gait != null)
            {
                gait.SetLocomotionStyle(running, activeSneak, activeWalk);
                gait.Animate(actualVelocity, (actualVelocity - previousActualVelocity) / dt,
                    displacement.magnitude, controller.isGrounded, sprintSpeed);
                previousActualVelocity = actualVelocity;
                return;
            }
            blend = Mathf.MoveTowards(blend, controller.isGrounded
                    ? Mathf.Clamp01(actualSpeed / Mathf.Max(0.0001f, walkSpeed)) : 0f,
                Time.deltaTime * 8f);
            phase = Mathf.Repeat(phase + displacement.magnitude / Mathf.Max(0.1f, strideLength) * Mathf.PI * 2f, Mathf.PI * 2f);
            float swing = Mathf.Sin(phase) * blend;
            Pose(leftLeg, swing * legSwing);
            Pose(rightLeg, -swing * legSwing);
            Pose(leftArm, -swing * legSwing * 0.75f);
            Pose(rightArm, swing * legSwing * 0.75f);
            if (body != null)
            {
                body.localPosition = bodyOrigin + Vector3.up * (Mathf.Cos(phase * 2f) * 0.035f * blend);
                body.localRotation = Quaternion.Euler(blend * 5f, 0f, -swing * 3f);
            }
        }

        // Unity's rounded capsule can climb higher than stepOffset. Check the riser
        // against the supporting surface before allowing its built-in step solver.
        public void Move(Vector3 motion)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            Vector3 horizontal = Vector3.ProjectOnPlane(motion, Vector3.up);
            float distance = horizontal.magnitude;
            if (distance > 0f && controller.isGrounded && GetComponent<ShadowKnightProportions>() != null)
            {
                Vector3 center = transform.TransformPoint(controller.center);
                float radius = controller.radius * transform.lossyScale.x;
                float half = controller.height * transform.lossyScale.y * 0.5f - radius;
                Vector3 direction = horizontal / distance;
                if (Physics.CapsuleCast(center + Vector3.up * half, center - Vector3.up * half,
                    radius, direction, out RaycastHit wall, distance + controller.skinWidth,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && !wall.transform.IsChildOf(transform)
                    && Vector3.Angle(wall.normal, Vector3.up) > controller.slopeLimit
                    && Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down,
                        out RaycastHit support, 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    float limit = controller.stepOffset * transform.lossyScale.y;
                    Vector3 probe = wall.point + direction * 0.025f;
                    float probeHeight = controller.height * transform.lossyScale.y + limit;
                    probe.y = support.point.y + probeHeight;
                    if (!Physics.Raycast(probe, Vector3.down, out RaycastHit top, probeHeight + 0.01f,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                        || top.point.y - support.point.y > limit + 0.001f
                        || Vector3.Angle(top.normal, Vector3.up) > controller.slopeLimit)
                    {
                        Vector3 normal = Vector3.ProjectOnPlane(wall.normal, Vector3.up).normalized;
                        horizontal = Vector3.ProjectOnPlane(horizontal, normal);
                        motion = horizontal + Vector3.up * motion.y;
                    }
                }
            }
            controller.Move(motion);
        }

        private void OnValidate()
        {
            idleThreshold = Mathf.Clamp(idleThreshold, 0.01f, 0.5f);
            walkThreshold = Mathf.Clamp(walkThreshold, idleThreshold + 0.01f, 0.99f);
        }

#if UNITY_EDITOR
        internal void CopyLegacyBalanceTo(PlayerMovementBalance destination)
        {
            destination.Capture(idleThreshold, walkThreshold, speed, acceleration, braking, runSpeed, turnSpeed);
        }

        internal void AssignBalanceProfile(PlayerGameplayBalanceProfile profile)
        {
            balanceProfile = profile;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void OnDisable()
        {
            torchInteraction?.SetInteractHeld(false);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) torchInteraction?.SetInteractHeld(false);
        }

        private void ReadLegacyInput(out Vector2 input, out bool running, out bool analog)
        {
            input = Vector2.zero;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                input.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                input.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                    - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            }
            var gamepad = Gamepad.current;
            running = (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
                || (gamepad != null && gamepad.leftStickButton.isPressed);
            analog = false;
            // The keyboard keeps priority; the stick only takes over once it is actually deflected.
            if (input.sqrMagnitude > 0f || gamepad == null) return;
            Vector2 stick = gamepad.leftStick.ReadValue();
            if (stick.magnitude < (MovementBalance?.IdleThreshold ?? idleThreshold)) return;
            input = stick;
            analog = true;
        }

        private static void Pose(Transform limb, float angle)
        {
            if (limb != null) limb.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
