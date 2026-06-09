using UnityEngine;

public class SockSimulator : MonoBehaviour
{
    [Tooltip("Minimum seconds after throw before dormant check begins.")]
    [SerializeField] float minActiveTimeAfterThrow = 1.5f;

    [Tooltip("Velocity magnitude below which a Rigidbody is considered at rest.")]
    [SerializeField] float sleepVelocityThreshold = 0.05f;

    enum State { Active, Dormant }

    SockBoneFollower _boneFollower;
    SockFabricPhysics[] _fabricPhysics;
    Rigidbody[] _rigidbodies;

    State _state = State.Active;
    float _throwTime = -1f;

    void Awake()
    {
        _boneFollower  = GetComponentInChildren<SockBoneFollower>();
        _fabricPhysics = GetComponentsInChildren<SockFabricPhysics>();
        _rigidbodies   = GetComponentsInChildren<Rigidbody>();
    }

    public void OnPickedUp()
    {
        _throwTime = -1f;
        SetState(State.Active);
    }

    public void OnThrown()
    {
        _throwTime = Time.time;
    }

    void FixedUpdate()
    {
        if (_state != State.Active) return;
        if (_throwTime < 0f) return;
        if (Time.time - _throwTime < minActiveTimeAfterThrow) return;

        if (AllRigidbodiesAtRest())
            SetState(State.Dormant);
    }

    bool AllRigidbodiesAtRest()
    {
        foreach (Rigidbody rb in _rigidbodies)
        {
            if (!rb.isKinematic && !rb.IsSleeping() && rb.linearVelocity.magnitude >= sleepVelocityThreshold)
                return false;
        }
        return true;
    }

    void SetState(State next)
    {
        if (_state == next) return;
        _state = next;

        bool active = next == State.Active;

        if (_boneFollower != null)
            _boneFollower.enabled = active;

        foreach (SockFabricPhysics fp in _fabricPhysics)
            fp.enabled = active;

        foreach (Rigidbody rb in _rigidbodies)
            rb.isKinematic = !active;
    }
}
