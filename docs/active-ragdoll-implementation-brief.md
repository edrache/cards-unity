# Active Ragdoll Implementation Brief

## Purpose

This document defines the implementation backlog for the character balance system in [Assets/Scenes/scn_character.unity](/Users/marek/OfflineDocuments/Repo/Antigravity/cards-unity/Assets/Scenes/scn_character.unity).

The target outcome is a humanoid character that:
- stands as a physics-driven active ragdoll,
- recovers balance after pushes and root offsets,
- performs support steps when balance cannot be recovered in place,
- later supports head and arm targets without collapsing.

This brief is written to support implementation by separate agents working in small, reviewable branches.

## Constraints

- All code, comments, and documentation must be written in English.
- Conversation with the user remains in Polish.
- Runtime code should live under `Assets/Scripts/`.
- Keep logic incremental and scene-safe. Do not regenerate `.meta` files unless necessary.
- Prefer debug-friendly systems over ambitious full locomotion.
- Build the system in layers: rig first, active pose control second, balance sensing third, stepping fourth.

## Current Context

- The project currently has a humanoid scene at `Assets/Scenes/scn_character.unity`.
- The scene contains a `Player` hierarchy with a humanoid skeleton and an `Animator`.
- `Assets/Scripts/` is currently empty, so the architecture can be introduced cleanly.
- The initial objective is not full procedural walking. The first milestone is support stepping for balance recovery.

## Recommended Script Layout

```text
Assets/Scripts/
  Config/
    ActiveRagdollTuning.cs
    BalanceTuning.cs
    StepTuning.cs
  Controllers/
    ActiveRagdollController.cs
    BalanceController.cs
    StepPlanner.cs
    StepExecutor.cs
    UpperBodyTargetController.cs
  Data/
    BalanceState.cs
    StepSide.cs
    StepRequest.cs
  Runtime/
    CharacterPhysicsRig.cs
    CenterOfMassTracker.cs
  Tests/
    EditMode/
      BalanceControllerTests.cs
      StepPlannerTests.cs
  Debug/
    BalanceDebugGizmos.cs
    BalanceTestDriver.cs
```

## Delivery Strategy

Implementation should be split into 12 packages. The recommended order matters because later packages depend heavily on stable earlier behavior.

## Status Tracking

Progress is tracked in `docs/active-ragdoll-progress.md`.

Current summary:

- `Package 1` is complete.
- `Packages 2-5, 9, and 12` have working scaffolding or partial implementation.
- `Packages 6-8, 10, and 11` have not started in a meaningful way yet.

## Package 1: Rig Audit And Architecture

### Objective

Audit the existing character scene and define the exact physical rig layout before implementation starts.

### Scope

- Inspect the `Player` hierarchy in `scn_character.unity`.
- Identify the physical root, visual root, and the main humanoid bone chain.
- Define which bones should receive `Rigidbody`, `Collider`, and `ConfigurableJoint`.
- Define layer and collision matrix expectations.
- Define scene references needed by future controllers.

### Files To Create Or Update

- `docs/active-ragdoll-implementation-brief.md`
- Optionally a second reference document if the agent wants to record exact bone mappings.

### Acceptance Criteria

- A clear list exists for all physics segments.
- A clear list exists for all joints between segments.
- The intended root ownership is documented.
- The team can move to rig creation without unresolved structure questions.

### Risks

- Choosing too many rigid bodies will create instability and tuning cost.
- Choosing too few rigid bodies will reduce believable recovery behavior.
- Mixing visual and physical roots too early can make later control logic confusing.

### Agent Prompt

```text
Audit the humanoid character in Assets/Scenes/scn_character.unity and document the active ragdoll rig plan. Identify the physical root, the main bone chains, the rigidbody segments, the collider plan, the configurable joint plan, and any scene-level risks. Do not implement behavior yet. Keep all written output in English.
```

## Package 2: Physics Rig Builder

### Objective

Turn the character into a stable physical ragdoll foundation.

### Scope

- Add `Rigidbody` components to the chosen body segments.
- Add colliders for trunk, limbs, and feet.
- Add `ConfigurableJoint` components linking the segments.
- Tune initial mass distribution and joint constraints.
- Configure collision layers to reduce destructive self-collision.

### Files To Create Or Update

- `Assets/Scenes/scn_character.unity`
- `Assets/Scripts/Runtime/CharacterPhysicsRig.cs`

### Acceptance Criteria

- The character enters Play Mode without explosive instability.
- The rig falls and collides in a predictable way.
- Segments remain connected under ordinary gravity and mild impact.
- The scene can be used as a stable base for active control.

### Risks

- Poor mass ratios can destabilize everything that comes later.
- Loose angular limits may cause twisting and collapse.
- Overlapping colliders may create constant jitter.

### Agent Prompt

```text
Implement the first physical rig pass for the humanoid in Assets/Scenes/scn_character.unity. Add rigidbodies, colliders, configurable joints, and a CharacterPhysicsRig component under Assets/Scripts/Runtime. Optimize for stability and predictable behavior, not realism perfection. Keep code and comments in English.
```

## Package 3: Active Ragdoll Core

### Objective

Drive the ragdoll toward a standing reference pose through joint motors or equivalent joint drive settings.

### Scope

- Introduce a standing reference pose model.
- Drive joints toward target rotations.
- Add tuning profiles for hips, spine, legs, and arms.
- Support toggling between passive and active modes.
- Expose enough inspector settings for iterative tuning.

### Files To Create Or Update

- `Assets/Scripts/Controllers/ActiveRagdollController.cs`
- `Assets/Scripts/Config/ActiveRagdollTuning.cs`
- `Assets/Scenes/scn_character.unity`

### Acceptance Criteria

- In active mode, the character attempts to regain upright posture after light disturbances.
- In passive mode, the character behaves like a normal ragdoll.
- Joint drives can be tuned in Inspector without code changes.

### Risks

- Excessive drive strength can cause jitter and energy injection.
- Weak drives can make balance detection meaningless because the character never recovers.
- Reference pose capture may be inconsistent if the rig is not initialized carefully.

### Agent Prompt

```text
Build the active ragdoll core for the physical humanoid. Create an ActiveRagdollController and ActiveRagdollTuning assets/classes that drive joints toward a standing pose. Support passive and active modes, expose practical tuning parameters, and optimize for stability and debuggability. Keep all code and comments in English.
```

## Package 4: Balance Sensing

### Objective

Measure whether the character is balanced, recovering, or needs a step.

### Scope

- Compute approximate center of mass from rigidbodies.
- Track support points at the feet.
- Measure body lean and COM projection.
- Introduce a simple balance state machine.
- Add debug gizmos for COM, support area, and active state.

### Files To Create Or Update

- `Assets/Scripts/Runtime/CenterOfMassTracker.cs`
- `Assets/Scripts/Controllers/BalanceController.cs`
- `Assets/Scripts/Data/BalanceState.cs`
- `Assets/Scripts/Debug/BalanceDebugGizmos.cs`
- `Assets/Scenes/scn_character.unity`

### Acceptance Criteria

- COM can be visualized in the scene.
- The system distinguishes at least `Stable`, `Recovering`, `NeedsStep`, and `Falling`.
- State transitions match visible balance behavior well enough for debugging.

### Risks

- A bad COM approximation will produce unreliable step decisions.
- Foot support detection may be wrong if grounded state is too naive.
- Overcomplicated support polygon logic is unnecessary for the MVP.

### Agent Prompt

```text
Implement balance sensing for the active ragdoll. Create a CenterOfMassTracker, a BalanceController, a BalanceState model, and debug gizmos that visualize COM, support area, and balance state. Optimize for reliable support-step decisions rather than mathematically perfect biomechanics. Keep code and comments in English.
```

## Package 5: Step Planning MVP

### Objective

Decide when a support step is required and where the foot should land.

### Scope

- Choose which foot is allowed to step.
- Add thresholds for step triggering.
- Generate a `StepRequest` with side, target point, and timing data.
- Use ground raycasts to project step targets onto terrain.
- Prevent both feet from attempting to step simultaneously.

### Files To Create Or Update

- `Assets/Scripts/Controllers/StepPlanner.cs`
- `Assets/Scripts/Config/StepTuning.cs`
- `Assets/Scripts/Data/StepSide.cs`
- `Assets/Scripts/Data/StepRequest.cs`

### Acceptance Criteria

- Larger balance errors generate sensible step requests.
- Step side selection is stable and explainable.
- Step targets stay within a believable reach envelope.

### Risks

- Overeager stepping will create constant shuffling.
- Conservative thresholds will delay stepping until collapse.
- Ignoring body velocity may produce backward or sideways target errors.

### Agent Prompt

```text
Implement a support-step planner for the active ragdoll. Create StepPlanner, StepSide, StepRequest, and StepTuning so the system can decide when a support step is needed, which foot should move, and where the foot should land on the ground. Focus on MVP balance recovery, not full locomotion. Keep code and comments in English.
```

## Package 6: Step Execution MVP

### Objective

Execute support steps with a clear lift, swing, and plant sequence.

### Scope

- Consume `StepRequest` objects.
- Move the selected foot target through a simple procedural arc.
- Manage foot lock and support status during the step.
- Update planted support state after landing.
- Keep the step timing readable and tunable.

### Files To Create Or Update

- `Assets/Scripts/Controllers/StepExecutor.cs`
- `Assets/Scenes/scn_character.unity`

### Acceptance Criteria

- A selected foot performs a visible support step without teleporting.
- The step reduces the current balance error in common cases.
- The planted foot becomes a stable support point again after landing.

### Risks

- Planting too early or too late can worsen balance.
- If support state is not synchronized, both feet may become unstable.
- Directly moving physical bones can fight the active ragdoll controller.

### Agent Prompt

```text
Implement the first procedural support-step executor for the active ragdoll. Create a StepExecutor that consumes StepRequest data and moves the chosen foot through lift, swing, and plant phases. The goal is readable, stable support stepping for balance recovery. Keep code and comments in English.
```

## Package 7: Foot IK And Grounding

### Objective

Make the final foot placement look grounded and natural.

### Scope

- Integrate the project's IK setup for feet.
- Align planted feet to ground height and surface normal.
- Improve foot lock after landing.
- Keep the implementation compatible with uneven surfaces.

### Files To Create Or Update

- `Assets/Scripts/Controllers/StepExecutor.cs`
- `Assets/Scenes/scn_character.unity`
- Additional IK driver scripts if needed

### Acceptance Criteria

- Feet do not visibly float above the surface during plant.
- Feet do not sink heavily into the floor.
- Landing looks more stable than the raw procedural target alone.

### Risks

- IK added too aggressively can fight the physical solver.
- If grounding happens before support state is correct, results may slide.
- Poor orientation alignment may twist the ankle unnaturally.

### Agent Prompt

```text
Integrate foot IK and grounding for the support-step system. Improve landing quality by aligning the planted foot to ground height and normal while staying compatible with the active ragdoll behavior. Prioritize natural contact and stable support over complexity. Keep code and comments in English.
```

## Package 8: Whole-Body Recovery

### Objective

Improve recovery behavior so the entire body helps maintain balance.

### Scope

- Add counter-rotation or compensation in pelvis and torso.
- Shift upper body posture in response to balance error.
- Allow limited arm counterbalance during larger disturbances.
- Clamp overly extreme reactions.

### Files To Create Or Update

- `Assets/Scripts/Controllers/BalanceController.cs`
- `Assets/Scripts/Controllers/ActiveRagdollController.cs`
- `Assets/Scripts/Config/BalanceTuning.cs`

### Acceptance Criteria

- Balance recovery looks like coordinated body behavior, not only foot movement.
- Moderate pushes can be recovered more often than before.
- Reactions remain readable and not wildly exaggerated.

### Risks

- Too much upper-body correction can destabilize the legs.
- Arm counterbalance can conflict with later hand target control.
- Strong torso compensation may look artificial.

### Agent Prompt

```text
Improve the active ragdoll with whole-body recovery behavior. Extend balance and pose control so pelvis, torso, and optionally arms contribute to balance recovery in a controlled way. Optimize for believable recovery under moderate pushes. Keep code and comments in English.
```

## Package 9: Balance Test Driver

### Objective

Create a repeatable in-scene way to perturb the character and evaluate balance behavior.

### Scope

- Add a simple driver for push impulses and root offsets.
- Support several test directions and force levels.
- Expose inspector controls for repeatable testing.
- Optionally support keyboard-triggered debug actions in Play Mode.

### Files To Create Or Update

- `Assets/Scripts/Debug/BalanceTestDriver.cs`
- `Assets/Scenes/scn_character.unity`

### Acceptance Criteria

- The team can trigger repeatable disturbances without manually dragging transforms.
- Push cases can be reproduced during tuning and review.
- The test setup is simple enough for future agents to use safely.

### Risks

- Test perturbations that bypass physics may give misleading results.
- Too much debug convenience can contaminate production logic.

### Agent Prompt

```text
Create a BalanceTestDriver for the character scene so the team can apply repeatable push impulses and balance disturbances during Play Mode. Keep it lightweight, inspector-friendly, and separate from core production logic. Keep code and comments in English.
```

## Package 10: Arms And Head Targets

### Objective

Allow upper-body targeting without destroying standing stability.

### Scope

- Add left hand and right hand targets.
- Add a head look target.
- Define control priority between balance and upper-body target precision.
- Soften target enforcement under severe balance loss.

### Files To Create Or Update

- `Assets/Scripts/Controllers/UpperBodyTargetController.cs`
- `Assets/Scenes/scn_character.unity`

### Acceptance Criteria

- Hands and head can be directed toward targets.
- The character remains generally upright under light and moderate target motion.
- Balance recovery still takes priority over perfect target tracking when necessary.

### Risks

- Hard target enforcement can pull the character off balance instantly.
- A missing priority system will produce controller conflicts.

### Agent Prompt

```text
Implement upper-body target control for the active ragdoll. Add controllable hand targets and a head look target while preserving the balance system as the top priority. If balance is threatened, the controller should sacrifice precision before stability. Keep code and comments in English.
```

## Package 11: Tuning Pass

### Objective

Tune the system into a practical, stable MVP profile.

### Scope

- Review spring, damper, step, and balance thresholds.
- Create a small number of useful behavior presets.
- Reduce jitter, foot sliding, and overreaction.
- Improve inspector defaults.

### Files To Create Or Update

- `Assets/Scripts/Config/ActiveRagdollTuning.cs`
- `Assets/Scripts/Config/BalanceTuning.cs`
- `Assets/Scripts/Config/StepTuning.cs`
- `Assets/Scenes/scn_character.unity`

### Acceptance Criteria

- One stable preset exists for demo and development use.
- Light pushes are recovered in place.
- Larger pushes trigger support stepping rather than immediate collapse in common cases.

### Risks

- Tuning too early wastes time before the feature set stabilizes.
- Tuning without reproducible test cases makes progress hard to verify.

### Agent Prompt

```text
Tune the active ragdoll balance and support-step system into a stable MVP profile. Reduce jitter, sliding, and overreaction, and improve exposed defaults in the tuning classes. Prioritize a practical demo-ready preset over broad configurability. Keep code and comments in English.
```

## Package 12: Tests And Debug Tooling

### Objective

Protect the planning logic and improve iteration speed.

### Scope

- Add EditMode tests for `StepPlanner`.
- Add EditMode tests for balance state transitions where practical.
- Improve debug visualization for COM, step targets, support feet, and state.
- Keep tests focused on deterministic logic, not unstable full-scene physics.

### Files To Create Or Update

- `Assets/Scripts/Tests/EditMode/StepPlannerTests.cs`
- `Assets/Scripts/Tests/EditMode/BalanceControllerTests.cs`
- `Assets/Scripts/Debug/BalanceDebugGizmos.cs`

### Acceptance Criteria

- Deterministic logic has test coverage.
- Future regressions in step selection and state transitions are easier to catch.
- Debug views reveal the information needed to tune the system confidently.

### Risks

- Attempting to unit test full physics behavior will be slow and fragile.
- Debug code can become noisy if not kept focused.

### Agent Prompt

```text
Add tests and debug tooling for the active ragdoll balance system. Focus on deterministic logic such as step planning and balance state transitions, and improve debug visualization for COM, support area, and step targets. Avoid fragile full-physics tests. Keep code and comments in English.
```

## Package Dependencies

```text
1 -> 2 -> 3 -> 4 -> 5 -> 6 -> 7 -> 8 -> 10 -> 11
                  \         /
                   \-> 9 ->/

12 can start after 4 and expand after 5-7 stabilize.
```

## MVP Cut

The first real milestone should stop after Package 7.

The MVP definition is:
- the active ragdoll stands upright for several seconds,
- small disturbances are recovered without stepping,
- larger disturbances trigger at least one support step,
- the stepping foot lands in a visually grounded way.

Packages 8 through 12 improve realism, usability, and future extensibility.

## Suggested Agent Assignment

- Agent A: Packages 1-2
- Agent B: Package 3
- Agent C: Package 4
- Agent D: Packages 5-6
- Agent E: Package 7
- Agent F: Packages 9 and 12
- Agent G: Packages 8 and 10
- Agent H: Package 11

## Review Checklist For Every Package

- Does the scene still enter Play Mode cleanly?
- Are all new scripts in the expected architecture folders?
- Are inspector fields named clearly and documented by usage?
- Is the behavior observable without reading code?
- Is the new logic isolated enough for later tuning or replacement?
- Did the change avoid breaking the next package in sequence?

## Notes For Future Implementation

- Do not start with full walking cycles or gait generation.
- Do not let IK become the primary balance solver.
- Prefer a simple support-foot model before attempting exact support polygons.
- Keep debug visualization active through most of development.
- Treat root ownership and force application carefully to avoid controller conflicts.
