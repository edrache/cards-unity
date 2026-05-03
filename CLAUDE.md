# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Language

All code, documentation, comments, and commit messages must be written in **English**.
Conversation with the user is conducted in **Polish**.

## Project Overview

Unity 6 (6000.3.10f1) project. Currently a bare scaffold — no game logic has been implemented yet.

## Key Dependencies

| Asset | Purpose |
|---|---|
| DOTween (Plugins/) | Tweening / animation |
| Rewired (Rewired/) | Input management |
| Feel / MMFeedbacks | Screen shake, haptics, VFX feedback |
| Quibli | Stylized / toon shaders (URP) |
| TrueShadow (Le Tai's Asset/) | UI drop shadows |
| Unity Input System (`com.unity.inputsystem`) | New input backend |
| Cinemachine (`com.unity.cinemachine`) | Camera control |
| Timeline (`com.unity.timeline`) | Sequenced animations / cutscenes |
| Post Processing (`com.unity.postprocessing`) | Screen-space effects |
| Unity MCP (`com.coplaydev.unity-mcp`) | Editor control via MCP protocol |

Rendering: URP 17.3.0. PC and Mobile URP variants are in `Assets/Settings/`.

## Source Layout

Runtime code lives under `Assets/Scripts/`:

- `Data` — game state, runtime instances, immutable definitions, enums.
- `Config` — ScriptableObject tuning objects.
- `Runtime` — bootstrap helpers and shared runtime services.
- `Combat` — combat resolver code, result objects, previews.
- `Controllers` — MonoBehaviour game-flow coordinators.
- `UI` — UGUI views and pointer interaction components.
- `Tests` — Unity Test Framework tests.

Assemblies:

- `CardsUnity.Runtime` — runtime code under `Assets/Scripts/`.
- `CardsUnity.Tests` — EditMode tests under `Assets/Scripts/Tests/`.

## Design Documentation

The single source of truth for all game design is `docs/game-design-doc.md`.

When implementing any change — adding a mechanic, modifying a rule, removing a feature, changing a data model — you **must**:
1. Update the relevant section(s) in `docs/game-design-doc.md`.
2. Append an entry to the Changelog table at the bottom of that file (date, one-line description of the change).

Do not let the implementation diverge from the documented design without updating the doc. If a design decision is unclear or undocumented, treat `docs/game-design-doc.md` as the authoritative reference and ask the user before changing it.

## Verification Commands

Use the Unity 6000.3.10f1 batchmode runner from the repository root.

EditMode tests:

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testResults TestResults/EditMode.xml
```

PlayMode tests:

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -runTests -testPlatform PlayMode -testResults TestResults/PlayMode.xml
```

## Workflow

- Keep runtime data and resolver logic free of Unity scene dependencies unless the architecture explicitly calls for a `MonoBehaviour`.
- Prefer focused, incremental changes that preserve existing Unity asset references and serialized fields.
- Do not rewrite or regenerate Unity `.meta` files unless the asset operation genuinely requires it.
- When changing runtime gameplay flows, add frequent structured debug logs in the touched code paths so state transitions are visible in the Unity Console during debugging. Prefer logs around trigger evaluation, condition checks, mutations, and end-of-step summaries rather than a single final log.
