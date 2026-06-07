using Rewired;
using UnityEngine;

public class CameraWalk : MonoBehaviour
{
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float lookSensitivity = 2f;
    [SerializeField] bool invertLookX;
    [SerializeField] bool invertLookY;
    [SerializeField] float collisionRadius = 0.3f;
    [SerializeField] LayerMask collisionMask = ~0;
    [SerializeField] bool mouseLookEnabled = true;

    const float SkinWidth = 0.01f;

    Player _player;
    float _pitch;
    float _yaw;
    bool _prevMouseLookEnabled;

    void Awake()
    {
        _player = ReInput.players.GetPlayer(0);

        _prevMouseLookEnabled = mouseLookEnabled;
        ApplyCursorState();

        _yaw = transform.eulerAngles.y;
        _pitch = transform.eulerAngles.x;
    }

    void Update()
    {
        if (mouseLookEnabled != _prevMouseLookEnabled)
        {
            _prevMouseLookEnabled = mouseLookEnabled;
            ApplyCursorState();
        }

        HandleLook();
        HandleMove();
    }

    void HandleLook()
    {
        float lookX = _player.GetAxis("LookX");
        float lookY = _player.GetAxis("LookY");

        if (mouseLookEnabled)
        {
            lookX += _player.GetAxis("MouseX");
            lookY += _player.GetAxis("MouseY");
        }

        if (invertLookX) lookX = -lookX;
        if (invertLookY) lookY = -lookY;

        _yaw += lookX * lookSensitivity;
        _pitch -= lookY * lookSensitivity;
        _pitch = Mathf.Clamp(_pitch, -89f, 89f);

        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    void HandleMove()
    {
        float moveX = _player.GetAxis("MoveX");
        float moveZ = _player.GetAxis("MoveZ");

        // Project forward/right onto XZ plane so walking stays level
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

        Vector3 move = right * moveX + forward * moveZ;
        float distance = move.magnitude * moveSpeed * Time.deltaTime;

        if (distance <= Mathf.Epsilon) return;

        Vector3 dir = move.normalized;

        if (Physics.SphereCast(transform.position, collisionRadius, dir, out RaycastHit hit, distance + SkinWidth, collisionMask))
            distance = Mathf.Max(0f, hit.distance - SkinWidth);

        transform.position += dir * distance;
    }

    void ApplyCursorState()
    {
        Cursor.lockState = mouseLookEnabled ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !mouseLookEnabled;
    }
}
