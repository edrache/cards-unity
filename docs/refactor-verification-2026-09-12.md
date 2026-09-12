# Designer configuration refactor — verification

Branch: `codex/designer-config-refactor`. Unity 6000.3.10f1, URP 17.3.0. No package changes. The primary scene remains `Assets/Scenes/ProceduralCave.unity`.

## Design and migration

The refactor separates shared authoring data from instance runtime state. Generation settings, player balance, torch balance, centipede balance and treasure definitions are authored as typed ScriptableObjects in `Assets/Settings/Gameplay/`. Original serialized component fields remain the fallback when a profile is absent. Rig bindings, local pose/appearance, physics layers and scene bindings remain local.

The generator's configuration capture and generic content stage are separate partial files. Generic room content runs after all built-in populations, uses stable per-rule seed salts and supplies an optional `ICaveSpawnParticipant` initialization callback. Existing centipedes implement that callback; new enemy or trap mechanics still need their own behavior component. This is an authoring extension point, not a replacement navigation or combat engine.

Before editing, the Git worktree was clean but the active Unity scene was dirty. The scene was saved through Unity to preserve its existing authoring changes. Unity also serialized previously unsaved/default prefab fields. Those changes are included. Migration captured actual component tuning, not C# defaults. Distinct profiles preserve the cave player's overrides and the movement playground's shorter torch range. Treasure variants each retain their own identity and value instead of inheriting the chalice definition. Unnecessary reserialization of untouched scaffold/playground scenes was discarded.

## Checks performed

| Check | Evidence / result |
| --- | --- |
| Runtime and editor compilation | Unity compiled both assemblies; final Console query contained no errors or exceptions. |
| Repeatable configuration checks | `Tools > Cards Unity > Run Configuration Checks`: 21 assertions passed in an isolated Edit Mode preview scene; temporary objects and profiles are destroyed in `finally`. |
| Health | Profile controls slot count and refill time; partial refill survives enable toggle; a hit clears partial refill and one full slot; legacy fields stay intact. |
| Grouped player profile | Incomplete capture fails without mutating the profile. Complete capture reads authored movement and assigns the same profile to all seven components. Missing editor action target is rejected. |
| Torch | Profile lifetime and strike cost affect remaining fuel; enabling/disabling preserves fuel; burning does not mutate the profile asset. |
| Treasure | Definition supplies display name/value/coin category independent of object name; runtime `SetValue` changes only the instance. |
| Cave settings | Invalid/inverted ranges are corrected; nested rule and prefab-array snapshots are independent copies. |
| Original cave generation | Seed 1, 16 rooms. All generated local transforms and all MeshFilter vertices/triangles matched before refactor, after refactor with local fallback, and after assigning the captured profile. 502 transforms. SHA-256: `3D4E76407FAA41D21C0389ED4A6E411B5B84198AFD94B4F68EEA34D00A867E29`. |
| Serialized authoring data | Compared 48 before/after component JSON snapshots: 828 original fields matched. EditorJsonUtility does not preserve scene object references in this export, so this check establishes scalar/asset-reference preservation, not scene-reference identity. Scene bindings were inspected separately. |
| Generic content rules | A temporary profile requested one extra centipede in every room; bounded placement produced 15. Rebuild repeated their transforms exactly. Adding a later coin rule did not shift them. Every spawned enemy had a player target and dormant cave home, and its center was inside a room. The shared source profile was unchanged; original profile was restored and test objects removed. |
| Play Mode integration | Migrated cave: 16 rooms, 42 active treasures, 3 full health slots, walk speed 2 and sprint speed 7. |
| Pickup transition | Direct `TryInteract`, `Tick` and `ApplyPose`: collected one coin; player position and facing remained unchanged. |
| Attack transition | Direct strike/Tick/pose: attack finished and consumed 5 seconds of fuel. |
| Knockdown transition | Direct Tick: stationary time did not recover; reporting movement completed recovery. |
| Full gait mixer | Set a distinct custom weight in all twelve styles, advanced the actual `Animate` transitions to Run and back. Run reached its target; all twelve authored weights returned with maximum error 0. Test ran in Play Mode and was not saved. |
| Camera/UI inspection | Game-view capture inspected with camera framed on the player. Original saved target reference was already valid; no camera fix was needed. UI text, health segments and pickup prompt were visible. |
| Handbook | Opened through a local loopback server, visually inspected desktop layout and expanded field tables; search filtered TorchFuelBalance correctly. Source links, embedded JSON and internal anchors were checked. |
| Cleanup | Restored Edit Mode, original active scene and original cave profile, saved the intended scene state; no temporary gameplay objects or profiles remain. |

## Practical limits

These are focused deterministic/direct-call checks, not physical keyboard/controller input tests, an exhaustive seed sweep, a player build, or performance certification. A native UI keyboard test could not be completed through the available computer-use surface. Existing light/shadow attack behavior, obstruction cancellation, complex boulder physics and every supported screen aspect were not exhaustively replayed.

Generic placement uses authored circular footprints and bounded attempts. Collision resolution is ordered: earlier generic rules can alter availability for later rules even though their random streams are separate. Designers must verify prefab footprints, collider clearance and route access. The count is a request, not a guaranteed spawn total. Centipede behavior still uses its existing local steering and per-instance behavior-seed policy.

The delivered menu checks do not add a Unity Test Runner assembly. Runtime inventory/fuel persistence across application sessions, a global navigation solution, arbitrary enemy AI, and a generic damage framework are outside this refactor. The [designer handbook](designer-guide/index.html) identifies the current extension contracts and remaining local tuning.
