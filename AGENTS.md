# Project guidance

## Language and collaboration

- Communicate with the user in Polish.
- Write code, documentation, comments, and commit messages in English.
- Inspect the working tree before editing. Preserve unrelated changes, including imported assets and package updates.
- Implement focused changes; do not introduce unrelated systems or refactor vendor code.
- After completing work, commit all outstanding repository changes, including pre-existing changes, imported assets, package updates, and new non-ignored files. The user has explicitly authorized this workflow; do not limit commits to files authored by the current agent. Review the full diff and use clear English commit messages. Respect explicit requests to leave changes uncommitted. Do not push without a request.

## Current project

Unity **6000.3.10f1**, using **URP 17.3.0**. Confirm versions in `ProjectSettings/ProjectVersion.txt` and `Packages/manifest.json` before changing dependencies.

The project contains a playable procedural 3D character prototype, not a bare scaffold. The character is built from primitive meshes with articulated arms and legs. Animation is generated in code, with two-bone leg IK and a blendable gait system.

Scenes:

- `Assets/Scenes/ProceduralMovement.unity`: daytime movement and animation playground.
- `Assets/Scenes/TorchNight.unity`: dark obstacle course with a handheld torch, point light, fire, and smoke.
- `Assets/Scenes/SampleScene.unity`: original scaffold scene.

## Runtime code

Project code currently lives in `Assets/Scripts/Controllers/`, under the `CardsUnity.Controllers` namespace:

| File | Responsibility |
| --- | --- |
| `ProceduralCharacter.cs` | Rewired input (Input System fallback in scenes without a manager), acceleration, braking, gravity, CharacterController movement, turning, and sprint requests |
| `CharacterFollowCamera.cs` | Smoothed orthographic camera following from above |
| `CartoonCharacterGait.cs` | Rig references, IK, body motion, independent arm/forearm controls, noise, style weights, and sprint style transitions |
| `GaitStylePose.cs` | Procedural style evaluation and blending, including foot adjustments |
| `HandheldTorch.cs` | LateUpdate holding pose, torch inertia, smooth light flicker, illumination controls, particle drift, pose suppression during an attack, and the fuel a strike costs |
| `TorchAttack.cs` | Procedural overhead torch swing on the right arm: hold to cock, release to strike, pose blending over the holding pose, optional chest twist, and a strike event |
| `ShadowKnightProportions.cs` | Editable limb/body dimensions, rig and collider sizing, torch grip, and optional helmet |
| `ProceduralSpine.cs` | Three-joint spine, neck stabilization, and per-instance CPU torso deformation driven by gait styles |
| `FootstepAudio.cs` | One-shot footstep playback on gait foot contacts, with per-step pitch randomization and Sneak/Walk/Run volume |
| `MusicPlayer.cs` | Looping background music with fade-in/fade-out and an optional mixer group |
| `CentipedeLegAudio.cs` | Pooled leg-click voices for a centipede, limited to a chosen number of legs, gated on the distance to the player and randomized per contact |

There are currently no project-owned `.asmdef` files or automated test suites under `Assets/Scripts/`. Do not assume `CardsUnity.Runtime` or `CardsUnity.Tests` assemblies exist. Add folders and assemblies only when needed; keep data-only calculations independent of scene objects where practical.

## Controls and animation behavior

- WASD/arrows or the gamepad left stick move relative to the camera.
- TorchNight uses Rewired Player0: MoveHorizontal/MoveVertical (WASD or left stick), held Run (left Shift or the mapped pad button), toggled Sneak (C), and held Attack (Space), which strikes on release. Run is read from every controller type and overrides Sneak and Walk; releasing it returns to whatever the stick or the Sneak toggle asks for. Sneak stays restricted to the keyboard input source, because toggling it also hands control back from the stick.
- The gamepad left stick chooses between Sneak and Walk by deflection: below Walk Threshold it sneaks, above it walks. Running is never derived from deflection. `ProceduralCharacter` exposes Idle Threshold and Walk Threshold, kept ordered by `OnValidate`. Movement speed scales with deflection because the input vector keeps its magnitude.
- Below Idle Threshold the stick releases the automatic style and the manual style mixer takes over again, so a connected but centred gamepad never overwrites the Inspector sliders.
- The daytime scene has no Rewired manager and falls back to Input System: keyboard movement keeps priority, the left stick takes over once deflected past Idle Threshold, Run is left Shift or left stick click, Sneak toggles on C, and Attack is Space.
- Sprinting smoothly changes the visible style sliders to Run and fades the other weights. Releasing sprint restores the previous custom mix, including after rapid toggling.
- Gait phase advances from actual horizontal distance travelled. Avoid animating a full walk when blocked by a wall.
- Styles: Walk, Double Bounce Walk, Strut, Shuffle, Sneak, Run, Jump, Fast Run, Tip Toe, Skip, Knee Walk, and Crawl.
- Style weights are normalized and transitions are smoothed. All-zero weights fall back to Walk.
- Jump and hop styles are visual animations; they do not jump the CharacterController.
- `CartoonCharacterGait` raises a `Footstep` event once per foot contact, derived from the travelled distance rather than from time. Even half-cycle indices are the left foot, odd ones the right; standing still produces no steps.
- `FootstepAudio` sits next to the gait, plays `Assets/Audio/SFX/step_left.wav` and `step_right.wav` through two runtime `AudioSource` children ("Footstep Left"/"Footstep Right") moved to the contacting foot, randomizes pitch per step, and blends volume and base pitch between Sneak, Walk and Run using the blended style weights (Sneak + Tip Toe, Run + Fast Run).
- Head orientation compensates for torso rotation and looks ahead.
- Arm elevation controls the upper arm. Forearm controls set elbow bend relative to that arm, with separate speed and swing contributions.
- Preserve existing Inspector tuning and serialized references. Use serialization migration attributes when renaming fields.
- `Animate` accepts an optional delta time for deterministic pose checks. `HandheldTorch.Tick` similarly accepts explicit time values.

## Shadow Knight

- `TorchNight` uses `Assets/Prefabs/Shadow Knight.prefab`; the original `Procedural Character.prefab` remains available.
- Change dimensions on the root `ShadowKnightProportions` component. Its custom Inspector records Undo and prefab overrides. Edit outside Play Mode to retain changes.
- `Shadow Knight Helmet.prefab` is a nested accessory under `Body Pivot/Head/Helmet`; `Helmet Visible` toggles it. Meshes and materials are in `Assets/Prefabs/ShadowKnight/`.
- The knight has `Spine Lower -> Spine Middle -> Spine Chest -> Neck` under `Body Pivot`. Arms attach to the chest; head attaches to the neck. The 120-vertex torso uses blended procedural deformation in Play Mode, with an immutable rest mesh asset and a disposable per-instance mesh. This is a procedural rig, not an imported Humanoid Avatar.
- `ProceduralSpine` exposes bend, twist, side bend, response time and head stabilization. All ten gait styles blend spine poses using the same weights and distance-driven phase; stopping fades to neutral.
- Keep rig part names stable: the proportions component resolves and caches the named transforms. Limb dimensions also update gait IK and the torch grip offset.

## Audio

- Audio assets live in `Assets/Audio/`: `SFX/` (footstep and leg-click samples), `Music/`, and `Mixers/Deep.mixer` with a single `Master` group. Footsteps, leg clicks and music all route to that group.
- `TorchNight` has a `Music` object with `MusicPlayer`, playing `Assets/Audio/Music/Stereo_Color_Dark-Background_main.wav` looped and faded in. Music tracks are imported as Streaming/Vorbis; short samples stay Decompress On Load.
- Footstep volumes, pitches and the mixer group are tuned on the `Shadow Knight` prefab asset itself. Preserve that tuning.
- `CentipedeLegAudio` on `Centipede.prefab` plays `Assets/Audio/SFX/ClickLeg.wav` for every leg contact through a pool of runtime `AudioSource` children ("Leg Click 0..n"), each with its own randomized pitch and volume. A voice is recycled on reuse, because the sample carries over a second of trailing silence after its transient.
- Leg clicks are gated on the distance between the creature and the player: full volume within `Full Volume Distance`, a squared falloff up to `Hearing Distance`, and no voices at all beyond it, so a populated cave neither floods the mix nor spends voices out of earshot. The audible falloff is that range check, not the 3D curve, because the listener rides the overhead camera.
- `Maximum Audible Legs` caps how many of the two legs per segment may click at all. The audible legs are picked evenly along the body, so lowering it thins the rattle proportionally without breaking the travelling wave; a value at or above the leg count makes every leg audible. The default 8 gives roughly 17 clicks per second while stalking and 65 while fleeing, against 57 and 213 with every leg of a 14-segment creature.
- `Maximum Steps Per Frame` is a separate guard that only drops the surplus of an unusually long frame.

## Torch and rendering

- Torch settings are on `Handheld Torch` in the character's right forearm hierarchy.
- `Assets/Prefabs/Handheld Torch.prefab` is nested in Shadow Knight, with holder references overridden on the knight. The standalone torch also burns without a character reference. `Lifetime` defaults to 180 gameplay seconds; `Burnout Fraction` (0.4) controls the final fading phase and `Dying Flicker Amount` controls stronger end-of-life flicker. Light, fire and smoke emission fade to zero; existing particles finish naturally. Disabling/re-enabling does not refill fuel. `RemainingLifetime` and `IsBurnedOut` expose runtime state. Direct Play Mode checks covered standalone burnout, holder references and no reignition on re-enable; no persistent test suite was added.
- `Brightness` and `Light Range` control illumination; flicker and sway have separate settings.
- Torch pose runs after gait updates. Preserve this ordering so the holding pose does not fight the walking animation.
- `SetPoseSuppression` fades out both the holding pose and the upright torch lock, so another animation can take the right arm and carry the torch with the forearm. Anything using it must push a value every frame; the torch keeps the last one.
- Fire and smoke simulate in world space; trails should remain behind a moving torch.
- URP assets are in `Assets/Settings/`. Check the active quality/pipeline asset before diagnosing lights or shadows.
- Use URP-compatible shaders and volume effects. Preserve the daytime scene when changing the night scene.

## Unity MCP workflow

Prefer the user-selected **unity-mcp relay** for editor operations. Its executable is `~/.unity/relay/relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64`, launched with `--mcp`. Expand the home directory in client configurations that require an absolute executable path.

The relay exposes tools such as `Unity_RunCommand`, `Unity_GetConsoleLogs`, and `Unity_Camera_Capture`. Inspect the tools available in the current session; do not assume schemas from the older Coplay server apply. The repository also includes `com.coplaydev.unity-mcp`, but it is a separate integration.

- Inspect the active scene, dirty state, and Play Mode before changing editor objects. Preserve unsaved work before switching scenes.
- For `Unity_RunCommand`, follow its current schema and use `internal class CommandScript : IRunCommand` with `Execute(ExecutionResult result)`.
- Register creations, modifications, and deletions using the tool's tracking helpers.
- Camera capture expects a **GameObject instance ID**, not the Camera component ID. Re-query IDs after scene changes or reloads.
- Script compilation can temporarily interrupt relay discovery. Wait for reload and retry a read-only check before repeating a mutation.
- Check whether a partially completed operation already created objects/assets before retrying it.
- Save intended scene edits in Edit Mode. Do not save temporary verification poses or cloned preview characters.

## Verification

After script edits, let Unity compile and inspect console errors. For movement or visual changes, verify in Play Mode and inspect a camera capture when relevant. Test changed behavior, including stop/start, sprint transitions, custom mix restoration, and meaningful parameter boundaries. Distinguish direct pose tests from actual keyboard input tests.

Return to the previous editor mode after testing and remove temporary test objects. Report what was verified and any remaining limitations; do not claim an automated suite passed when none exists.

If project tests are added, use Unity Test Runner in the open editor or the following batch commands when the project is not already open in another Unity process:

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults/EditMode.xml -logFile TestResults/EditMode.log
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults TestResults/PlayMode.xml -logFile TestResults/PlayMode.log
```

Inspect the resulting XML and log; process exit alone does not establish test success. `git diff --check` is useful for authored text; Unity-generated `.meta` files may contain trailing spaces in empty fields.

## Asset and dependency hygiene

- Let Unity generate `.meta` files and preserve their GUIDs. Commit each new asset with its metadata and required folder metadata.
- Do not manually regenerate serialized scenes or vendor assets when editor operations can preserve references.
- Respect `.gitignore`; do not force-add ignored build outputs such as `Library/` or `Temp/`. Include all non-ignored outstanding changes in the commit, as requested by the user.
- Existing asset packages include DOTween, Rewired, Feel/MMFeedbacks, Quibli, TrueShadow, and FMOD. Avoid broad changes to these packages.
- Installed Unity packages include Input System, Cinemachine, Animation Rigging, AI Navigation, Timeline, Recorder, Test Framework, and Unity AI Assistant. An installed package is not necessarily used by the character implementation.

## Maintaining agent instructions

Keep this file aligned with the actual repository. `CLAUDE.md` points to this shared guidance; update shared rules here instead of maintaining divergent copies.

## Torch attack

- `Attack` is Rewired action id 4 in `Assets/Prefabs/Rewired Input Manager.prefab`, a button in the Default category, bound to Space on the keyboard. No gamepad binding is authored; add one in the Rewired inspector, because joystick element ids depend on the hardware GUID.
- `ProceduralCharacter` reads the held button state on both input paths and feeds `TorchAttack.SetAttackHeld` every frame. `TorchAttack` detects the edges: pressing cocks the arm, releasing strikes. `TryStrike()` remains for callers and tests that want a whole swing without holding.
- `TorchAttack` sits on the `Shadow Knight` root with `[DefaultExecutionOrder(150)]`, so its `LateUpdate` runs after `HandheldTorch` (100) and wins over the holding pose. It drives `SetPoseSuppression` from `Update`, so the torch reads the weight in the same frame.
- Phases are Charging, Striking and Recovering. Charging ramps the pose weight in over `Windup Duration` and then holds the cocked pose indefinitely, so the arm stays raised while the button is down. A release is deferred until the windup has finished, so even a one-frame tap swings from the top. Strike (0.10 s) uses an ease-out curve; Recover (0.30 s) only fades the weight out, and that fade is what hands the arm back to the torch. There is no separate return pose, and presses during a swing are ignored rather than buffered.
- Angles are tuned for this rig: `Windup Pitch` -170 puts the hand above and behind the head, `Strike Pitch` -40 puts it low and in front. Increasing pitch rotates the arm backwards, so a positive windup would swing the wrong way. Re-measure both if limb proportions change substantially.
- Movement, turning and the gait keep running during a swing; only the right arm, the torch and optionally the chest are overridden.
- The optional `Chest` overlay is multiplied onto whatever `ProceduralSpine` wrote. `TorchAttack` remembers its own last write and undoes it first, because otherwise the twist accumulates whenever nothing else rewrites that joint, dragging the shoulder forward.
- `Strike`, `HitWindowOpen`, `IsAttacking`, `IsCharging`, `ChargeTime` and `SwingProgress` exist for future hit detection and feedback. Nothing detects hits or applies damage yet.
- Every strike calls `HandheldTorch.ConsumeStrikeFuel`, which ages the torch by `Strike Lifetime Cost` seconds, exactly as burning for that long would. The field lives on `Assets/Prefabs/Handheld Torch.prefab` (default 5 s of a 180 s lifetime); zero makes hitting free. Charging costs nothing beyond normal burning, and a strike can push the torch into its burnout phase or put it out.
- Direct Edit Mode checks stepping `Tick`/`ApplyPose` against `HandheldTorch.Tick`: the pose returns exactly to the holding pose (hand 0.80 m forward, 1.56 m up) after three consecutive swings with 0.000 degrees of chest drift; the hand reaches 2.86 m up and 0.22 m behind at the apex and 1.16 m up, 0.83 m forward at the lowest strike frame, against a 1.98 m shoulder; the hit window lasts six frames at 60 Hz; a second `TryStrike` during a swing is rejected. Torch tilt against character up peaks near 70 degrees, so the torch follows the forearm.
- Hold checks: holding for 1.5 s keeps the hand parked at 2.48 m up, 0.52 m behind with weight one and no strike event, and costs only the 1.5 s of ordinary burning; the release fires exactly one strike, spends `Strike Lifetime Cost` on top of elapsed burning, and returns the hand to the rest pose; a one-frame tap still reaches a 2.81 m apex.
- These are direct method checks, not an automated suite, and physical Space presses were not tested, because the relay assembly cannot reference Rewired.

## Procedural centipede

- Light occlusion ignores colliders only within the emitting HandheldTorch (or the Light subtree for other lights), not ancestors such as the holder. CharacterController capsules can therefore shield nearby stalkers. Direct SampleLight check with a nested point light and a character capsule: exposure 0 behind the holder, 4.495966 after a 180-degree holder turn. This verifies collider-based shadow detection, not the rendered mesh silhouette or a complete pursuit sequence.

- Shadow pursuit now searches outward from the target for the nearest safe radial distance and blends approach with a persistent per-instance orbit direction. Only nearby lights bound that search. Head exposure controls flight and its exit so an illuminated trailing tail cannot keep the head fleeing indefinitely; all segments still activate dormant residents. Direct Play Mode checks with a continuously lit point light (range 6, intensity 6): approach from 12 m to 6.20 m, orbit through 210.5 degrees with radius 6.09–6.28 m, then settle at 6.21 m after the target/light moved 2 m. These isolated flat-surface Tick checks do not establish routing through cave obstacles.

- After escaping light, the Hiding cooldown immediately tracks the player like Stalking. Both states score candidate light exposure relative to the safe faint-light threshold and reject steps into brighter unsafe light, favouring close pursuit along shadow edges. Recovery and cave surface restrictions still apply. This remains local steering; direct Play Mode Tick checks confirmed that Hiding immediately reduced target distance from 12.00 to 10.68 m in one second after the light was disabled. A further 30 seconds travelled 29.99 m with no exterior samples and a longest stationary interval of 0.42 s. A live moving torch shadow-edge test remains unverified. The artificial disabled-probe recovery check did not report recovery within its one-second window; movement resumed after restoring probes.

- Cave residents start Dormant and become alerted when the player enters their assigned chamber or faint local light reaches any segment. Alert persists after leaving that chamber. Generated residents receive their exact room; manually placed cave centipedes resolve the nearest room. Standalone centipedes outside cave scenes retain immediate stalking.
- Cave traversal is restricted to carved interior floors, rocks and inward-facing wall surfaces. Cutaway rims, outer faces and ceilings are rejected. Stalking prefers floor/rocks and cannot start climbing a boundary wall; flight may use inner walls, and safe creatures descend again. Candidate lookahead and floor preference reduce wall stalls. Visible wall raycasts are front-face only.
- Direct Play Mode checks covered dormant immobility, activation on entering the room, 600 stalking frames without wall climbing/outside contacts, and 600 light-response frames without outside contacts followed by return to stalking on the floor. The final dedicated wall-descent/rim test could not be completed because the Unity relay stopped responding; do not treat it as verified.

- `TorchNight` contains three `Assets/Prefabs/Centipede.prefab` instances. `ProceduralCentipede` generates a disposable preview/runtime rig using shared URP materials. Generated children are not saved; edit the root component.
- `Segment Count` (3–48) and `Size` (0.25–3) rebuild the creature independently per instance, including in Edit Mode. Duplicate the prefab to add differently sized creatures; leave root Transform scale at one. An unassigned target resolves the scene's `ProceduralCharacter`.
- `ProceduralCentipede` raises a `LegStep` event for every individual leg contact. A leg stays planted while `sin(cycle)` is negative, so its touchdown is the crossing of `cycle = pi`; all legs share the distance-driven phase and differ by a fixed offset, so the detection needs no per-leg state and a standing creature stays silent.
- Distance travelled drives the travelling leg wave. Surface-relative probes support floors, walls, ceilings and concave/convex junctions. The body follows recorded head contact poses at fixed path distances, including orientation around corners. This remains local steering, not global pathfinding; gaps without an adjoining surface stop movement.
- States are Stalking, Fleeing and Hiding. Point/spot light exposure across all segments triggers flight; shadow-casting lights respect collider occlusion. Directional moonlight is ignored. Exposure is a gameplay approximation, not a sample of rendered pixels or baked lighting. `Fear Threshold`, `Safe Light Ratio`, and `Hide Duration` tune the reaction.
- `Tick(float dt)` supports direct deterministic checks. Earlier editor Play Mode checks (before continuous movement was added): dark pursuit stopped near 1.2 m, restored torch triggers flight at 4.5 m/s, darkness triggers hiding and pursuit resumes; collider occlusion blocks exposure; 3/48 segments and 0.25/3 size generate successfully. These checks are not a persistent automated test suite.

- `Natural variation` controls independent Perlin steering noise, speed/tempo differences, varied hiding duration and leg phase offsets. `Behaviour Variation = 0` restores uniform behaviour; `Random Seed = 0` chooses an instance-specific runtime seed, while equal nonzero seeds give repeatable behaviour for equal inputs. Randomness uses a private generator and a clock advanced by `Tick`, without modifying Unity's global Random state. Flight limits steering/speed variation. Alerted creatures keep moving during the dark cooldown and circle nearby targets instead of stopping.

- Centipede surface traversal uses colliders on `Environment Mask`, ignoring triggers and character controllers. Align root local up away from the starting surface when placing directly on a wall/ceiling. `CrawlSurfaceMesh` accelerates read-only ray queries against visible cave wall meshes when these differ from the player's taller collision walls; no player colliders or layers are changed. Cached queries rebuild when cave meshes regenerate.
- Surface checks: direct Play Mode traversal covered floor-to-wall-to-ceiling, convex descent and the rendered cutaway cave wall/lip. Direct boundary checks covered size 0.25/3 with 3/48 segments; wall-light checks covered flight into darkness. These are editor checks, not a persistent test suite.

- Movement recovery checks net head progress every `Stuck Check Interval` (default 0.7 s), commits to an alternate heading for `Recovery Duration` (1.5 s), and uses the recent surface trail to backtrack after repeated stalls. All recovery steps still obey cave interior and surface restrictions.
- `Dim Light Threshold` (default 0.005, capped by Fear Threshold) activates and frightens creatures in faint outer light. This is a gameplay exposure threshold; rendered dither tones also depend on materials and post-processing. Direct Play Mode checks: activation at exposure 0.00882, 34.09 m travelled over 30 simulated seconds with a longest stationary interval of 0.40 s and no exterior samples; disabling surface probes triggered recovery, and restoring them allowed movement. These are direct Tick checks, not an automated suite.

## Procedural cave

- `Centipede Room Percentage` (0–100, default 25) spawns one assigned `Centipede Prefab` per selected room. Count rounds to the nearest integer with half rounded up; zero disables spawning, 100 populates every room. A separate seeded shuffle keeps selection repeatable without changing layout or rocks. Creatures are disposable children of the generated cave, so rebuilding replaces them; the entrance tunnel is not counted as a room. The saved scene assigns `Centipede.prefab`. Play Mode checks at 18 rooms yielded 0/5/9/18 creatures for 0/25/50/100%, with matching room selection after regeneration.

- `Assets/Scenes/ProceduralCave.unity` is a separate playable cave scene with the Shadow Knight prefab and an overhead follow camera; it uses the existing pipeline's dither effect and the shared Rewired Input Manager prefab. TorchNight also uses that manager prefab.
- Select `Procedural Cave` and edit `ProceduralCave`: Room Count (1–24), Seed, Room Radius, Room Size Variation, Extra Connections, Corridor Width, Corridor Winding, Branch Density, Entrance Length, Irregularity and Wall Height. Changes rebuild the preview; `Rebuild Cave` also regenerates it. Generation uses Unity meshes rather than ProBuilder objects.
- Chambers use sunflower packing with stratified, shuffled radii, avoiding grid rows. A distance-based spanning tree connects chambers; Extra Connections opens nearby spare links to create loops. Zero adds no explicit loop links; positive values add at least one when a spare edge exists. Winding passages and side branches can also meet geometrically. Room Size Variation zero gives equal base radii, while one varies them from roughly 55% to 145%, with a 2.5 m minimum radius. The triangulated floor is level when Enable Elevation is off, rock walls have mesh colliders, and the roof is cut away for visibility. Keep the generator transform at unit scale. Rebuilding moves the assigned player into the entrance tunnel and faces them inward. Player can be assigned explicitly; an empty reference resolves a ProceduralCharacter in the same scene.
- Cave lighting comes only from the player torch: no fill light, skybox, ambient light or environment reflections.
- Generated objects/meshes are disposable and not saved; the scene stores generator settings and material/player references. Generation runs again in Play Mode. Materials live in `Assets/Materials/Cave/`.
- Direct Play Mode checks covered 1/6/24 rooms, minimum corridor width with maximum irregularity, 4,260 controller traversal/floor samples without failures, and wall blocking. These are editor checks, not a persistent automated suite or physical keyboard input tests.

- Maze verification: 1/2/10/24 chambers at zero/maximum extra connections passed reachability checks. Direct Play Mode controller traversal covered 9,488 corridor samples with minimum width and maximum irregularity/size variation without collision or floor failures. Corridor endpoints are pinned exactly to their chamber centers.

- Corridor Winding controls multi-bend tunnels. Branch Density adds side tunnels rooted at sampled midpoints of main passages. The final corridor is the southern entrance, and SpawnPosition is inside it. Passage bounds accelerate field sampling. Decorative rock lips use a separate vertical boundary collision mesh so nearby paths stay traversable.
- Updated winding-layout checks: direct Play Mode traversal of all tunnels for 1/6/24 rooms at minimum width and maximum winding/branches/irregularity passed without blocked movement or missing floor; spawn matched exactly. All 20 rooms in the saved layout were reachable from the entrance. Temporary inspection lighting was removed.

- Enable Elevation adds a continuous, slope-limited height field shared by every room and corridor, so junctions remain seamless and floors never stack. Elevation Range controls the total height envelope; Maximum Slope controls terrain steepness (5–20 degrees). Cutaway Wall Height caps visible walls in this mode while full-height boundary collision remains. The overhead camera follows player Y; spawn includes floor elevation.
- Elevation verification: 23,288 direct controller samples uphill/downhill at 1/12 m range and the maximum slope setting passed; disabling elevation produced zero floor height. The analytic height field bounds slope conservatively; tiny clipped boundary triangles can have noisy numerical normals. Lighting remains torch-only.



## Stepping onto ledges

- `ShadowKnightProportions.ApplyProportions` sets CharacterController stepOffset from Calf Length. `ProceduralCharacter.Move` checks steep risers against the supporting floor to prevent the rounded capsule from climbing above that limit; Update uses this same movement path.
- Gait IK probes walkable collider surfaces under each foot and along its recovery path, adding terrain height to the foot target. Probes ignore triggers and the character hierarchy; trailing feet can sample the lower tread. Obstacles need colliders.
- Direct Play Mode checks at the saved calf length (0.524 m) passed traversal for 0.2/0.524 m blocks and blocked 0.574/1.5 m blocks. Foot lift and a camera pose were inspected. These are direct controller checks, not keyboard input tests or a persistent automated suite.

## Scattered cave rocks

- Floor Irregularity has been removed. Four reusable low, broad rock prefabs with convex mesh colliders live in `Assets/Prefabs/CaveRocks/`. The cave uses Rock Density (0–1), Minimum Rock Size and Maximum Rock Size (1–4 scale for roughly unit-width meshes). Defaults are 0.45 density and 1.2–2.6 size.
- Rocks are deterministically regenerated under the disposable cave root, align to floor slope, avoid other rocks and cave edges, and reserve a 1.8 m-wide route along corridor centerlines plus 3 m around spawn. Actual count depends on available space; large rocks reduce capacity. Density zero generates none.
- Direct Play Mode checks: density 0/0.1/0.45/1 produced 0/13/45/69 rocks in the tested scene. Maximum 4-unit size preserved traversal across 4,732 samples using ProceduralCharacter.Move. Preview camera and inspection light were removed after visual verification.

## Dropping and picking up the torch

- `TorchInteraction` on `Shadow Knight` provides the contextual `TorchInteract` Rewired action (id 5, keyboard E) and a small Polish "Upuść" / "Podnieś" prompt. The Input System fallback also uses E. Gamepad mapping can be assigned in the Rewired inspector. With empty hands, the character selects the nearest unheld, active torch within Pickup Range and clear reach, including corpse torches. Drop the held torch before taking another; there is no inventory.
- Dropping lowers and extends the right arm before release. Picking up turns toward the torch, crouches using the gait's existing leg IK, and reaches for the grip using arm IK. A short recovery blends back to the holding pose. Movement pauses during the interaction; attacks and repeated interaction presses are rejected until it finishes. Attacks require a held torch.
- `Pickup Range` (default 1 m), `Drop Duration`, `Pickup Duration`, `Recovery Duration` and `Show Prompt` live on `TorchInteraction`. Pickup accepts moving and rolling torches; it requires a clear line to the grip and a reachable hand target at the moment of attachment. Facing and arm IK track the moving grip throughout the reach. Burned-out torches can still be picked up.
- `HandheldTorch.Drop` detaches the same object and adds a runtime capsule collider and Rigidbody. It ignores the former holder's colliders. Pickup disables that collider and makes the body kinematic. Fuel is never refilled by these transitions.
- `Drop Lifetime Cost` on `Handheld Torch.prefab` defaults to 10 seconds. Only the first collision after each drop consumes it; subsequent bounces do not. Zero disables the penalty. The detached torch continues its regular light, fire, smoke and burnout updates, using its own movement for particle drift.
- Direct Play Mode checks covered two drop/pickup cycles, a 10-second impact penalty and zero-cost boundary, continued ground burning, complete burnout with zero light/emission, no refill on re-enable, pickup of an extinguished torch at 0.95 m, distance/wall rejection, and attack blocking/restoration. A camera capture confirmed the crouched reach; grip error at 0.65 m was below 0.2 mm. These are direct Tick/physics checks, not a persistent automated suite or physical keyboard input tests. All temporary Play Mode objects were discarded and the editor returned to Edit Mode.

- Moving-pickup follow-up: direct Play Mode physics checks on an 8-degree slope accepted a torch moving at 0.35 m/s and attached it while still moving at 0.67 m/s. Facing and reach tracked the rolling grip; a torch that rolled beyond pickup range was rejected. A camera capture confirmed the grip pose. The test used a temporary character and a separate physics scene, which were removed afterward; no physical E-key test was performed.

## Exhausted locomotion styles

- `CartoonCharacterGait` adds `Knee Walk` and `Crawl` to the normalized style mixer (indices 10 and 11). Set the desired slider to one and the other eleven to zero for a pure style. Both default to zero, preserving existing tuning. Automatic Run/Sneak/Walk overrides and restoration include all twelve weights.
- Low pelvis height and torso pitch persist at rest; distance still advances the alternating knee/shin and crawling arm motion. Knees sample walkable ground, shins trail behind, and blended leg chains retain their lengths. Ordinary footstep sounds are suppressed when low styles dominate.
- The existing late torch holding and pickup overlays retain the right arm. Pickup adds only the remaining crouch and lean for knee walking, while crawling stays low. A crawling drop reaches forward to release above the floor. Fuel and interaction rules are unchanged.
- These remain selectable visual styles without an exhaustion meter or shortened CharacterController. Combat knockdown now layers the Knee Walk posture separately over the locomotion mixer; see below.
- Verification: direct Play Mode checks covered both pure styles, stationary posture, rapid sprint toggles, exact restoration of a 0.4/0.6 knee/crawl mix, all-zero Walk fallback, suppressed low-pose footsteps, and drop/physics-settle/pickup at 0.65 m. On the saved rig, knee centers stayed at or above 0.12 m over 240 moving samples per style; leg-length error stayed below 0.02 mm. Camera captures were inspected and the console contained no errors. These are direct method/physics checks, not physical keyboard input tests or a persistent automated suite. Temporary objects were discarded on returning to Edit Mode. Complex cave rocks/slopes and extreme rig proportions have not been exhaustively checked for these new styles.


## Centipede attack and knee recovery

- `ProceduralCentipede.Attack.cs` extends the existing component. An alerted creature accumulates head-light exposure above its existing reaction threshold; darkness drains that timer at one second per second. `Light Patience` (3 s) provokes Enraged pursuit, which approaches the player despite light. Existing dormant activation and shadow pursuit remain in use before provocation.
- Within `Attack Range` (3.5 m), on a floor-like surface and with a clear approach, the creature raises its front chain for `Attack Windup` (0.65 s), then commits to a straight `Leap Duration` (0.45 s) lunge. The rendered body rises on a `Leap Height` (0.8 m) arc while contact samples remain on supported cave terrain. It cannot jump gaps. Surface checks and collider sphere casts stop blocked lunges; the head checks contact against the target CharacterController. A moving target can evade after takeoff. `Attack Cooldown` is 4 s.
- `CharacterKnockdown` is attached to `Shadow Knight.prefab`. A hit cancels torch attack/interaction and immediately drops a held torch using the existing physical drop and fuel rules. The knee overlay leaves the saved locomotion mix intact.
- The character can move at 40% speed while down. Falling takes 0.25 s, followed by 1.4 s of actual movement on the knees and 0.65 s of movement to stand. No displacement means no recovery progress, including when blocked by a wall. Stopping during recovery holds the current pose. Sprint, torch attacks and interaction are unavailable until standing; dropped torches can then be picked up normally. Repeat hits are ignored while down and for 1.5 s afterward.
- Direct Play Mode method checks on an isolated flat floor verified light-provoked windup/lunge/contact, physical torch detachment, persistent kneeling after 20 s idle, movement-only recovery and pause/resume, post-recovery immunity, preservation of a 0.4 Walk / 0.6 Strut mix, a dodged lunge, and a wall introduced during windup stopping the lunge without a hit. Windup and knee camera captures were inspected. These are direct Tick/physics checks, not physical keyboard tests or an automated suite. Complex cave routing and extreme creature sizes remain unverified for attacks. Temporary objects were discarded by returning to Edit Mode.

- Darkness also provokes alerted centipedes: `Darkness Attack Threshold` (0.02) compares the brightest of four samples 0.8 m around the target, at 0.8 m height. Target colliders are ignored only for these samples, so the player's own capsule cannot falsely indicate darkness; ordinary creature exposure retains holder occlusion. `Darkness Patience` (0.75 s) filters brief flickers. This trigger shares the existing range, windup, lunge, cooldown and knockdown protection. Dormant residents still require their usual activation. Once provoked, the creature commits to pursuit like the prolonged-light trigger.
- Direct Play Mode darkness checks with the prolonged-light trigger delayed for isolation: exposure 0.10126 did not attack; 0.00506 and zero exposure attacked and hit; 0.3 s darkness followed by restored light did not attack. Tests used a shadow-casting point light and the actual knight capsule. Temporary objects were discarded on returning to Edit Mode; no physical keyboard test was performed.

## Character stress

- `CharacterStress` on `Shadow Knight.prefab` exposes Stress (0–100), Encounter Stress (8), Hit Stress (25), Encounter Range (7 m), Encounter Reset Time (10 s), optional Recovery Per Second (0 by default), Recovery Delay (10 s), Hunch Angle (24 degrees), Head Look Angle (60 degrees) and Motion Chaos (1).
- Each centipede reports proximity through `Observe` from its Tick, including dormant creatures. A clear collider ray to its head within range counts as an encounter; walls block it and the character/creature hierarchies are ignored. This is omnidirectional proximity awareness, independent of rendered illumination. Continuous presence refreshes the last-seen time without adding stress; ten seconds unseen permits another encounter. Successful lunge knockdown adds hit stress only when TryKnockDown accepts the hit, respecting existing immunity.
- Stress smoothly blends procedural body/arm noise, distributed spine hunch and irregular neck scanning, including while idle. It does not edit locomotion style weights. Gait and spine retain their existing order before torch/attack overlays. Default stress does not decay; optional recovery starts after threats stop.
- Direct Play Mode checks verified 8 points for first contact, no repeated increment, wall occlusion, 16 after a renewed encounter, 41 after an additional RegisterHit, clamps at 0/100 and zero visual weight after settling at zero. A maximum-stress pose capture was inspected. An initial reencounter assertion failed because deferred destruction left the test wall active while paused; disabling that obstacle and syncing physics passed the recheck. Unity compiled and the final console check reported no errors. Tests used direct methods, not a full lunge-to-stress combat sequence or physical controls; sprint/interaction regression scenarios were not rerun. All temporary objects were discarded by returning to Edit Mode.

## Fallen cave bodies

- `Assets/Prefabs/Fallen Knight.prefab` contains a static body derived from Shadow Knight and a separate nested Handheld Torch. Player, gait, combat, stress, input and collider components are removed; disabled proportions/spine components retain the rig sizing support. Bodies are decorative and do not block movement. Their separate torches can be picked up through TorchInteraction.
- `ProceduralCave` exposes Body Prefab and Body Room Percentage (0–100, default 25), using the same nearest-room rounding as centipedes. Independent seeded selection produces one body per selected room, random yaw and repeatable limb/torso/head dimensions through `CaveBody.Configure`. Bodies align to the local floor slope; room centers reserve 2.1 m clearance from scattered rocks when bodies are enabled.
- Torches lie next to bodies, begin lit and use the existing 180-second fuel lifetime. Their light participates in existing creature light reactions. Generated bodies are disposable preview/runtime children; the original Shadow Knight prefab is unchanged.
- Direct Play Mode checks in the saved 15-room scene verified 0/4/8/15 bodies at 0/25/50/100%, repeated seed selection, removal of player/gait components and independent burning lights. A camera capture verified the fallen pose and adjacent torch. Editor returned to Edit Mode. These are direct checks, not an automated suite; extreme terrain configurations were not exhaustively tested.

- Corpse torch pickup follow-up: successful transfer binds TorchAttack to the new torch and adapts its grip to the player only at attachment, preserving its world position during reach and its remaining fuel. Direct Play Mode checks passed corpse torch pickup, retained fuel, attack charging the new torch, dropping it, wall rejection and picking it up after burnout. An initial check caught premature grip assignment moving the ground torch; moving that assignment to attachment fixed the check. Tests stepped methods on prefab instances, not physical E-key input; temporary objects were discarded by returning to Edit Mode.

## Treasures

- `Assets/Prefabs/Treasures/Golden Chalice.prefab` contains a hollow revolved gold cup with six rubies, a trigger collider and subtle star particles. Assets are shared; the model is 0.81 m tall.
- `Treasure` stores a positive `Value` (prefab default 100) for future gameplay. Collection and scoring are not implemented. Point/spot lights activate glints at `Light Threshold` (0.005), with hysteresis; shadow-casting lights respect collider occlusion, ignoring the treasure and emitting torch only. Directional light is ignored. Checks run every 0.15 s with a shared light cache refreshed every second. Darkness clears particles.
- `ProceduralCave` exposes `Treasure Prefab` and `Treasure Room Percentage` (default 50). A separate seeded shuffle populates selected rooms. Values now come from the selected prefab, rather than a random 50–150 roll. Generated treasures lie on their sides with seeded yaw and 82–98 degree roll relative to the floor. Candidate positions span the chamber, with an 80% preference for wall-adjacent clearance. Full footprint checks avoid irregular walls, body clearances and other treasures; rock placement reserves the selected positions. Actual mesh vertices determine floor support height with a 0.015 m margin. Rooms without a valid candidate are skipped. Rebuilding replaces them and reproduces positions/values. The saved 15-room scene generates eight chalices.
- Direct Play Mode checks: 0/50/100 percent yielded 0/8/15 treasures; values stayed in range and regeneration reproduced names, positions and values. Exposure 0.008889 activated glints; an intervening cube or disabled light produced zero exposure and no glints. Close-up camera capture confirmed the hollow cup silhouette under the existing dither pipeline. Full player-driven exploration/readability remains unverified. No persistent automated suite was added; temporary Play Mode objects were discarded and Edit Mode restored.

- Fallen treasure checks: all eight chalices in the saved layout generated in Play Mode with minimum mesh-to-height-field clearance 0.015 m and near-horizontal cup axes; regeneration reproduced positions, rotations and values. A temporary lit camera confirmed a fallen chalice beside a wall. These checks used the analytic floor height, not exhaustive physics contact tests across seeds; temporary objects were discarded and Edit Mode restored.
- Treasure glints follow the combined world-space mesh bounds in LateUpdate, centred horizontally and held above the highest point plus the emission radius and 0.08 m clearance. Their world rotation stays upright independently of the fallen cup. Direct Play Mode check of all eight cups measured 0.08 m minimum emitter-sphere clearance and zero horizontal centre error.
- Treasure particle size scales linearly with 3D distance to the scene's ProceduralCharacter: full size at zero distance, 0.505 at 5 m, and 0.01 at/above Minimum Size Distance (default 10 m). Distant Size Fraction defaults to 0.01. The size-over-lifetime module multiplies the original authored curves, affecting live particles without modifying Start Size; original module settings restore on disable. Without a player, full size is used. Direct Play Mode calls verified 0/5/10/15/0 m and disable/re-enable without cumulative shrinking; no console errors. Preserved user prefab tuning: Start Size 1–2 and emission rate 1.

- Five treasure types share Golden Chalice as the base prefab (value 100). Royal Crown (300), Royal Orb (240), Golden Idol (180) and Silver Flask (140) are genuine Prefab Variants with overridden meshes, materials/colliders and values; the original ruby children are disabled and replaced by variant jewels. Particle settings remain inherited. ProceduralCave exposes Treasure Variants alongside Treasure Prefab and cycles through a separately shuffled type bag, ensuring variety before repeats. Model bounds determine the placement centre. All authored models fit the existing reserved footprint.
- Variant verification: Unity reported Variant for all four derived assets. Direct Play Mode generation produced eight objects spanning all five values/types; mesh-to-height-field clearance remained at least 0.015 m. A temporary camera captured all five silhouettes under the game renderer. No console errors; Edit Mode restored and preview objects discarded. Saved existing unsaved cave tuning (rock density/size and centipede percentage) alongside the new variant references.

## Dither accent materials

- `Assets/Settings/DitherAccent.shader` (`CardsUnity/Dither Accent`) is an opaque, URP Lit-based shader. `Accent Color` (`_BaseColor`) supplies both the lit surface color and its replacement for the dither palette's Paper color. Assign a material using this shader directly to a MeshRenderer; no component or layer is required. The first version exposes Base Map, Metallic and Smoothness; transparent/cutout accents are not supported.
- `Royal Crown.prefab` overrides its body material with `Assets/Prefabs/Treasures/Royal Crown Yellow.mat`; jewels retain their inherited material. Other treasure prefabs and the shared Chalice Gold material remain unchanged.
- `DitherAccentRendererFeature` on PC_Renderer and Mobile_Renderer runs an opaque, depth-equal `DitherAccent` mask pass before post-processing and the existing OneBitDither material after post-processing. The old Full Screen Pass feature is disabled; do not enable both. This implementation uses URP 17 Render Graph (the project's current mode). A per-camera RGBA8 mask carries accent RGB and coverage; explicit Render Graph dependencies and per-draw property blocks prevent stale masks across cameras. The final pass swaps the color output instead of making the old full-screen input copy.
- OneBitDither retains its illumination-derived tone, grain and paper effects, replacing only Paper on marked visible surfaces. The accent mask follows the unwarped silhouette to avoid color bleeding onto neighbouring objects. Darkness remains Ink; accents do not emit light.
- Direct Play Mode render checks: yellow crown beside an ordinary palette crown, point light and standalone Handheld Torch captures, and a 640x360 readback yielding 2817 yellow pixels lit, zero unlit and zero behind an opaque wall. Shader compilation passed. These were temporary editor checks, not an automated suite or a target-device GPU benchmark. Temporary objects were discarded and Edit Mode restored.

## Gold coins

- `Assets/Prefabs/Treasures/Gold Coin.prefab` is a separate `Treasure` worth 1, with a 0.48 m bevelled disc, raised rim and diamond stamp, trigger collider, and smaller light-gated glints. Its `Coin Gold.mat` uses `CardsUnity/Dither Accent` to retain yellow through dithering.
- `ProceduralCave` exposes `Coin Prefab` and `Coins Per Room` (0–8, default 2). Coins spawn independently after rocks, lie flat along the floor slope, and avoid walls, rock footprints, large treasures, each other and reserved body locations. A separate seed stream preserves existing layout and large treasure/rock placement. Crowded rooms may receive fewer coins after bounded placement attempts. Coins are disposable generated children; they are not part of the large treasure variant bag.
- Direct Play Mode checks in the saved 15-room cave: 0/2/8 coins per room yielded 0/30/120 coins, always worth 1, alongside eight unchanged large treasures. Rebuilding reproduced coin positions. Mesh vertices retained at least 0.008 m clearance over the analytic floor; no overlaps with rock colliders were found. A close-up camera capture verified the yellow coin under a standalone torch and the dither pipeline. No console errors; temporary objects discarded and Edit Mode restored. These are direct editor checks, not a persistent automated suite; collection/scoring remains unimplemented as for other treasures.
