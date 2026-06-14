using System.Collections.Generic;
using UnityEngine;

public class SockSimulator : MonoBehaviour
{
    [Tooltip("Minimum seconds after throw before dormant check begins.")]
    [SerializeField] float minActiveTimeAfterThrow = 1.5f;

    [Tooltip("Velocity magnitude below which a Rigidbody is considered at rest.")]
    [SerializeField] float sleepVelocityThreshold = 0.05f;

    [Tooltip("Radius in which dormant socks are woken up when this sock is picked up.")]
    [SerializeField] float wakeUpRadius = 0.4f;

    enum State { Active, Dormant }

    SockBoneFollower _boneFollower;
    SockFabricPhysics[] _fabricPhysics;
    Rigidbody[] _rigidbodies;

    [SerializeField] State _state = State.Active;
    float _throwTime = -1f;
    SockSimulator _wakeSource;
    bool _isBeingHeld;

    void Awake()
    {
        _boneFollower  = GetComponentInChildren<SockBoneFollower>();
        _fabricPhysics = GetComponentsInChildren<SockFabricPhysics>();
        _rigidbodies   = GetComponentsInChildren<Rigidbody>();
    }

    void Start()
    {
        _throwTime = Time.time;
    }

    public void OnPickedUp()
    {
        _isBeingHeld = true;
        WakeNearbySocks();
        WakeUp();
    }

    void WakeNearbySocks()
    {
        HashSet<SockSimulator> woken = new HashSet<SockSimulator>();
        foreach (Rigidbody rb in _rigidbodies)
        {
            Collider[] hits = Physics.OverlapSphere(rb.position, wakeUpRadius);
            foreach (Collider hit in hits)
            {
                SockSimulator other = hit.GetComponentInParent<SockSimulator>();
                if (other != null && other != this && woken.Add(other))
                    other.WakeUpFrom(this);
            }
        }
    }

    public void WakeUp()
    {
        _wakeSource = null;
        foreach (Rigidbody rb in _rigidbodies)
            rb.isKinematic = false;
        SetState(State.Active);
        _throwTime = -1f;
    }

    public void WakeUpFrom(SockSimulator source)
    {
        _wakeSource = source;
        foreach (Rigidbody rb in _rigidbodies)
            rb.isKinematic = false;
        SetState(State.Active);
        _throwTime = Time.time;
    }

    public void OnThrown()
    {
        _isBeingHeld = false;
        _wakeSource = null;
        SetState(State.Active);
        _throwTime = Time.time;
    }

    void FixedUpdate()
    {
        if (_state != State.Active) return;
        if (_throwTime < 0f) return;
        if (Time.time - _throwTime < minActiveTimeAfterThrow) return;

        if (_wakeSource != null)
        {
            if (_wakeSource._isBeingHeld && IsWithinWakeRadius(_wakeSource))
                return;
            _wakeSource = null;
        }

        if (AllRigidbodiesAtRest())
            SetState(State.Dormant);
    }

    bool IsWithinWakeRadius(SockSimulator other)
    {
        float sqrRadius = wakeUpRadius * wakeUpRadius;
        foreach (Rigidbody myRb in _rigidbodies)
            foreach (Rigidbody otherRb in other._rigidbodies)
                if ((myRb.position - otherRb.position).sqrMagnitude <= sqrRadius)
                    return true;
        return false;
    }

    bool AllRigidbodiesAtRest()
    {
        if (_rigidbodies.Length == 0) return false;
        foreach (Rigidbody rb in _rigidbodies)
        {
            if (rb.isKinematic) continue;
            if (!rb.IsSleeping() && rb.linearVelocity.sqrMagnitude >= sleepVelocityThreshold * sleepVelocityThreshold)
                return false;
        }
        return true;
    }

    void OnDrawGizmosSelected()
    {
        if (_rigidbodies == null) return;
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        foreach (Rigidbody rb in _rigidbodies)
            Gizmos.DrawWireSphere(rb.position, wakeUpRadius);
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

        if (!active)
        {
            foreach (Rigidbody rb in _rigidbodies)
                rb.isKinematic = true;
        }
    }
}
