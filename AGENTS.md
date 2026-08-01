# AGENTS.md

This file provides guidance to coding agents working in this repository.

## Language

Write all code, identifiers, comments, new documentation, test names, and commit messages in English. Conduct conversation with the user in Polish.

`docs/loom-design-doc.md` is an existing Polish product/design draft. Treat it as source material; do not translate or rewrite it unless the task explicitly requires that.

## Current Objective

The repository is building **LOOM**, a deterministic adaptive-music engine for Unity. The first deliverable is an engine prototype, not a music-composition evaluation:

- desktop only: macOS and Windows;
- play individual notes from Unity;
- experiment with a polyphonic synthesizer;
- use FMOD Core for synthesis and sample-accurate scheduling;
- use FMOD Studio for mixer buses, routing, and later effects;
- no production sample library, consoles, native DSP plugin, or musical-quality assessment yet.

Use the built-in FMOD oscillator/DSP path for the initial synth spike. Do not require audio samples for the first milestone.

## Sources of Truth

Read these files before changing LOOM:

1. `docs/loom-implementation-plan.md` — canonical execution plan, current status, next action, and acceptance criteria.
2. `docs/loom-design-doc.md` — product intent and high-level architecture.
3. `AGENTS.md` — working rules for agents.

If implementation and the plan disagree, investigate the repository and update the plan in the same change. Do not silently treat stale plan entries as complete.

## Project and Tool Versions

- Unity: `6000.3.10f1`
- FMOD for Unity integration: `2.03.07`
- FMOD Studio project: `fmod/loom/loom.fspro`
- Rendering: URP `17.3.0`
- Tests: Unity Test Framework `1.6.0`

The FMOD project currently exists but is essentially empty. The serialized Unity FMOD settings may still contain an empty source-project path. Verify the actual connection and bank loading before marking FMOD setup complete.

## Target Architecture

Runtime code belongs under `Assets/Scripts/Loom/`:

```text
Assets/Scripts/Loom/
├── Core/       Pure deterministic C# domain code
├── Fmod/       FMOD Core and Studio adapters
├── Unity/      MonoBehaviours, input, lifecycle, and authoring adapters
├── Demo/       Playable synth/sequencer demonstration
└── Tests/      EditMode and PlayMode tests
```

Planned assemblies:

- `Loom.Core` — no `UnityEngine`, FMOD, scene, or asset dependencies;
- `Loom.Fmod` — FMOD implementation, references `Loom.Core`;
- `Loom.Unity` — Unity adapters, references `Loom.Core` and `Loom.Fmod`;
- `Loom.Demo` — disposable product demonstration layer;
- `Loom.Tests.EditMode` and `Loom.Tests.PlayMode` — verification.

Unity `ScriptableObject` assets are authoring/configuration adapters, not the canonical runtime model. Convert them into immutable or explicitly owned Core data before playback.

## Architectural Invariants

- Musical time uses integer `long` ticks. Initial defaults are 960 PPQN, 4/4, 120 BPM, and 48 kHz.
- Randomness is stateless and hash-derived. Do not use `UnityEngine.Random`, `System.Random`, wall-clock time, or unordered collection iteration in deterministic logic.
- The Core assembly does not reference Unity or FMOD.
- Sequencers emit abstract note events; instruments render them.
- FMOD DSP clocks are the only scheduling authority at the audio boundary.
- A tempo change must not invalidate already scheduled audio. For the MVP, apply tempo changes only at a safe quantized boundary beyond the scheduling horizon.
- Determinism means an identical logical event stream. Do not promise bit-identical PCM or restored reverb tails after loading.
- Hot scheduler paths must be allocation-free after initialization.
- Voice ownership must be explicit. A note-on operation must return or internally retain a stable voice handle so note-off and voice stealing are well-defined.
- Core-generated voices route through a Studio bus channel group. The initial required bus is `bus:/MUS_Synth`.

## FMOD Workflow

- Keep the FMOD Studio source project under `fmod/loom/`.
- Prefer editing mixer structure through FMOD Studio rather than hand-editing metadata XML.
- Never edit or commit user-specific state from `fmod/loom/.user/` as shared project configuration.
- The first mixer requirement is `Master/MUS_Synth` and a built Master Bank.
- Generated banks are build artifacts unless the implementation plan explicitly changes that policy.
- Check every FMOD `RESULT`; surface contextual errors rather than ignoring them.
- Treat Studio bus `ChannelGroup` handles as lifecycle-bound: acquire, validate, and release/reacquire safely around bank reloads and shutdown.

## Working With an Open Unity Editor

The user normally keeps the project open in Unity. Prefer Unity MCP or the active Editor session for scene changes, compilation, and tests when available. Do not launch a second batchmode Editor against the same project because Unity project locking and licensing can make the result misleading.

If batchmode verification is necessary, first confirm that the interactive Editor is closed or use a copied workspace explicitly prepared for that purpose.

## Plan Maintenance

Every LOOM task must update `docs/loom-implementation-plan.md` when it changes project state:

1. Before implementation, confirm the current milestone and mark only the selected bounded item `IN PROGRESS`.
2. Keep at most one plan item `IN PROGRESS` unless work is explicitly parallelized.
3. On completion, record evidence such as test names, scene path, or FMOD bank/build verification.
4. Mark an item `DONE` only when all of its acceptance criteria pass.
5. Add a dated entry to the plan changelog for material progress, decisions, blockers, or scope changes.
6. Leave a precise `Next Action` that another agent can execute without reconstructing context from chat history.

## Verification

Run the narrowest relevant tests first, then the milestone-level suite. When the Unity Editor is closed, these are the standard commands from the repository root:

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testResults TestResults/EditMode.xml
```

```bash
/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -runTests -testPlatform PlayMode -testResults TestResults/PlayMode.xml
```

For audio milestones, compilation alone is insufficient. Record manual or automated evidence for audible output, routing, note-off behavior, polyphony, console errors, and timing where applicable.

## Repository Hygiene

- Preserve unrelated user changes and untracked art assets.
- Do not regenerate `.meta` files without a corresponding Unity asset operation.
- Keep generated caches, FMOD banks, logs, and local user state out of source control according to `.gitignore`.
- Prefer focused incremental changes that leave the project compiling.
