# Torch attack design

Date: 2026-09-09

## Goal

Add a Rewired `Attack` action and a procedural right-arm swing with the handheld torch:
an overhead chop. Animation only; hit detection and damage are out of scope.

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
- **`ProceduralCharacter`**: stays the single input reader. Reads the button on the Rewired path and
  the Input System fallback, then calls `TorchAttack.TryStrike()`.
- **`TorchAttack`**: three-phase timeline (windup, strike, recover) with per-phase durations. The
  pose weight ramps in, holds, and fades out; the fade is what returns the arm to the holding pose.
  Only the strike uses an ease-out curve. Angles interpolate the upper arm pitch and elbow bend.
  Exposes `Strike`, `HitWindowOpen`, `IsAttacking`, `NormalizedTime`, and a `Tick(float)` overload
  for deterministic checks. Optional chest twist layered over `ProceduralSpine`, self-undoing so it
  cannot accumulate.
- **`HandheldTorch`**: `SetPoseSuppression(float)` fades out the holding pose and the upright lock,
  so the torch rides the forearm. Getters `UpperArm`, `Forearm`, `Holder` let `TorchAttack`
  auto-resolve its rig.

## Behaviour

Movement, turning and the gait continue during a swing; only the right arm, the torch and optionally
the chest are overridden. A press during a swing is ignored rather than buffered.

## Verification

No automated suite exists in this project and none was added. Verified by stepping `Tick` and
`ApplyPose` against `HandheldTorch.Tick` in Edit Mode, plus a camera capture of the frozen poses.
Physical key presses were not verified, because the relay assembly cannot reference Rewired.
