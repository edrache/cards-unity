# Pit trap verification — 2026-09-20

Unity 6000.3.10f1, open editor, `Assets/Scenes/ProceduralCave.unity`.

## Implementation

Both variants are enabled at 25% in the playable generation profile. With its current seed and dense existing content, placement produces one open pit in room 14 and one cracked pit in room 20; unavailable placements are skipped. Other profiles retain zero default percentages. Shared settings support 2–12 m independently sampled axes, uneven convex outlines, 3–30 m shafts and a discrete list of total collapse durations.

The cover is eight deterministic Voronoi fragments. Support is removed at 25% of total collapse duration; all fragments reach the bottom by its end. A fall of 0.8 m below the local floor is fatal regardless of health or knockdown immunity. Existing defeat presentation and timing are reused.

## Checks

- `Tools > Cards Unity > Run Pit Checks`: exact area after two holes crossing source triangle edges; no triangles in hole interiors; upward winding; preserved sloping-plane heights; snapshot deep-copy isolation; invalid and empty duration-list handling.
- Play Mode with the real player: collider raycast confirms intact cracked-floor support, direct `Tick` starts the warning/collapse, moving off the cover preserves health, collapse continues to completion, and no floor collider remains above the hole.
- Fresh Play Mode run: positioned the real player on cracked ground, then allowed normal frame updates, CharacterController gravity and trap updates to run. The cover collapsed, the player fell, all three full health segments were lost, locomotion stopped, and the delayed defeat summary became visible. The body reached approximately y=-6.58. This was not keyboard/controller input automation.
- Another fresh run: positioned the player above the open pit and allowed natural gravity. Confirmed death, zero health, stopped locomotion, visible defeat summary, and rejected repeated lethal damage. The body reached approximately y=-7.51.
- Temporary player-state object completed a run through `CaveExit.TryExit`; `CharacterHealth.TryKill` then returned false and preserved life. Temporary object removed.
- Camera inspections of both variants; cracked cover inspected again after replacing radial triangles with Voronoi fragments. Temporary inspection cameras/lights removed.
- Runtime background ticking was temporarily enabled to allow natural simulation while Unity lacked focus, then restored to false. Tests ended in Edit Mode; no test poses were saved.
- Rebuilt twice with identical settings: pit names, durations and every generated vertex matched. The live pit-mesh count remained constant across rebuilds. Final editor state: Edit Mode, clean scene, time scale 1, runtime background ticking false.
- Handbook regenerated from source; local navigation/file links and `git diff --check` checked.

## Limits

No physical keyboard/gamepad traversal was automated. Terrain edits apply to the procedural cave floor, not arbitrary terrain meshes. The rim is an irregular convex polygon, not a hand-authored concave spline. Seeded placement can skip crowded rooms; percentages are requests, not guaranteed counts.
