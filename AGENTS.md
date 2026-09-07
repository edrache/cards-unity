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

There are currently no project-owned `.asmdef` files or automated test suites under `Assets/Scripts/`. Do not assume `CardsUnity.Runtime` or `CardsUnity.Tests` assemblies exist. Add folders and assemblies only when needed; keep data-only calculations independent of scene objects where practical.

## Controls and animation behavior

- WASD/arrows or the gamepad left stick move relative to the camera.
- TorchNight uses Rewired Player0: MoveHorizontal/MoveVertical (WASD), held Run (left Shift), and toggled Sneak (C). Run temporarily overrides Sneak; releasing it restores Sneak, and disabling both restores the custom mix. The daytime scene retains Input System keyboard/gamepad controls.
- Sprinting smoothly changes the visible style sliders to Run and fades the other weights. Releasing sprint restores the previous custom mix, including after rapid toggling.
- Gait phase advances from actual horizontal distance travelled. Avoid animating a full walk when blocked by a wall.
- Styles: Walk, Double Bounce Walk, Strut, Shuffle, Sneak, Run, Jump, Fast Run, Tip Toe, and Skip.
- Style weights are normalized and transitions are smoothed. All-zero weights fall back to Walk.
- Jump and hop styles are visual animations; they do not jump the CharacterController.
- Head orientation compensates for torso rotation and looks ahead.
- Arm elevation controls the upper arm. Forearm controls set elbow bend relative to that arm, with separate speed and swing contributions.
- Preserve existing Inspector tuning and serialized references. Use serialization migration attributes when renaming fields.
- `Animate` accepts an optional delta time for deterministic pose checks. `HandheldTorch.Tick` similarly accepts explicit time values.

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
