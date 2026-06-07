using Rewired;
using UnityEngine;

public class CameraFly : MonoBehaviour
{
    [SerializeField] float moveSpeed = 10f;
    [SerializeField] float lookSensitivity = 2f;
    [SerializeField] bool invertLookX;
    [SerializeField] bool invertLookY;

    Player _player;
    float _pitch;
    float _yaw;

    void Awake()
    {
        _player = ReInput.players.GetPlayer(0);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _yaw = transform.eulerAngles.y;
        _pitch = transform.eulerAngles.x;
    }

    void Update()
    {
        HandleLook();
        HandleMove();
    }

    void HandleLook()
    {
        float lookX = _player.GetAxis("LookX");
        float lookY = _player.GetAxis("LookY");

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
        float moveY = _player.GetAxis("MoveY");

        Vector3 move = transform.right * moveX + transform.forward * moveZ + Vector3.up * moveY;
        transform.position += move * (moveSpeed * Time.deltaTime);
    }
}
