# Project guidance

## Working agreement

- Communicate with the user in Polish. Write code, documentation, comments and commit messages in English.
- Inspect `git status --short` before editing. Preserve unrelated work, Inspector tuning and serialized references; keep changes focused and avoid vendor refactors.
- After completing work, review and commit **all outstanding repository changes**, including pre-existing changes, imported assets, package updates and new non-ignored files. The user explicitly authorizes this workflow. Respect requests to leave changes uncommitted. Do not push unless requested.

## Token budget and delegation

Optimize total work: context, reasoning, tool output, child agents and retries. A smaller model does not by itself guarantee fewer tokens.

- Handle small, sequential tasks locally. Delegate only a concrete independent subtask when its benefit exceeds startup/context overhead and the parent can do useful work alongside it. This guide authorizes that selective delegation and explicit child-model selection; it does not require agents on every task.
- Select the least expensive **available, capable** model from the current tool/runtime model list. The table is project policy, not a price or availability guarantee. Honor a user-specified model. If an entry is unavailable, use an exposed equivalent by capability; do not guess model IDs or install providers.

| Subtask | Codex preference when exposed | Reasoning |
| --- | --- | --- |
| Bounded search, file inventory, documentation edits, straightforward mechanical changes | `gpt-5.6-luna` | low |
| Focused C# implementation, ordinary bug fix or meaningful test | `gpt-5.6-terra` | medium |
| Several interacting components, difficult diagnosis or substantive code review | `gpt-5.6-sol` | medium; high if needed |
| Difficult IK/math, cave traversal, rendering correctness or unresolved cross-system failure | `gpt-6-astra` | high |

- Start at the appropriate tier; escalate when a concrete failed check or unresolved reasoning problem warrants it. Pass the evidence and narrow the problem instead of repeating a broad investigation. Do not default to maximum reasoning, duplicate reviews or nested delegation.
- For each child, specify scope, owned files, acceptance checks and a brief result format (changes, evidence, risks). Pass only relevant context. In Codex, prefer `fork_turns="none"` with explicit `model` and `reasoning_effort`; a full-history fork inherits settings and cannot override the model. Follow the live tool schema if it changes.
- Default to at most one child; use more only for substantial independent work. Only one agent may mutate the Unity Editor at a time. Avoid simultaneous edits to the same files. The parent owns integration and the final repository commit.
- These rules select child models when the host allows it; they do not change the active parent model or account settings. If model selection is unsupported, continue locally and disclose the limitation when relevant.

## Read only what the task needs

- Read this guide once per context. `CLAUDE.md` is a short entry point, not a second project manual.
- Start with `rg` / `rg --files` in project-owned paths; inspect matching symbols and bounded line ranges. Expand into callers, assets or packages only when evidence requires it.
- Detailed behavior and historical verification live in [docs/gameplay-reference.md](docs/gameplay-reference.md). Search its headings, then read only relevant sections. Do not preload the entire reference or historical design documents.
- Batch independent reads and return compact summaries/counts from tools. Keep full logs in files; inspect errors and relevant context. Avoid repeated full scene hierarchies, source dumps, console history and screenshots without a new visual question.
- Complete the smallest meaningful checks for the change; broaden only after failures or new evidence. Do not remove required verification merely to save tokens.

## Current project map

Unity **6000.3.10f1**, URP **17.3.0**. Confirm `ProjectSettings/ProjectVersion.txt` and `Packages/manifest.json` before dependency changes.

Playable procedural 3D cave prototype: articulated knight, distance-driven gait/IK, finite torch fuel, light-reactive centipedes with attacks, knockdown/stress, fallen bodies, treasure pickup and session inventory.

| Path | Purpose |
| --- | --- |
| `Assets/Scenes/ProceduralCave.unity` | Playable generated cave, torch lighting, residents, bodies, treasures and coins |
| `Assets/Scenes/TorchNight.unity` | Dark obstacle course |
| `Assets/Scenes/ProceduralMovement.unity` | Daytime movement playground, Input System fallback |
| `Assets/Scenes/SampleScene.unity` | Original scaffold |
| `Assets/Scripts/Controllers/` | Runtime gameplay in `CardsUnity.Controllers` |
| `Assets/Scripts/Controllers/Editor/` | Cave and knight proportions inspectors |
| `Assets/Scripts/Rendering/DitherAccentRendererFeature.cs` | URP Render Graph accent/dither passes in `CardsUnity.Rendering` |
| `Assets/Prefabs/` | Shadow Knight, Handheld Torch, Centipede, Fallen Knight, CaveRocks and Treasures |
| `Assets/Settings/`, `Assets/Materials/Cave/`, `Assets/Audio/` | Rendering/shaders, cave materials and audio |

Gameplay entry points (filenames below are under `Assets/Scripts/Controllers/`):

- Movement/animation: `ProceduralCharacter.cs`, `CharacterFollowCamera.cs`, `CartoonCharacterGait.cs`, `GaitStylePose.cs`, `ProceduralSpine.cs`, `ShadowKnightProportions.cs`.
- Torch/combat/player state: `HandheldTorch.cs`, `TorchAttack.cs`, `TorchInteraction.cs`, `CharacterKnockdown.cs`, `CharacterStress.cs`.
- Cave/creatures: `ProceduralCave.cs`, `CrawlSurfaceMesh.cs`, `ProceduralCentipede.cs`, `ProceduralCentipede.Attack.cs`, `CaveBody.cs`.
- Collection/audio: `Treasure.cs`, `CharacterInventory.cs`, `FootstepAudio.cs`, `CentipedeLegAudio.cs`, `MusicPlayer.cs`.

## UI text

- Use TextMeshPro for all project-owned game UI text: `TextMeshProUGUI` components and `TMP_Text` references, including runtime-created labels and prompts. Do not add legacy `UnityEngine.UI.Text`, `TextMesh`, or IMGUI text (`GUI.Label`, `GUI.Box`, `GUILayout`) to game UI. Editor inspectors and third-party packages are outside this rule.
- Preserve layout, serialized references, masking and input when migrating text. Verify Polish glyph coverage, wrapping and overflow with the assigned TMP font and fallbacks.

## Behavior to preserve

- Camera-relative WASD/arrows/stick; held Run overrides Walk/Sneak, C toggles keyboard Sneak. Stick deflection selects Sneak/Walk, never Run; centered sticks release automatic style control. Sprint restores the custom mix of all twelve styles.
- Player control takes priority: pickup must preserve movement, speed and player-controlled facing. Losing range or clear reach before contact cancels that attempt with a smooth recovery.
- Gait phase/footsteps use actual distance, including stopping at walls. Jump/hop styles are visual. Keep named procedural rig parts and limb lengths stable; this is not a Humanoid Avatar.
- Space holds torch windup and strikes on release. Preserve overlay order: gait/spine, HandheldTorch (100), TorchAttack (150), TorchInteraction (175). Pose suppression must be updated every frame by its owner.
- E picks up the nearest reachable treasure first, otherwise an available torch with empty hands. It never drops or replaces a held torch. Collection uses the free arm, rechecks reach/obstruction at transfer and updates session-only inventory. Knockdown drops the torch and recovery advances only with actual movement.
- Torch fuel persists through dropping, pickup and disable/re-enable; strikes consume additional fuel. Fire/smoke simulate in world space. Corpse torches remain transferable with their remaining fuel.
- Cave/centipede generated children and meshes are disposable; change generator settings or source prefabs, not generated children. Preserve seeded independence of layout, rocks, residents, bodies and treasures, traversable paths and cave interior restrictions.
- Centipede steering is local, not global pathfinding. Dormant activation, light/shadow pursuit, prolonged-light/darkness provocation and lunge attacks coexist. Gameplay exposure is collider-based, not rendered illumination.
- Preserve scene-specific lighting, prefab overrides and audio mixer routing/tuning. Dither accents use the custom renderer feature; do not also enable the old Full Screen Pass.

## Choose CLI or Unity MCP per operation

Use the shortest reliable route for the required evidence; a task may use both.

| Operation | Preferred route |
| --- | --- |
| Search/read/edit C#, shaders, docs; inspect serialized values read-only; Git/diffs | Shell CLI (`rg`, scoped file reads, patches, `git`) |
| Documentation-only change | CLI; no Unity discovery, launch or Play Mode required |
| Scene/prefab/object changes, serialized reference writes, asset import/settings, Undo | Unity MCP in the open editor |
| Compilation, live state/console, deterministic Tick/pose checks, Play Mode, camera capture | Unity MCP in the open editor |
| Existing automated tests/build with this project closed | Unity CLI batch mode, matching the project editor version |

- CLI means local shell tools or the Unity executable, not a second agent CLI. Do not launch an extra LLM session just to read/edit files. Do not start a second Unity process on a project already open in the editor.
- For editor operations prefer the user-selected **unity-mcp relay**: `~/.unity/relay/relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64 --mcp`. Expand `~` in client configurations requiring an absolute path. Discover only the needed tools and follow their current schemas. The installed Coplay package is a separate integration.
- Before editor mutations inspect active scene, dirty state and Play Mode. Preserve unsaved work before scene switches; save intended edits in Edit Mode, restore the prior mode after testing and remove temporary objects.
- Relay commands use `internal class CommandScript : IRunCommand` with `Execute(ExecutionResult result)` when required by the current schema. Register mutations with its tracking helpers. Camera capture uses a GameObject instance ID, not a Camera component ID; re-query after reloads.
- Group related inspections into one bounded command returning only useful fields. After compilation interruptions, allow reload and retry a read-only check. Inspect for partial success before retrying any mutation; avoid repeated discovery loops.
- If MCP is unavailable, continue independent CLI work. Use batch verification only when the project is closed; otherwise report the specific unverified editor behavior. Do not bypass reference-preserving editor operations by rewriting scene/prefab YAML wholesale.

## Verification and asset hygiene

- Documentation-only: check links, accuracy and `git diff --check`; no gameplay tests needed.
- Script/shader edits: let Unity compile and inspect new console errors. Movement/visual changes also need focused Play Mode checks and camera inspection when relevant. Check affected boundaries/transitions (e.g. stop/start and custom mix restoration for gait changes), not every historical scenario.
- Direct `Tick`/pose/physics calls are not physical input tests or a persistent automated suite. Report what was actually checked and what remains unverified. Restore editor mode and remove temporary test objects; do not save test poses.
- No project-owned `.asmdef` or automated suite currently exists under `Assets/Scripts/`. Do not assume `CardsUnity.Runtime` or `CardsUnity.Tests` exists. Add tests/assemblies only when warranted; keep pure calculations independent of scene objects where practical.
- If tests are added, use the open editor Test Runner or, with the project closed, `/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults/EditMode.xml -logFile TestResults/EditMode.log` (substitute PlayMode for that suite). Inspect XML and logs; exit status alone is insufficient.
- Let Unity create `.meta` files; preserve GUIDs and commit new assets with their metadata. Preserve serialized field names or use migration attributes. Respect `.gitignore`, including Library/Temp; never force-add build outputs.
- Avoid broad edits to DOTween, Rewired, Feel/MMFeedbacks, Quibli, TrueShadow, FMOD or other packages. Installed packages are not proof that a gameplay feature uses them.

## Maintaining these instructions

Keep shared workflow rules and the concise current map here. Keep `CLAUDE.md` focused on its host. Update relevant sections of `docs/gameplay-reference.md` for implementation detail; replace stale claims rather than appending repeated narratives. Store long verification evidence in task-specific documentation, not the always-loaded guide. Current code/assets take precedence over historical checks.
