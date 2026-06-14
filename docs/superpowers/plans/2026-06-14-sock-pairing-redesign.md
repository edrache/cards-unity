# Sock Pairing Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the hand-slot pickup system with bone-manipulation + proximity-based sock pairing, and add SockPair pickup/throw/decompose via InteractSock.

**Architecture:** Three existing scripts are modified — `Sock.cs` gains a static registry and pair-highlight methods; `SockPair.cs` stores Sock references and handles kinematic hold; `SockSelector.cs` is rewritten as a three-state machine (Idle / ManipulatingSock / HoldingPair). No new files are created.

**Tech Stack:** Unity 6 (6000.3.10f1), C#, Rewired, URP/Quibli shaders with `_OutlineEnabled` + `_OutlineColor` shader properties.

---

## Files

| Action | Path |
|---|---|
| Modify | `Assets/Socks/Scripts/Sock/Sock.cs` |
| Modify | `Assets/Socks/Scripts/SockPair.cs` |
| Modify | `Assets/Socks/Scripts/SockSelector.cs` |

---

### Task 1: `Sock.cs` — static registry, GetManipulatedBone, pair highlight

**Files:**
- Modify: `Assets/Socks/Scripts/Sock/Sock.cs`

- [ ] **Step 1: Add static registry and OutlineColorId**

The file already has `using System.Collections.Generic;`. After the existing line:
```csharp
static readonly int OutlineEnabledId = Shader.PropertyToID("_OutlineEnabled");
```
add:
```csharp
static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

public static readonly List<Sock> AllSocks = new List<Sock>();
```

- [ ] **Step 2: Register in Awake, deregister in OnDestroy**

At the very end of `Awake()`, add:
```csharp
AllSocks.Add(this);
```

Add `OnDestroy` immediately after `Awake`:
```csharp
void OnDestroy()
{
    AllSocks.Remove(this);
}
```

- [ ] **Step 3: Expose the manipulated bone**

Add after `StopManipulating()`:
```csharp
public Rigidbody GetManipulatedBone() => _manipulatedRb;
```

- [ ] **Step 4: Add pair highlight methods**

Add after `Unhighlight()`:
```csharp
public void ShowPairHighlight(Color color)
{
    _mat.SetFloat(OutlineEnabledId, 1f);
    _mat.EnableKeyword("DR_OUTLINE_ON");
    _mat.SetColor(OutlineColorId, color);
}

public void HidePairHighlight()
{
    _mat.SetFloat(OutlineEnabledId, 0f);
    _mat.DisableKeyword("DR_OUTLINE_ON");
}
```

- [ ] **Step 5: Verify compilation**

Open Unity — confirm no errors in the Console.

- [ ] **Step 6: Commit**

```bash
git add Assets/Socks/Scripts/Sock/Sock.cs
git commit -m "feat: sock static registry, GetManipulatedBone, pair highlight"
```

---

### Task 2: `SockPair.cs` — Setup, Decompose, kinematic hold

**Files:**
- Modify: `Assets/Socks/Scripts/SockPair.cs`

- [ ] **Step 1: Replace file contents**

Replace the entire contents of `Assets/Socks/Scripts/SockPair.cs` with:

```csharp
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SockPair : MonoBehaviour
{
    [SerializeField] Renderer rendererLeft;
    [SerializeField] Renderer rendererRight;

    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

    Material _matLeft;
    Material _matRight;
    Color    _originalOutlineLeft;
    Color    _originalOutlineRight;

    Sock _sockA;
    Sock _sockB;

    Rigidbody _rb;
    float     _holdDistance;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    void CacheMaterials()
    {
        if (rendererLeft != null)
        {
            _matLeft             = rendererLeft.material;
            _originalOutlineLeft = _matLeft.GetColor(OutlineColorId);
        }
        if (rendererRight != null)
        {
            _matRight             = rendererRight.material;
            _originalOutlineRight = _matRight.GetColor(OutlineColorId);
        }
    }

    public void Setup(Sock sockA, Sock sockB)
    {
        _sockA = sockA;
        _sockB = sockB;

        if (rendererLeft  != null) rendererLeft.sharedMaterial  = sockA.GetSharedMaterial();
        if (rendererRight != null) rendererRight.sharedMaterial = sockB.GetSharedMaterial();
        CacheMaterials();

        sockA.gameObject.SetActive(false);
        sockB.gameObject.SetActive(false);
    }

    public void Decompose()
    {
        _sockA.transform.position = transform.position;
        _sockB.transform.position = transform.position;
        _sockA.gameObject.SetActive(true);
        _sockB.gameObject.SetActive(true);
        Destroy(gameObject);
    }

    public void StartHold(Camera cam)
    {
        _rb.isKinematic    = true;
        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _holdDistance = Vector3.Distance(cam.transform.position, transform.position);
    }

    public void UpdateHold(Camera cam)
    {
        Ray ray = cam.ScreenPointToRay(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        transform.position = ray.origin + ray.direction * _holdDistance;
    }

    public void StopHold(Vector3 throwForce)
    {
        _rb.isKinematic   = false;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.AddForce(throwForce, ForceMode.Impulse);
    }

    public void Highlight()
    {
        _matLeft?.SetColor(OutlineColorId,  Color.white);
        _matRight?.SetColor(OutlineColorId, Color.white);
    }

    public void Unhighlight()
    {
        _matLeft?.SetColor(OutlineColorId,  _originalOutlineLeft);
        _matRight?.SetColor(OutlineColorId, _originalOutlineRight);
    }
}
```

- [ ] **Step 2: Verify compilation**

Open Unity — confirm no errors in the Console. The `SockPair` prefab will show missing serialized fields for `sockPrefabLeft` / `sockPrefabRight` — this is expected; those fields are intentionally removed.

- [ ] **Step 3: Commit**

```bash
git add Assets/Socks/Scripts/SockPair.cs
git commit -m "feat: sockpair setup/decompose with sock refs, kinematic hold"
```

---

### Task 3: `SockSelector.cs` — rewrite to new state machine

**Files:**
- Modify: `Assets/Socks/Scripts/SockSelector.cs`

- [ ] **Step 1: Replace file contents**

Replace the entire contents of `Assets/Socks/Scripts/SockSelector.cs` with:

```csharp
using Rewired;
using UnityEngine;

public class SockSelector : MonoBehaviour
{
    [SerializeField] float     maxDistance   = 10f;
    [SerializeField] LayerMask sockLayerMask = ~0;
    [SerializeField] float     throwForce    = 8f;

    [Header("Segment Manipulation")]
    [SerializeField] string actionInteract = "InteractSock";

    [Header("Pair / Unpair")]
    [SerializeField] string actionPair            = "Pair";
    [SerializeField] string actionUnpair          = "Unpair";
    [SerializeField] float  pairProximityDistance = 0.15f;
    [SerializeField] Color  pairHighlightColor    = Color.green;

    [Header("Pair Throw")]
    [SerializeField] SockPair sockPairPrefab;
    [SerializeField] float    pairThrowForce = 8f;

    Player   _player;
    Sock     _current;
    SockPair _currentPair;

    bool _isManipulating;
    Sock _manipulatingSock;
    Sock _pairCandidate;

    bool     _isHoldingPair;
    SockPair _heldPair;

    void Awake() => _player = ReInput.players.GetPlayer(0);

    void Update()
    {
        HandleManipulation();

        if (_isHoldingPair)
        {
            HandlePairHold();
            return;
        }

        if (_isManipulating)
        {
            TryPair();
            return;
        }

        TryPickUpPair();
        UpdateSelection();
        UpdateSegmentHover();
    }

    void HandleManipulation()
    {
        if (_player.GetButtonDown(actionInteract) && _current != null && !_isManipulating && !_isHoldingPair)
        {
            if (_current.StartManipulating(Camera.main))
            {
                _isManipulating   = true;
                _manipulatingSock = _current;
            }
        }

        if (_isManipulating)
        {
            if (_player.GetButton(actionInteract))
            {
                _manipulatingSock?.UpdateManipulation(Camera.main);
                UpdatePairCandidate();
            }

            if (_player.GetButtonUp(actionInteract))
                EndManipulation();
        }
    }

    void UpdatePairCandidate()
    {
        Rigidbody bone = _manipulatingSock?.GetManipulatedBone();
        if (bone == null) { ClearPairCandidate(); return; }

        Sock  nearest     = null;
        float nearestDist = float.MaxValue;
        foreach (Sock s in Sock.AllSocks)
        {
            if (s == _manipulatingSock || !s.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(bone.position, s.transform.position);
            if (d < pairProximityDistance && d < nearestDist) { nearest = s; nearestDist = d; }
        }

        if (nearest == _pairCandidate) return;
        _pairCandidate?.HidePairHighlight();
        _pairCandidate = nearest;
        _pairCandidate?.ShowPairHighlight(pairHighlightColor);
    }

    void ClearPairCandidate()
    {
        _pairCandidate?.HidePairHighlight();
        _pairCandidate = null;
    }

    void EndManipulation()
    {
        _manipulatingSock?.StopManipulating();
        ClearPairCandidate();
        _isManipulating   = false;
        _manipulatingSock = null;
    }

    void TryPair()
    {
        if (!_player.GetButtonDown(actionPair) || _pairCandidate == null) return;

        Sock      sockA    = _manipulatingSock;
        Sock      sockB    = _pairCandidate;
        Rigidbody bone     = sockA.GetManipulatedBone();
        Vector3   spawnPos = bone != null
            ? (bone.position + sockB.transform.position) * 0.5f
            : sockB.transform.position;

        ClearPairCandidate();
        EndManipulation();

        SockPair pair = Instantiate(sockPairPrefab, spawnPos, Quaternion.identity);
        pair.Setup(sockA, sockB);
    }

    void TryPickUpPair()
    {
        if (!_player.GetButtonDown(actionInteract) || _currentPair == null) return;

        _currentPair.Unhighlight();
        _currentPair.StartHold(Camera.main);
        _heldPair      = _currentPair;
        _currentPair   = null;
        _isHoldingPair = true;
    }

    void HandlePairHold()
    {
        if (_player.GetButton(actionInteract))
            _heldPair.UpdateHold(Camera.main);

        if (_player.GetButtonDown(actionUnpair))
        {
            _heldPair.Decompose();
            _heldPair      = null;
            _isHoldingPair = false;
            return;
        }

        if (_player.GetButtonUp(actionInteract))
        {
            _heldPair.StopHold(Camera.main.transform.forward * pairThrowForce);
            _heldPair      = null;
            _isHoldingPair = false;
        }
    }

    void UpdateSegmentHover()
    {
        if (_current == null) return;
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        _current.UpdateHoveredSegment(ray);
    }

    void UpdateSelection()
    {
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));

        Sock     hitSock = null;
        SockPair hitPair = null;

        if (Physics.Raycast(ray, out RaycastHit info, maxDistance, sockLayerMask))
        {
            hitSock = info.collider.GetComponentInParent<Sock>();
            if (hitSock == null)
                hitPair = info.collider.GetComponentInParent<SockPair>();
        }

        if (hitSock != _current)
        {
            _current?.Unhighlight();
            _current?.ClearHoveredSegment();
            _current = hitSock;
            _current?.Highlight();
        }

        if (hitPair != _currentPair)
        {
            _currentPair?.Unhighlight();
            _currentPair = hitPair;
            _currentPair?.Highlight();
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

Open Unity — confirm no errors in the Console. The `SockSelector` component in the scene will show missing values for the removed hand-slot fields — clear them manually in the Inspector. The new fields (`actionUnpair`, `pairProximityDistance`, `pairHighlightColor`) appear with sensible defaults.

- [ ] **Step 3: Commit**

```bash
git add Assets/Socks/Scripts/SockSelector.cs
git commit -m "feat: sock selector — bone manipulation + proximity pairing state machine"
```

---

### Task 4: Inspector setup + manual integration test

**Files:** No code changes — Inspector configuration and playtest.

- [ ] **Step 1: Clean up SockSelector Inspector**

Select the GameObject with `SockSelector` in the scene. In the Inspector:
- `actionUnpair` → `"Unpair"` (must match the Rewired action name exactly)
- `pairProximityDistance` → start with `0.15` (tune in playtest)
- `pairHighlightColor` → green (default)
- `sockPairPrefab` → assign `Assets/Socks/Prefabs/SockPair.prefab`
- Remove any leftover null/missing fields from the old system

- [ ] **Step 2: Clean up SockPair prefab**

Open `Assets/Socks/Prefabs/SockPair.prefab`. Confirm:
- `rendererLeft` and `rendererRight` are still assigned
- No missing-reference warnings for `sockPrefabLeft` / `sockPrefabRight` (fields are gone)

- [ ] **Step 3: Confirm Rewired has the Unpair action**

Open the Rewired Input Manager asset. Confirm an action named exactly `"Unpair"` exists and is bound to a key/button.

- [ ] **Step 4: Playtest — pairing**

Enter Play mode. Expected:
1. Aim at a sock → white hover outline appears
2. Hold `InteractSock` on a bone → bone follows screen center
3. Bring bone near another sock → both socks show green outline
4. Move away → green outlines disappear
5. Get close again, press `Pair` → `SockPair` spawns at midpoint, both original socks disappear

- [ ] **Step 5: Playtest — SockPair pickup and throw**

1. Aim at the `SockPair` → white highlight appears
2. Hold `InteractSock` → pair becomes kinematic, follows screen center
3. Release `InteractSock` → pair is thrown forward

- [ ] **Step 6: Playtest — Unpair**

1. Pick up a `SockPair` (hold `InteractSock`)
2. Press `Unpair` → pair is destroyed, both original socks reappear at the pair's position with physics

- [ ] **Step 7: Commit**

```bash
git commit --allow-empty -m "test: manual integration — sock pairing redesign verified"
```
