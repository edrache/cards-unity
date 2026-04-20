# Phase 0 Project Setup

This document records the first-playable conventions established before gameplay implementation starts.

## Source Layout

Runtime code lives under `Assets/Scripts/`:

| Folder | Purpose |
|---|---|
| `Data` | Plain C# game state, runtime card instances, immutable card definitions, and shared enums. |
| `Config` | ScriptableObject tuning objects such as `GameConfig`, `AnimConfig`, and `CardVisualConfig`. |
| `Runtime` | Bootstrap helpers and shared runtime services that are not specifically combat or UI. |
| `Combat` | Pure combat math, resolver code, result objects, and read-only previews. |
| `Controllers` | MonoBehaviour game-flow coordinators such as `GameManager`, `RoundController`, `CombatController`, and `RewardController`. |
| `UI` | UGUI views, drag/drop surfaces, HUD, tooltip, reward, and combat result presentation. |
| `Tests` | Unity Test Framework test assemblies and fixtures. |

`CardsUnity.Runtime.asmdef` owns runtime code. `CardsUnity.Tests.asmdef` owns EditMode tests and references the runtime assembly.

## UI Text Stack

`com.unity.textmeshpro` is not listed in `Packages/manifest.json` or `Packages/packages-lock.json`. The first playable should therefore use UGUI `Text` components as the supported baseline.

Imported assets contain optional TextMeshPro integrations, but gameplay UI should not depend on TMP until the package is intentionally added to the project. If TMP is added later, card labels, values, tooltips, and combat result text can migrate without changing gameplay logic.

## First-Playable Technology Choices

- Use UGUI (`com.unity.ugui`) for the first playable UI.
- Use Unity EventSystem pointer interfaces for drag/drop before optional controller support.
- Use DOTween for view animation once UI prefabs exist.
- Keep MMFeedbacks, NiceVibrations, TrueShadow, and Quibli as presentation and polish layers.
- Keep core data, deck, combat, and round-flow logic independent from scene objects and presentation assets.
- Use Unity Test Framework for deterministic EditMode coverage of pure logic.

## Scene Bootstrap Strategy

The first playable should use a dedicated gameplay scene instead of relying on `Assets/Scenes/SampleScene.unity` as a long-term production scene.

Recommended bootstrap path:

1. Create a gameplay scene with one `GameManager` root.
2. Assign config assets and card definition collections through serialized fields or a small bootstrap asset.
3. Let `GameManager` initialize a new run, create `GameState`, and publish state/phase events.
4. Let UI views subscribe to events and render state. UI components should call controller methods for player intent.

Minimal bootstrapping is allowed before full UI work, but it should not implement gameplay rules outside the planned data, combat, and controller layers.

## Verification Commands

Use these commands once tests exist:

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testResults TestResults/EditMode.xml
```

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -runTests -testPlatform PlayMode -testResults TestResults/PlayMode.xml
```

Phase 0 does not add gameplay tests yet; it only establishes the assemblies and folders that future tests will use.
