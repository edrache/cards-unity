# Sock Simulator — Active/Dormant State Design

**Date:** 2026-06-09  
**Branch:** game/sock-detailed

## Problem

Socks have complex skinned meshes with bones and a physics chain (HingeJoints, Rigidbodies, SockFabricPhysics). This is expensive to run when a sock is simply lying on the floor. The goal is to deactivate physics and bone simulation when the sock is at rest, while keeping it visually identical.

## Solution Overview

A new `SockSimulator` component manages two states: **Active** and **Dormant**. `Sock.cs` notifies it on pickup and throw. `SockSimulator` decides when to transition to Dormant after the sock settles.

## States

| State | Condition | What is active |
|---|---|---|
| `Active` | Just picked up or recently thrown | `SockBoneFollower`, `SockFabricPhysics`, Rigidbody physics |
| `Dormant` | At rest on floor | All simulation disabled, Rigidbodies kinematic, bones frozen in last pose |

## Transition: Active → Dormant

Triggered after throw when **both** of the following are true simultaneously:
1. At least `minActiveTimeAfterThrow` seconds have passed since the throw
2. All Rigidbodies satisfy: `rb.IsSleeping()` OR `rb.velocity.magnitude < sleepVelocityThreshold`

Checked in `FixedUpdate` only after the minimum time has elapsed (avoids checking every frame before the timer expires).

## Transition: Dormant → Active

Triggered immediately on `OnPickedUp()`. No delay.

## What Changes on Transition

**Going Dormant:**
- `SockBoneFollower.enabled = false`
- All `SockFabricPhysics.enabled = false`
- All `Rigidbody.isKinematic = true`

Bones remain in the last pose they were in — visually identical to the settled sock.

**Going Active:**
- All `Rigidbody.isKinematic = false`
- All `SockFabricPhysics.enabled = true`
- `SockBoneFollower.enabled = true`

## Configuration (SockSimulator SerializeFields)

| Field | Default | Description |
|---|---|---|
| `minActiveTimeAfterThrow` | `1.5f` | Minimum seconds after throw before dormant check starts |
| `sleepVelocityThreshold` | `0.05f` | Velocity below which a Rigidbody is considered at rest |

## Integration with Sock.cs

`Sock.cs` calls:
- `SockSimulator.OnPickedUp()` at the start of `PickUp()` and `PickUpPaired()`
- `SockSimulator.OnThrown()` at the start of `Throw()`

`SockSimulator` finds its dependencies (`SockBoneFollower`, all `SockFabricPhysics`, all `Rigidbody`) in `Awake` via `GetComponentsInChildren`.

## Out of Scope

- Baking SkinnedMesh to static MeshRenderer (Approach B) — deferred as a potential later optimization
- Automatic dormant on scene load (socks start dormant) — can be added as a follow-up
