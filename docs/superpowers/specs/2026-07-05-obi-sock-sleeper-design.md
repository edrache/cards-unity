# Obi Sock Sleeper — Kinematic Freeze for Idle Cloth Actors

**Date:** 2026-07-05
**Branch:** game/socks-obicloth

## Problem

Socks spawned by `SockSpawner` are pure Obi Cloth actors (`ObiSock.prefab`: `ObiCloth` + `ObiClothRenderer`, no rigidbody chain). Every spawned sock keeps running full XPBD cloth simulation (distance/bending/aerodynamics/self-collision constraints) even after it has settled on the floor and nobody is interacting with it. With many socks on screen this is wasted CPU/GPU work for objects that are visually static.

The goal is to stop simulating settled socks while leaving their mesh exactly as it is, and to resume simulation only for the sock being interacted with and any settled socks near it (so they react correctly if bumped).

## Rejected Approach: Removing the Actor from the Solver

Fully disabling the `ObiCloth` component (`OnDisable` → `RemoveFromSolver`) gives the largest possible savings (the actor's particles are entirely removed from the solver), and Obi already auto-snapshots particle state on unload/reload so the simulation resumes where it left off. However, this was rejected for two reasons discovered during investigation:

1. `ObiClothRenderer.CleanupRenderer()` resets `MeshFilter.sharedMesh` back to the original undeformed input mesh when the renderer unregisters — the sock would visually pop back to its flat rest shape while asleep, contradicting the goal of leaving the mesh as-is.
2. A sleeping actor removed from the solver provides no collision presence — other active socks would pass through it. Fixing this would require a stand-in Unity collider approximating the current cloth shape, which is fragile (shape mismatch, visual/physical mismatch) and was explicitly rejected as too risky.

## Solution Overview

A new per-actor component, `ObiSockSleeper`, sits on the `ObiSock` prefab root next to `ObiCloth`/`ObiClothRenderer`. Instead of removing the actor from the solver, it freezes it in place by setting all of the actor's particles to `invMass = 0` ("kinematic freeze"). The actor stays registered in the `ObiSolver`:

- It keeps colliding with other active cloth actors (it's still a real obstacle at its exact current shape — no stand-in collider needed).
- `ObiClothRenderer` keeps rendering it from the (now static) particle positions — no mesh reset, no baking needed.
- The solver skips the expensive parts (force integration, distance/bending/aerodynamics/self-collision constraint solving) for its particles, since a zero-inverse-mass particle cannot move under those constraints.

Waking it back up restores each particle's original `invMass` from the source blueprint.

## States

| State | Meaning | Particle `invMass` |
|---|---|---|
| `Awake` | Simulated normally | Original value from blueprint |
| `Asleep` | Frozen in place, still collidable | `0` |

## Transition: Awake → Asleep

Checked in `FixedUpdate`, only for actors currently `Awake` and not the actively-dragged actor. A rest timer accumulates while every particle's velocity magnitude (read from `solver.velocities[actor.solverIndices[i]]`) stays below `sleepVelocityThreshold`. Any particle exceeding the threshold resets the timer to zero. Once the timer reaches `minRestTimeBeforeSleep`, the actor goes `Asleep` (freeze all particles).

## Transition: Asleep → Awake

Triggered by one of:
1. **Direct grab** — `SockDragController` begins dragging this actor.
2. **Proximity to a grabbed sock** — when a drag begins on some actor A, every `ObiSockSleeper` within `wakeUpRadius` of A's particles wakes up too (mirrors the old `SockSimulator.WakeNearbySocks` pattern).

Waking is immediate: restore original per-particle `invMass` values, reset the rest timer, and (if `minActiveTimeAfterWake > 0`) suppress the sleep check for that grace period, exactly like the post-throw grace period in the old `SockSimulator`. Setting `minActiveTimeAfterWake` to `0` disables the grace period entirely — the sleep check runs as soon as the rest timer would allow it.

A sock currently being dragged (its `ObiParticleAttachment` is `enabled` with `target` set to the dragger) is never a candidate for sleep, regardless of particle velocity.

## Configuration (`ObiSockSleeper` SerializeFields)

| Field | Default | Description |
|---|---|---|
| `sleepVelocityThreshold` | `0.05f` | Particle speed below which it counts as "at rest" |
| `minRestTimeBeforeSleep` | `0.5f` | Seconds all particles must stay below the threshold before sleeping |
| `wakeUpRadius` | `0.4f` | Radius around a newly-grabbed sock's particles in which sleeping socks wake up |
| `minActiveTimeAfterWake` | `1.0f` | Grace period after waking before the sleep check resumes; `0` disables it |

Defaults mirror the equivalent fields in the old `SockSimulator` for consistency.

## Integration with Existing Code

- **`ObiSockSleeper` (new)**: lives on `ObiSock.prefab`, auto-registers into a static `ObiSockSleeper.All` list in `Awake()` / removes itself in `OnDestroy()`, mirroring the `Sock.AllSocks` pattern. Resolves its own `ObiCloth`, `ObiClothRenderer`, and `ObiParticleAttachment[]` via `GetComponent`/`GetComponents`.
- **`SockDragController.cs`**: needs a small additive change — expose which `ObiActor` a drag started/ended on (current `DragStarted`/`DragEnded` events only carry a screen position). This is used to (a) wake the grabbed actor and (b) run the proximity wake-up against `ObiSockSleeper.All`.
- **`SockSpawner.cs` / `SockGrabAutoBinder.cs`**: no changes needed — `ObiSockSleeper` attaches and finds its dependencies the same way other per-sock components already do.

## Out of Scope

- Removing actors from the solver entirely / mesh baking (rejected approach above) — could be revisited later for socks that are guaranteed far from any interaction, but not needed now.
- Any change to hover/segment-highlight behavior — hover detection works off transform positions and is unaffected by an actor being asleep.
- Waking based on camera/player distance (`CameraWalk`) — proximity is defined relative to the currently-dragged sock, not the camera.
