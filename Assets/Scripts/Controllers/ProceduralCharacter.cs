using UnityEngine;
using UnityEngine.InputSystem;

namespace CardsUnity.Controllers
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ProceduralCharacter : MonoBehaviour
    {
        [Header("Movement")]
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
        private Vector3 previousActualVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            gait = GetComponent<CartoonCharacterGait>();
            if (body != null) bodyOrigin = body.localPosition;
        }

        private void Update()
        {
            Vector2 input = Vector2.zero;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                input.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                input.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                    - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            }
            if (Gamepad.current != null && Gamepad.current.leftStick.ReadValue().sqrMagnitude > input.sqrMagnitude)
                input = Gamepad.current.leftStick.ReadValue();
            input = Vector2.ClampMagnitude(input, 1f);

            Vector3 forward = movementCamera != null
                ? Vector3.ProjectOnPlane(movementCamera.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            bool running = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            running |= Gamepad.current != null && Gamepad.current.leftStickButton.isPressed;
            Vector3 desired = (forward * input.y + right * input.x) * (running ? runSpeed : speed);
            velocity = Vector3.MoveTowards(velocity, desired,
                (input.sqrMagnitude > 0.001f ? acceleration : braking) * Time.deltaTime);
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            Vector3 before = transform.position;
            controller.Move((velocity + Vector3.up * verticalSpeed) * Time.deltaTime);
            Vector3 displacement = Vector3.ProjectOnPlane(transform.position - before, Vector3.up);
            if ((controller.collisionFlags & CollisionFlags.Above) != 0) verticalSpeed = Mathf.Min(verticalSpeed, 0f);
            if (velocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(velocity), turnSpeed * Time.deltaTime);

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 actualVelocity = displacement / dt;
            float actualSpeed = actualVelocity.magnitude;
            if (gait != null)
            {
                gait.SetRunning(running);
                gait.Animate(actualVelocity, (actualVelocity - previousActualVelocity) / dt,
                    displacement.magnitude, controller.isGrounded, runSpeed);
                previousActualVelocity = actualVelocity;
                return;
            }
            blend = Mathf.MoveTowards(blend, controller.isGrounded ? Mathf.Clamp01(actualSpeed / speed) : 0f,
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

        private static void Pose(Transform limb, float angle)
        {
            if (limb != null) limb.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
