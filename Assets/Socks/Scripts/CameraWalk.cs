using Rewired;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class CameraWalk : MonoBehaviour
{
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float lookSensitivity = 2f;
    [SerializeField] float controllerLookSensitivity = 2f;
    [SerializeField] bool invertLookX;
    [SerializeField] bool invertLookY;
    [SerializeField] bool mouseLookEnabled = true;
    [SerializeField] float gravity = -20f;

    Player _player;
    CharacterController _controller;
    float _pitch;
    float _yaw;
    float _verticalVelocity;
    bool _prevMouseLookEnabled;

    void Awake()
    {
        _player = ReInput.players.GetPlayer(0);
        _controller = GetComponent<CharacterController>();

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
        float lookX = _player.GetAxis("LookX") * controllerLookSensitivity;
        float lookY = _player.GetAxis("LookY") * controllerLookSensitivity;

        if (mouseLookEnabled)
        {
            lookX += _player.GetAxis("MouseX") * lookSensitivity;
            lookY += _player.GetAxis("MouseY") * lookSensitivity;
        }

        if (invertLookX) lookX = -lookX;
        if (invertLookY) lookY = -lookY;

        _yaw += lookX;
        _pitch -= lookY;
        _pitch = Mathf.Clamp(_pitch, -89f, 89f);

        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    void HandleMove()
    {
        float moveX = _player.GetAxis("MoveX");
        float moveZ = _player.GetAxis("MoveZ");

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 horizontalMove = (right * moveX + forward * moveZ) * moveSpeed;

        if (_controller.isGrounded)
            _verticalVelocity = -2f; // small constant keeps grounded flag stable
        else
            _verticalVelocity += gravity * Time.deltaTime;

        Vector3 move = horizontalMove + Vector3.up * _verticalVelocity;
        _controller.Move(move * Time.deltaTime);
    }

    void ApplyCursorState()
    {
        Cursor.lockState = mouseLookEnabled ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !mouseLookEnabled;
    }
}
