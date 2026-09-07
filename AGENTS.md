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
| `HandheldTorch.cs` | LateUpdate holding pose, torch inertia, smooth light flicker, illumination controls, and particle drift |
| `ShadowKnightProportions.cs` | Editable limb/body dimensions, rig and collider sizing, torch grip, and optional helmet |
| `ProceduralSpine.cs` | Three-joint spine, neck stabilization, and per-instance CPU torso deformation driven by gait styles |
| `FootstepAudio.cs` | One-shot footstep playback on gait foot contacts, with per-step pitch randomization and Sneak/Walk/Run volume |
| `MusicPlayer.cs` | Looping background music with fade-in/fade-out and an optional mixer group |

There are currently no project-owned `.asmdef` files or automated test suites under `Assets/Scripts/`. Do not assume `CardsUnity.Runtime` or `CardsUnity.Tests` assemblies exist. Add folders and assemblies only when needed; keep data-only calculations independent of scene objects where practical.

## Controls and animation behavior

- WASD/arrows or the gamepad left stick move relative to the camera.
- TorchNight uses Rewired Player0: MoveHorizontal/MoveVertical (WASD or left stick), held Run (left Shift or the mapped pad button), and toggled Sneak (C). Run is read from every controller type and overrides Sneak and Walk; releasing it returns to whatever the stick or the Sneak toggle asks for. Sneak stays restricted to the keyboard input source, because toggling it also hands control back from the stick.
- The gamepad left stick chooses between Sneak and Walk by deflection: below Walk Threshold it sneaks, above it walks. Running is never derived from deflection. `ProceduralCharacter` exposes Idle Threshold and Walk Threshold, kept ordered by `OnValidate`. Movement speed scales with deflection because the input vector keeps its magnitude.
- Below Idle Threshold the stick releases the automatic style and the manual style mixer takes over again, so a connected but centred gamepad never overwrites the Inspector sliders.
- The daytime scene has no Rewired manager and falls back to Input System: keyboard movement keeps priority, the left stick takes over once deflected past Idle Threshold, Run is left Shift or left stick click, and Sneak toggles on C.
- Sprinting smoothly changes the visible style sliders to Run and fades the other weights. Releasing sprint restores the previous custom mix, including after rapid toggling.
- Gait phase advances from actual horizontal distance travelled. Avoid animating a full walk when blocked by a wall.
- Styles: Walk, Double Bounce Walk, Strut, Shuffle, Sneak, Run, Jump, Fast Run, Tip Toe, and Skip.
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

- Audio assets live in `Assets/Audio/`: `SFX/` (footstep samples), `Music/`, and `Mixers/Deep.mixer` with a single `Master` group. Footsteps and music both route to that group.
- `TorchNight` has a `Music` object with `MusicPlayer`, playing `Assets/Audio/Music/Stereo_Color_Dark-Background_main.wav` looped and faded in. Music tracks are imported as Streaming/Vorbis; short samples stay Decompress On Load.
- Footstep volumes, pitches and the mixer group are tuned on the `Shadow Knight` prefab asset itself. Preserve that tuning.

## Torch and rendering

- Torch settings are on `Handheld Torch` in the character's right forearm hierarchy.
- `Brightness` and `Light Range` control illumination; flicker and sway have separate settings.
- Torch pose runs after gait updates. Preserve this ordering so the holding pose does not fight the walking animation.
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

## Procedural centipede

- `TorchNight` contains three `Assets/Prefabs/Centipede.prefab` instances. `ProceduralCentipede` generates a disposable preview/runtime rig using shared URP materials. Generated children are not saved; edit the root component.
- `Segment Count` (3–48) and `Size` (0.25–3) rebuild the creature independently per instance, including in Edit Mode. Duplicate the prefab to add differently sized creatures; leave root Transform scale at one. An unassigned target resolves the scene's `ProceduralCharacter`.
- Distance travelled drives the travelling leg wave. Body segments follow the head at fixed spacing. Sphere casts and ground probes steer around local obstacles; this is local steering, not global pathfinding, and long bodies can cut corners.
- States are Stalking, Fleeing and Hiding. Point/spot light exposure across all segments triggers flight; shadow-casting lights respect collider occlusion. Directional moonlight is ignored. Exposure is a gameplay approximation, not a sample of rendered pixels or baked lighting. `Fear Threshold`, `Safe Light Ratio`, and `Hide Duration` tune the reaction.
- `Tick(float dt)` supports direct deterministic checks. Verified in the editor's Play Mode: dark pursuit stops near 1.2 m, restored torch triggers flight at 4.5 m/s, darkness triggers hiding and pursuit resumes; collider occlusion blocks exposure; 3/48 segments and 0.25/3 size generate successfully. These checks are not a persistent automated test suite.

- `Natural variation` controls independent Perlin steering noise, speed/tempo differences, brief stalking pauses, varied hiding duration and leg phase offsets. `Behaviour Variation = 0` restores uniform behaviour; `Random Seed = 0` chooses an instance-specific runtime seed, while equal nonzero seeds give repeatable behaviour for equal inputs. Randomness uses a private generator and a clock advanced by `Tick`, without modifying Unity's global Random state. Flight cancels pauses and limits steering/speed variation.
