using UnityEngine;
using Rewired;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class CharacterMover : MonoBehaviour
{
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float flipThreshold = 0.05f;
    [SerializeField] int playerId = 0;

    Rigidbody _rb;
    Animator _animator;
    Player _player;
    static readonly int SpeedHash = Animator.StringToHash("Speed");

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        _player = ReInput.players.GetPlayer(playerId);
    }

    void Update()
    {
        float h = _player.GetAxisRaw("CharacterMove_Horizontal");
        float v = _player.GetAxisRaw("CharacterMove_Vertical");

        Vector3 input = new Vector3(h, 0f, v).normalized;
        _rb.linearVelocity = new Vector3(input.x * moveSpeed, _rb.linearVelocity.y, input.z * moveSpeed);

        float speed = input.magnitude;
        _animator.SetFloat(SpeedHash, speed);

        // Flip sprite based on horizontal direction
        if (Mathf.Abs(h) > flipThreshold)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (h < 0f ? -1f : 1f);
            transform.localScale = scale;
        }
    }
}
