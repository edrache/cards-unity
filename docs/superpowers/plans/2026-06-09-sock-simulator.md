# Sock Simulator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `SockSimulator` component that disables physics and bone simulation when a sock is at rest, re-enabling it on pickup.

**Architecture:** A new `SockSimulator` MonoBehaviour manages two states (Active / Dormant). `Sock.cs` calls `OnPickedUp()` and `OnThrown()` on it. In Dormant state, `SockBoneFollower` and all `SockFabricPhysics` components are disabled and all `Rigidbody` components are set kinematic — bones freeze in place. After a throw, dormant transition waits for a minimum time AND all rigidbodies to sleep.

**Tech Stack:** Unity 6 (6000.3.10f1), C#, Unity Physics (HingeJoint / Rigidbody)

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `Assets/Socks/Scripts/Sock/SockSimulator.cs` | State machine: Active ↔ Dormant |
| Modify | `Assets/Socks/Scripts/Sock/Sock.cs` | Call SockSimulator on pickup/throw |

---

### Task 1: Create SockSimulator.cs

**Files:**
- Create: `Assets/Socks/Scripts/Sock/SockSimulator.cs`

- [ ] **Step 1: Create the file with full implementation**

```csharp
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
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Socks/Scripts/Sock/SockSimulator.cs
git commit -m "feat: add SockSimulator Active/Dormant state machine"
```

---

### Task 2: Wire SockSimulator into Sock.cs

**Files:**
- Modify: `Assets/Socks/Scripts/Sock/Sock.cs`

- [ ] **Step 1: Add field and Awake wiring**

In `Sock.cs`, inside the field declarations block (after the existing `bool _isHeld;` line), add:

```csharp
SockSimulator _simulator;
```

At the end of `Awake()`, add:

```csharp
_simulator = GetComponent<SockSimulator>();
```

The full `Awake()` after the change:

```csharp
void Awake()
{
    if (sockRenderer == null)
        sockRenderer = GetComponentInChildren<Renderer>();

    _mat                  = sockRenderer.material;
    _originalOutlineColor = _mat.GetColor(OutlineColorId);
    _rigidbodies          = GetComponentsInChildren<Rigidbody>();
    _colliders            = GetComponentsInChildren<Collider>();
    _simulator            = GetComponent<SockSimulator>();
}
```

- [ ] **Step 2: Call OnPickedUp from PickUp and PickUpPaired**

At the very start of `PickUp()`, add:

```csharp
_simulator?.OnPickedUp();
```

Full `PickUp()` after the change:

```csharp
public void PickUp(Transform[] slots)
{
    _simulator?.OnPickedUp();

    _handSlots   = slots;
    _activeSlots = slots;

    SetColliders(false);
    SetKinematic(true);

    for (int i = 0; i < segments.Length && i < slots.Length; i++)
        TweenSegmentToSlot(segments[i], slots[i], pickUpDuration, pickUpEase);

    _isHeld = true;
}
```

At the very start of `PickUpPaired()`, add:

```csharp
_simulator?.OnPickedUp();
```

Full `PickUpPaired()` after the change:

```csharp
public void PickUpPaired(Transform[] handSlots, Transform[] pairSlots)
{
    _simulator?.OnPickedUp();

    _handSlots   = handSlots;
    _activeSlots = pairSlots;

    SetColliders(false);
    SetKinematic(true);

    for (int i = 0; i < segments.Length && i < pairSlots.Length; i++)
        TweenSegmentToSlot(segments[i], pairSlots[i], pickUpDuration, pickUpEase);

    if (skinnedMesh != null)
        skinnedMesh.SetBlendShapeWeight(blendShapeIndex, 100f);

    _isHeld = true;
}
```

- [ ] **Step 3: Call OnThrown from Throw**

At the very start of `Throw()`, add:

```csharp
_simulator?.OnThrown();
```

Full `Throw()` after the change:

```csharp
public void Throw(Vector3 force)
{
    _simulator?.OnThrown();

    _isHeld = false;

    SetColliders(true);
    SetKinematic(false);

    foreach (Rigidbody rb in _rigidbodies)
        rb.AddForce(force, ForceMode.Impulse);
}
```

- [ ] **Step 4: Commit**

```bash
git add Assets/Socks/Scripts/Sock/Sock.cs
git commit -m "feat: wire SockSimulator into Sock pickup and throw"
```

---

### Task 3: Add SockSimulator to prefab (manual — Unity Editor)

This task is for **Marek** to complete in the Unity Editor. No code changes.

- [ ] **Step 1: Open the base sock prefab**

Open `Assets/Socks/Prefabs/SockRoot.prefab` (and its variants) in Prefab Mode.

- [ ] **Step 2: Add SockSimulator component**

Select the root GameObject of the prefab. In the Inspector, click **Add Component** → search for `SockSimulator` → add it.

- [ ] **Step 3: Tune the values**

| Field | Suggested starting value |
|---|---|
| Min Active Time After Throw | `1.5` |
| Sleep Velocity Threshold | `0.05` |

- [ ] **Step 4: Save prefab, apply to all variants**

Save the base prefab. Open each variant (`SockRoot Variant`, `SockRoot Variant 1`–`4`) and verify the component inherited correctly. Save all.

- [ ] **Step 5: Verify in Play Mode**

Enter Play Mode. Spawn socks. Pick one up — confirm it moves freely. Throw it — after ~1.5 s + settling, the sock should freeze in place (no further bone movement). Pick it up again — should animate normally.

In the Inspector while selected, watch `SockSimulator` state field to confirm transitions.

---

> **Note on `_state` visibility:** If you want to see the current state in the Inspector during Play Mode for debugging, change the `State _state` field to:
> ```csharp
> [SerializeField] State _state = State.Active;
> ```
