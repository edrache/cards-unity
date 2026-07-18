# Active Ragdoll Progress

## Status Legend

- `DONE` means the package has reached its current acceptance target for this phase.
- `IN PROGRESS` means implementation has started, but practical validation is still pending.
- `NOT STARTED` means no meaningful implementation work has been completed yet.

## Current Package Status

| Package | Status | Notes |
|---|---|---|
| 1. Rig Audit And Architecture | `DONE` | Audit completed and documented in `docs/active-ragdoll-rig-audit.md`. |
| 2. Physics Rig Builder | `IN PROGRESS` | Scene wiring, rig mapping, validation, adaptive collider placement, internal collision suppression, per-bone joint defaults, cleanup tooling, and segment gizmo support are in place. Practical physics validation in Play Mode is still pending. |
| 3. Active Ragdoll Core | `IN PROGRESS` | Initial joint drive plumbing, pose-source following, torque-based body correction, root upright stabilization, and root position force are in place, but standing stability is not validated yet. |
| 4. Balance Sensing | `IN PROGRESS` | COM tracking, basic balance states, and debug gizmos exist in scaffold form. |
| 5. Step Planning MVP | `IN PROGRESS` | Basic planner logic and tests exist, but no grounded gameplay validation yet. |
| 6. Step Execution MVP | `NOT STARTED` | Placeholder stage only. |
| 7. Foot IK And Grounding | `NOT STARTED` | Not implemented yet. |
| 8. Whole-Body Recovery | `NOT STARTED` | Not implemented yet. |
| 9. Balance Test Driver | `IN PROGRESS` | Push driver supports keyboard triggering, target-body selection, animator disabling, and debug logging, but practical workflow validation is still pending. |
| 10. Arms And Head Targets | `NOT STARTED` | Placeholder stage only. |
| 11. Tuning Pass | `NOT STARTED` | Too early for full tuning. |
| 12. Tests And Debug Tooling | `IN PROGRESS` | Deterministic EditMode tests and debug visuals have started. |

## Current Focus

The current focus is split between `Package 2: Physics Rig Builder` and `Package 3: Active Ragdoll Core`.

The next concrete goal is:

1. Generate the first physics rig from the `Player` root using `CharacterPhysicsRigBuilder`.
2. Verify that the generated rigidbodies, colliders, and joints produce a stable first-pass ragdoll.
3. Verify that the hidden animation-driven pose source is actually feeding a meaningful target pose into the physical rig.
4. Adjust collider sizing, joint limits, and active stabilization based on Play Mode results.

## Notes

- The project could not be batch-validated yet because another Unity instance already had the project open.
- Until the first Play Mode validation happens, Package 2 should be treated as prepared but not complete.
