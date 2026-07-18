# Active Ragdoll Rig Audit

## Scene

- Target scene: `Assets/Scenes/scn_character.unity`
- Audit date: `2026-07-12`
- Scope: Package 1 audit for the standing active ragdoll balance system
- Status: `DONE`

## Summary

The character scene contains a clean imported humanoid animation rig, but no ragdoll setup yet. The floor is the only object with a visible physics collider. This means the physics rig for the character can be authored from scratch without first untangling an existing ragdoll configuration.

The most important implementation decision for the next package is root ownership:

- `Hips` should be treated as the physical root body for the initial active ragdoll MVP.
- `Player` should remain the world-space owner and high-level scene object.
- `Root_Player` should remain a structural bridge unless later implementation proves it should own offsets explicitly.

This gives the rig a clear physical center while keeping scene ownership and future tooling straightforward.

## Current Scene Findings

### Physics State

- The character currently has no `Rigidbody` components.
- The character currently has no `ConfigurableJoint` or `CharacterJoint` components.
- The character currently has no authored ragdoll colliders.
- The floor `Cube` has the only obvious scene collider in the current setup.

### Root Ownership

The imported hierarchy currently has three relevant transform layers:

- `Player`
- `Root_Player`
- `Hips`

Observed ownership:

- `Player` owns the `Animator`.
- `Root_Player` is an intermediate transform below `Player`.
- `Hips` is the skeletal root used by the mesh as `m_RootBone`.

For Package 2 and the first standing-balance prototype, the recommended split is:

- `Player`: world owner, debug entry point, and future high-level controller host
- `Hips`: physical root `Rigidbody`
- `Root_Player`: retained as hierarchy support, not first-choice physics owner

## Recommended Initial Physics Segments

These are the recommended rigidbody segments for the first standing balance MVP.

### Core body

- `Hips`
- `Spine`
- `Spine1`
- `Spine2`
- `Neck`
- `Head`

### Legs

- `LeftUpLeg`
- `LeftLeg`
- `LeftFoot`
- `RightUpLeg`
- `RightLeg`
- `RightFoot`

### Arms

- `LeftShoulder`
- `LeftArm`
- `LeftForeArm`
- `RightShoulder`
- `RightArm`
- `RightForeArm`

## Segments To Exclude In The First Pass

These bones should stay out of the first ragdoll implementation unless a later package explicitly needs them.

- `LeftToe_End`
- `RightToe_End`
- `LeftHand`
- `RightHand`
- finger chains
- thumb chains

Optional later additions:

- `LeftToeBase`
- `RightToeBase`

Toe bases may become useful once support stepping and foot contact are more mature, but they are not required for the initial standing prototype.

## Recommended Joint Chain

The first authored joint hierarchy should follow the imported bone chain closely:

- `Hips -> Spine -> Spine1 -> Spine2 -> Neck -> Head`
- `Hips -> LeftUpLeg -> LeftLeg -> LeftFoot`
- `Hips -> RightUpLeg -> RightLeg -> RightFoot`
- `Spine2 -> LeftShoulder -> LeftArm -> LeftForeArm`
- `Spine2 -> RightShoulder -> RightArm -> RightForeArm`

This keeps the first physics model small enough to tune while still supporting whole-body balance reactions.

## Key Risks

### Imported pelvis orientation

`Hips` appears to use an imported local basis that is not scene-aligned. The first physics package should not assume that local bone axes map cleanly to intuitive world-space balance axes.

Implication:

- joint frames should be authored deliberately,
- pelvis and leg joint axes should be validated visually,
- balance torque and target rotation code should not be built on blind local-axis assumptions.

### Animator update mode

The current `Animator` is configured as a standard animated character rather than a physics-synced ragdoll driver.

Implication:

- active ragdoll control should be driven from `FixedUpdate`,
- future animation pose sampling and physics driving need clear separation,
- jitter risk will be high if animation and physics ownership are mixed carelessly.

### Minimal test environment

The scene currently contains:

- one flat floor,
- one camera,
- one directional light,
- one character.

This is good for a clean first pass, but not enough for serious validation.

Implication:

- later packages should add a controlled test driver,
- perturbation tools should be part of the development loop,
- slope and uneven-ground cases should wait until the base standing controller is stable.

## Package 2 Guidance

Package 2 should proceed with these assumptions:

1. Build the first ragdoll from scratch.
2. Use `Hips` as the physical root body.
3. Keep `Player` as the high-level world owner.
4. Start with pelvis, torso, head, upper/lower legs, feet, and upper/lower arms.
5. Leave fingers, hands, and toe-end bones out of the first pass.
6. Author joint orientation carefully instead of trusting imported local axes.

## Done Criteria For Package 1

Package 1 is considered complete because:

- the scene has been audited,
- the core rigidbody segments have been selected,
- the initial joint chain has been selected,
- root ownership has a concrete recommendation,
- the next implementation package has enough guidance to begin work safely.
