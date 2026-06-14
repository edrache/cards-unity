# Sock Pairing Redesign

**Date:** 2026-06-14  
**Branch:** game/sock-detailed  

## Overview

Replace the two-hand (InteractLeft/InteractRight) sock pickup system with a bone-manipulation + proximity-based pairing flow. The player picks up a sock by grabbing its bone via `InteractSock`, brings it close to another sock, and presses `Pair` to create a `SockPair`. The pair can be picked up, thrown, and decomposed back into individual socks via `Unpair`.

## Rewired Actions

| Action | Behaviour |
|---|---|
| `InteractSock` | Hold to manipulate a sock bone; hold to carry a SockPair |
| `Pair` | While manipulating a bone near a candidate sock → create SockPair |
| `Unpair` | While holding a SockPair → decompose it back to two individual socks |

Actions `InteractLeft` and `InteractRight` are no longer used.

## State Machine (SockSelector)

Three mutually exclusive states:

| State | Entry | Exit |
|---|---|---|
| **Idle** | default | — |
| **ManipulatingSock** | `InteractSock` down on a Sock bone | `InteractSock` up, or `Pair` success |
| **HoldingPair** | `InteractSock` down on a SockPair | `InteractSock` up (throw), or `Unpair` |

## Proximity Detection

- Runs every frame while in **ManipulatingSock** state.
- Iterates `Sock.AllSocks` (static registry, registered in `Awake` / deregistered in `OnDestroy`).
- Measures distance from the manipulated `Rigidbody.position` to each candidate's `transform.position`.
- Candidate must not be the sock currently being manipulated.
- On candidate change: call `HidePairHighlight()` on the old candidate, `ShowPairHighlight(color)` on the new one.
- On manipulation end (any cause): clear candidate highlight.
- Proximity threshold and highlight color are serialized fields on `SockSelector` (configurable in Inspector).

## Pairing

When `Pair` is pressed while in **ManipulatingSock** and `_pairCandidate != null`:

1. Compute spawn position = midpoint between manipulated bone and candidate's `transform.position`.
2. Instantiate `SockPair` prefab at that position.
3. Call `SockPair.Setup(manipulatingSock, candidate)` — sets materials and deactivates both sock GameObjects.
4. End manipulation (`StopManipulating`, clear state).

## SockPair Hold & Throw

`InteractSock` pressed while `_currentPair != null` (Idle state):

1. `SockPair.StartHold(camera)` — make Rigidbody kinematic, record hold distance.
2. Each frame (`GetButton`): `SockPair.UpdateHold(camera)` — move to `ray.origin + ray.direction * holdDistance`.
3. `InteractSock` released: `SockPair.StopHold(camera.forward * pairThrowForce)` — disable kinematic, add impulse.

## Unpairing

`Unpair` pressed while in **HoldingPair**:

1. `SockPair.Decompose()` — sets both sock `GameObjects` active, moves them to the pair's position, returns `(Sock a, Sock b)`.
2. `Destroy(SockPair.gameObject)`.
3. Transition to **Idle**.

## File Changes

### `Sock.cs`
- Add `static List<Sock> AllSocks` — register in `Awake`, deregister in `OnDestroy`.
- Add `ShowPairHighlight(Color color)` — enables outline keyword and sets outline color.
- Add `HidePairHighlight()` — disables outline keyword, restores original outline color.

### `SockPair.cs`
- Remove `sockPrefabLeft`, `sockPrefabRight`.
- Add `_sockA`, `_sockB` (Sock references).
- Add `Setup(Sock sockA, Sock sockB)` — stores references, assigns `sharedMaterial` from each sock, calls `CacheMaterials()`, deactivates both sock GameObjects.
- Add `Decompose()` — activates both socks, teleports them to `transform.position`, destroys self.
- Add `StartHold(Camera)`, `UpdateHold(Camera)`, `StopHold(Vector3)` — kinematic follow.

### `SockSelector.cs`
- **Remove:** `actionLeft/Right`, `slotsLeft/Right`, `pairSlotsLeft/Right`, `_heldLeft/Right`, `_isPaired`, `PickUpPair`, `ThrowPair`, `TogglePair`, `HandleHand`, `ThrowHeld`.
- **Add fields:** `actionUnpair`, `pairProximityDistance`, `pairHighlightColor`, `pairThrowForce` (keep existing).
- **Add state:** `_pairCandidate : Sock`, `_heldPair : SockPair`, `_isHoldingPair : bool`.
- Refactor `Update` to the flow described in the State Machine section above.

## Future: Pair Validation

There is currently no sock-type identity system. When one is introduced (e.g. a `ScriptableObject` sock definition or a `sockTypeId` string on `Sock`), the proximity check in `SockSelector` will be the integration point — it can colour the outline green for a valid pair and red (or suppress it) for an invalid one. No changes to the interfaces designed here are required for this extension.
