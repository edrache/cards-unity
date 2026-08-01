# LOOM Implementation Plan

**Plan version:** 1.0  
**Last updated:** 2026-08-01  
**Current milestone:** M0 — Repository and FMOD foundation  
**Current status:** READY  
**Next action:** Configure and verify the FMOD Studio project connection, create `Master/MUS_Synth`, build the Master Bank, and confirm Unity can resolve `bus:/MUS_Synth`.

## Purpose

This is the canonical execution and handoff document for LOOM. It records scope, decisions, milestone status, acceptance criteria, verification evidence, blockers, and the next executable action.

Agents must read this file before implementation and update it whenever a task materially changes project state. Chat history is not a substitute for this plan.

## Status Legend

- `NOT STARTED` — no implementation work has begun.
- `READY` — prerequisites are known and the item can be started.
- `IN PROGRESS` — actively being implemented; normally only one item may have this status.
- `BLOCKED` — work cannot continue until the recorded blocker is resolved.
- `DONE` — every listed acceptance criterion is verified.
- `DEFERRED` — intentionally outside the current scope.

## Product Scope

The first product is an engine playground. It must let a developer play notes and experiment with synthesis before adaptive composition features are added.

### In scope

- Unity 6.0 desktop Editor and standalone builds for macOS and Windows.
- FMOD Core synthesis and sample-accurate scheduling.
- FMOD Studio mixer routing.
- A playable polyphonic synthesizer.
- Computer-keyboard note input and a small parameter UI.
- Deterministic integer-tick transport and step sequencing.
- Automated Core tests and targeted Unity/FMOD integration tests.

### Out of scope for the current roadmap

- Evaluation of musical quality or style.
- Production sample libraries and sample import workflow.
- Console targets and console SDK integration.
- Mobile and WebGL.
- Native C++ DSP plugins and granular synthesis.
- Full DAW-style authoring tools.
- Bit-identical rendered PCM across runs or platforms.

## Fixed Technical Decisions

| Area | Decision |
|---|---|
| Unity | 6000.3.10f1 |
| FMOD integration | 2.03.07 |
| FMOD project | `fmod/loom/loom.fspro` |
| Initial platforms | macOS and Windows desktop |
| Initial synthesis | Built-in FMOD oscillator/DSP graph; no samples required |
| Studio bus | `bus:/MUS_Synth` |
| Musical clock | Integer `long` ticks, 960 PPQN |
| Initial meter | 4/4 |
| Initial tempo | 120 BPM |
| Intended sample rate | 48 kHz, verified at runtime rather than assumed |
| Scheduler lookahead | 200 ms initial tuning value |
| Determinism contract | Identical logical event stream for identical seed, state, and mutation log |
| Core boundary | Pure C#, with no Unity or FMOD references |

## Target Assemblies and Ownership

| Assembly | Responsibility | Allowed dependencies |
|---|---|---|
| `Loom.Core` | Time, tempo, RNG, note/event data, sequencing, mutations | .NET/BCL only |
| `Loom.Fmod` | FMOD systems, clocks, synth voices, routing, error handling | `Loom.Core`, FMOD |
| `Loom.Unity` | Lifecycle, input, authoring conversion, MonoBehaviours | `Loom.Core`, `Loom.Fmod`, Unity |
| `Loom.Demo` | Playground scene and UI | All runtime LOOM assemblies, Unity UI |
| `Loom.Tests.EditMode` | Fast deterministic and adapter tests | Test Framework and required targets |
| `Loom.Tests.PlayMode` | Audible/runtime integration tests | Test Framework and runtime assemblies |

## Roadmap Summary

| Milestone | Deliverable | Status |
|---|---|---|
| M0 | Repository rules and verified FMOD foundation | READY |
| M1 | Playable polyphonic synth playground | NOT STARTED |
| M2 | Deterministic transport and step sequencer | NOT STARTED |
| M3 | Multiple tracks, scale, harmony, and routing | NOT STARTED |
| M4 | Quantized mutation layer and game-facing API | NOT STARTED |
| M5 | Save/replay determinism and engine hardening | NOT STARTED |
| M6 | Authoring, MIDI, samples, and advanced synthesis | DEFERRED |

## M0 — Repository and FMOD Foundation

**Goal:** establish reliable project structure and prove that Unity, FMOD Core, FMOD Studio, and the mixer bus are connected.

### Work items

- [x] `DONE` Record agent workflow and architectural invariants in `AGENTS.md` and `CLAUDE.md`.
- [x] `DONE` Create this canonical implementation plan.
- [ ] `READY` Configure the Unity FMOD integration to use `fmod/loom/loom.fspro`.
- [ ] `READY` Create the Studio mixer bus `Master/MUS_Synth`.
- [ ] `READY` Build the Master Bank for desktop.
- [ ] `NOT STARTED` Confirm Unity loads the bank and resolves `bus:/MUS_Synth`.
- [ ] `NOT STARTED` Create the LOOM folder layout and assembly definitions.
- [ ] `NOT STARTED` Confirm all assemblies compile with the intended dependency boundaries.

### Acceptance criteria

- Unity reports no FMOD initialization or version mismatch errors.
- The FMOD project path stored in Unity is non-empty and points to `fmod/loom/loom.fspro` or a stable repository-relative equivalent.
- Desktop banks build successfully.
- Runtime code can acquire the Core system and the `MUS_Synth` Studio bus channel group.
- `Loom.Core` compiles without Unity or FMOD references.
- EditMode and PlayMode test assemblies are discoverable.

### Verification evidence

- FMOD project exists and uses serialization model `Studio.02.03.00`.
- FMOD for Unity wrapper declares version `0x00020307`.
- As of 2026-08-01, the Unity FMOD settings serialized on disk still show an empty `sourceProjectPath`; connection is not yet verified.

## M1 — Playable Polyphonic Synth Playground

**Goal:** play and shape notes interactively without sample assets.

### Work items

- [ ] `NOT STARTED` Define `Note`, `NoteEvent`, `VoiceHandle`, and `IInstrument` contracts in Core.
- [ ] `NOT STARTED` Implement MIDI-note-to-frequency conversion with tests.
- [ ] `NOT STARTED` Implement one FMOD oscillator voice with explicit lifecycle and result checking.
- [ ] `NOT STARTED` Add ADSR amplitude control using DSP-clock-aligned fade points or an equivalent verified FMOD graph.
- [ ] `NOT STARTED` Add waveform selection, gain, octave, cutoff, and resonance controls supported by the initial graph.
- [ ] `NOT STARTED` Add fixed-size polyphony and deterministic voice stealing.
- [ ] `NOT STARTED` Route every voice through `bus:/MUS_Synth`.
- [ ] `NOT STARTED` Add computer-keyboard note input.
- [ ] `NOT STARTED` Add a demo UI and scene.
- [ ] `NOT STARTED` Add shutdown, domain-reload, focus-loss, and `AllNotesOff` handling.

### Acceptance criteria

- At least eight simultaneous notes can sound without unmanaged-resource leaks.
- Key-down starts the correct pitch and key-up releases the same logical voice.
- ADSR, waveform, octave, gain, cutoff, and resonance can be changed from the demo where supported by the verified DSP graph.
- Audio is audible through `MUS_Synth`; muting that bus silences the synth.
- Voice stealing is repeatable and documented.
- Repeated Play Mode entry/exit produces no invalid-handle or leaked-DSP errors.
- The demo runs in the macOS Editor and has a documented Windows verification path.

## M2 — Deterministic Transport and Step Sequencer

**Goal:** schedule notes from integer musical time with no accumulating drift.

### Work items

- [ ] `NOT STARTED` Implement `MusicalTime`, 4/4 meter, and 960 PPQN constants.
- [ ] `NOT STARTED` Implement immutable tempo-map segments, initially one 120 BPM segment.
- [ ] `NOT STARTED` Implement tick-to-sample/DSP-clock conversion with explicit rounding rules.
- [ ] `NOT STARTED` Implement stateless hashed RNG and named random slots.
- [ ] `NOT STARTED` Implement a bounded, allocation-free scheduling buffer.
- [ ] `NOT STARTED` Implement one step pattern with rests, velocity, duration, probability, ratchets, and microtiming tick offsets.
- [ ] `NOT STARTED` Add transport start, stop, pause, resume, and panic behavior.
- [ ] `NOT STARTED` Add a ten-minute timing/drift measurement.

### Acceptance criteria

- Boundary tests cover beats, bars, negative/invalid inputs, long sessions, and conversion rounding.
- Repeated scheduling windows emit every logical event exactly once.
- The same seed and pattern produce the same serialized event stream.
- Scheduler steady-state allocations are zero in the measured hot path.
- The ten-minute test shows no accumulating musical-clock drift; reported onset jitter is measured separately.

## M3 — Tracks, Scale, Harmony, and Mixer

**Goal:** coordinate multiple sequencers into a harmonically resolved multi-track engine.

### Work items

- [ ] `NOT STARTED` Add track definitions and independent pattern lengths.
- [ ] `NOT STARTED` Add scale-degree resolution for pitched tracks.
- [ ] `NOT STARTED` Add a separate percussion/sample-slot note model rather than routing drums through scale degrees.
- [ ] `NOT STARTED` Add harmony-plan and track-role resolution.
- [ ] `NOT STARTED` Add `MUS_Drums`, `MUS_Bass`, `MUS_Lead`, `MUS_Pad`, and `MUS_FX` buses when required.
- [ ] `NOT STARTED` Add track gain, mute, and basic effect parameters.
- [ ] `NOT STARTED` Demonstrate independent pattern lengths and stable scheduling order.

### Acceptance criteria

- Four tracks run concurrently with independent lengths.
- Pitched tracks resolve deterministically through scale and harmony data.
- Percussion uses explicit instrument slots.
- Mixer controls affect only their intended tracks.
- Same-tick event ordering is stable and tested.

## M4 — Mutation Layer

**Goal:** expose one game-facing API for immediate and quantized changes.

### Work items

- [ ] `NOT STARTED` Define versioned mutation verbs, targets, values, priorities, and sequence numbers.
- [ ] `NOT STARTED` Implement immediate, next-beat, next-bar, and defined next-phrase quantization.
- [ ] `NOT STARTED` Implement safe ramp semantics.
- [ ] `NOT STARTED` Implement track, pattern, harmony, intensity, and one-shot mutations.
- [ ] `NOT STARTED` Implement prominence profiles without embedding musical-quality judgement in the engine.
- [ ] `NOT STARTED` Add a Unity binding/demo layer for triggering mutations.

### Acceptance criteria

- Mutations in the same tick have deterministic ordering.
- Tempo changes cannot invalidate events already submitted inside the lookahead horizon.
- Immediate sequencer mutations have documented latency; one-shots use their documented bypass path.
- Public game code does not mutate sequencer internals directly.

## M5 — Save, Replay, and Hardening

**Goal:** reproduce future logical output from saved state and make the engine resilient.

### Work items

- [ ] `NOT STARTED` Add versioned save schema, stable IDs, seed, transport state, and mutation log.
- [ ] `NOT STARTED` Define snapshot/pre-roll handling for active voices, ramps, and effect history.
- [ ] `NOT STARTED` Add mutation-log compaction rules.
- [ ] `NOT STARTED` Compare deterministic event-stream hashes across runs.
- [ ] `NOT STARTED` Add tolerant audio-level integration checks without requiring bit-identical PCM.
- [ ] `NOT STARTED` Add resource, error, overflow, invalid-data, and reload tests.
- [ ] `NOT STARTED` Profile CPU, allocation, real/virtual voices, and scheduler cost.

### Acceptance criteria

- Loading produces the same future logical event stream from the declared synchronization point.
- Save format is versioned and rejects or migrates unsupported data explicitly.
- Active-voice and effect-tail limitations are documented and tested according to the chosen strategy.
- No FMOD resources remain allocated after engine disposal or Unity Play Mode exit.
- Performance results are recorded against a named machine and scenario.

## M6 — Deferred Expansion

Possible later work includes sample instruments, MIDI import, pattern authoring tools, procedural content generators, native synthesizer DSP, granular synthesis, packaging, mobile, and consoles. Move an item into the active roadmap only after updating scope, dependencies, and acceptance criteria.

## Handoff Checklist

Before ending a task that changed LOOM:

1. Run the narrowest relevant verification available.
2. Update the affected work-item statuses.
3. Add concrete evidence or state why verification could not run.
4. Record new architectural decisions under Fixed Technical Decisions or a dated changelog entry.
5. Set `Current milestone`, `Current status`, and `Next action` at the top of this file.
6. Do not mark a milestone complete while any acceptance criterion is unverified.

## Known Risks and Open Questions

- The viability and control range of FMOD's built-in oscillator graph must be proven during M1. If it cannot provide the required per-voice envelope/filter behavior, select another Core-only prototype strategy before considering a native C++ plugin.
- Studio bus channel groups are not permanent handles across all bank/lifecycle operations; adapter ownership must be tested.
- Unity's interactive Editor is normally open, so batchmode cannot be assumed available against the same workspace.
- Windows output and timing require verification on Windows hardware; macOS-only testing is not sufficient for a desktop milestone.
- Generated FMOD bank location and build policy must be verified during M0 and then documented here.

## Changelog

### 2026-08-01

- Created the canonical implementation plan.
- Narrowed the initial target to macOS and Windows desktop.
- Established engine-first scope: playable notes and a synth playground before sequencing and adaptive systems.
- Selected the built-in FMOD oscillator/DSP path for the first synth spike, with no sample dependency.
- Recorded the existing FMOD project at `fmod/loom/loom.fspro` and the unverified Unity project-link state.
- Added plan-maintenance and handoff rules for future agents.
