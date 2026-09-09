# Torch attack design

Date: 2026-09-09

## Goal

Add a Rewired `Attack` action and a procedural right-arm swing with the handheld torch: an overhead
chop, cocked by holding the button and released as the strike. Each strike costs torch fuel.
Animation only; hit detection and damage are out of scope.

## Constraint that shapes the design

`HandheldTorch` runs in `LateUpdate` at execution order 100 and overwrites the right upper arm
and forearm with the holding pose, then locks the torch to an upright world rotation. Any swing
must therefore run after it and temporarily take over both the arm and the torch.

## Approach

A separate `TorchAttack` component at `[DefaultExecutionOrder(150)]`, plus one new public method on
`HandheldTorch`.

Rejected alternatives: a new gait style in `CartoonCharacterGait` (its styles are distance-driven
blend weights, not timed events, and it would not resolve the torch conflict), and an Animator clip
(the rig is procedural, with no Avatar, and a clip would fight the torch every frame).

## Components

- **Rewired**: action `Attack`, id 4, button, Default category, bound to Space. Gamepad binding left
  to the editor, because joystick element ids depend on the hardware GUID.
- **`ProceduralCharacter`**: stays the single input reader. Reads the held button state on the
  Rewired path and the Input System fallback, then feeds `TorchAttack.SetAttackHeld` every frame.
- **`TorchAttack`**: three phases (charging, striking, recovering) with per-phase durations. Holding
  the button ramps the pose weight in and then parks the arm in the cocked pose; releasing strikes.
  A release is deferred until the windup finishes, so a tap still swings from the top. Only the
  strike uses an ease-out curve, and the recovery merely fades the weight out, which is what returns
  the arm to the holding pose. Exposes `Strike`, `HitWindowOpen`, `IsAttacking`, `IsCharging`,
  `ChargeTime`, `SwingProgress`, and a `Tick(float)` overload for deterministic checks. Optional
  chest twist layered over `ProceduralSpine`, self-undoing so it cannot accumulate.
- **`HandheldTorch`**: `SetPoseSuppression(float)` fades out the holding pose and the upright lock,
  so the torch rides the forearm. `Strike Lifetime Cost` (seconds, default 5, authored on
  `Handheld Torch.prefab`) is charged by `ConsumeStrikeFuel()` when a strike begins, ageing the torch
  exactly as burning for that long would. Getters `UpperArm`, `Forearm`, `Holder` let `TorchAttack`
  auto-resolve its rig.

## Behaviour

Movement, turning and the gait continue during a swing; only the right arm, the torch and optionally
the chest are overridden. A press during a swing is ignored rather than buffered.

## Verification

No automated suite exists in this project and none was added. Verified by stepping `Tick` and
`ApplyPose` against `HandheldTorch.Tick` in Edit Mode, plus a camera capture of the frozen poses.
Physical key presses were not verified, because the relay assembly cannot reference Rewired.
