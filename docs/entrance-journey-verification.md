# Entrance journey verification — 2026-09-20

Verified in the existing Unity 6000.3.10f1 editor using the selected relay. The user's unsaved scene was copied to a temporary backup before integration. The camera child had been renamed from Hide to CameraBlackout; that existing plane was retained. The disabled Blackout Image, edited narrative timing and inventory prefab changes were preserved.

## Generated route and movement

- The authored generation profile now uses Entrance Length 150 m. The sampled winding route from spawn to chamber centre is 161.6727 m, with 181 points. The entrance chamber is index 19 for this seed; its boundary is around route distance 150 m.
- A normal startup moved the character more than 23 m with the scripted locomotion active, time scale 1 and fuel remaining at 600 seconds.
- A bounded traversal check used the normal 1.15 m/s scripted speed, temporary time scale 4 and a runtime-cloned narrative with extended holds. The character traversed the automatic approach and stopped within 0.049 m of the configured 132 m limit, leaving 18 m before the chamber. It remained outside the room and protected, and fuel loss was exactly zero.
- CharacterController movement, actual-distance gait and the regular torch pose/flame updates remained active. No second Unity instance or persistent test assembly was added.

## Handoff and safety

- Protected health rejected both TryTakeDamage and TryKill. Direct torch Tick and strike-cost calls left remaining fuel unchanged.
- Advancing the sequence to its final beat synchronously released scripted movement while keeping journey protection active.
- Actual Game View captures were inspected at partial dissolve with the bold final text. The veil reached progress 1, its renderer was disabled and the original player occlusion outline state was restored. Fuel remained 600 seconds throughout this manual-control period.
- Direct position checks at route distances 148 m and 151 m verified protected manual approach versus room entry. Fuel then resumed at the requested Tick duration. Returning to the tunnel did not restore safety.
- A separate live check placed the protected player inside room 0 instead of the designated entrance room 19. Protection and fuel suspension ended there as well, preventing indefinite safety through an alternate branch.
- Completing narration and entering the room disabled the intro owner and restored normal fuel consumption. Temporary time scale and run-in-background changes were restored; Play Mode objects, narrative clones and test poses were not saved.

## Assets and limitations

- The URP dissolve shader compiled without shader errors. Source and handbook whitespace checks pass; handbook local links resolve and the generated configuration catalog was refreshed.
- Existing Unity/URP Volume inspector errors (`SerializedObjectNotCreatableException` in DepthOfFieldEditor and other Volume editors) were observed around domain reload. They are editor-inspector errors, not script/shader compilation or journey runtime failures; no vendor package changes were made.
- Checks used live gameplay, explicit method calls and temporary position changes. This is not a persistent automated suite or a physical gamepad test. Extreme aspect ratios and different generation seeds were not exhaustively tested.
