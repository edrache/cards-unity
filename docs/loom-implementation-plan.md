# LOOM Implementation Plan

**Plan version:** 1.12
**Last updated:** 2026-08-01  
**Current milestone:** M2 — Deterministic transport and step sequencer
**Current status:** READY
**Next action:** Begin M2 in `Loom.Core` by implementing immutable `MusicalTime` with 960 PPQN and 4/4 defaults, an explicit non-negative tick contract, and deterministic bar/beat/tick decomposition; add focused EditMode boundary tests before introducing tempo or FMOD scheduling.

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
| FMOD bank output | `fmod/loom/Build/<platform>`; generated banks are ignored build artifacts |
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
| Resolved pitch contract | `Note` stores a validated MIDI note number from 0 through 127; scale-degree resolution remains a later Conductor concern |
| Pitch-to-frequency conversion | `Note.FrequencyHz` returns `double` using twelve-tone equal temperament and A4 = MIDI 69 = 440 Hz; conversion to FMOD `float` occurs explicitly at the adapter boundary |
| Logical note event | `NoteEvent` stores a playable velocity from 1 through 127 and a non-overflowing `[StartTick, EndTick)` interval in `long` ticks |
| Voice ownership | `IInstrument.NoteOn` returns a non-zero stable `VoiceHandle`; `NoteOff` consumes that exact handle, `AllNotesOff` invalidates outstanding handles, and the instrument owns disposable resources |
| Initial oscillator voice | `FmodOscillatorVoice` owns one configurable `DSP_TYPE.OSCILLATOR` plus one `DSP_TYPE.MULTIBAND_EQ` low-pass, starts its channel paused before applying gain and filter, checks every `FMOD.RESULT`, accepts an explicit target `ChannelGroup`, and stops the channel before releasing both DSPs; its parameterless `Start` remains a focused standalone Core-output path |
| Initial ADSR envelope | `FmodAdsrEnvelope` stores validated seconds and a normalized sustain level, resolves durations against FMOD's runtime sample rate, and schedules linear `addFadePoint` segments in the parent ChannelGroup clock domain; voice start and note-off use a one-buffer scheduling lead, release begins continuously from the calculated future envelope level, and `setDelay` stops the channel at release end |
| Initial synth controls | FMOD oscillator waveform values map explicitly to sine, square, saw up, saw down, triangle, and noise. Octave is an integer from -4 through +4 and must keep the resolved oscillator rate within FMOD's 1–22000 Hz range; gain is 0–1. The resonant low-pass uses supported Multiband EQ band A at 24 dB/octave, with cutoff 20–22000 Hz and Q 0.1–10, instead of deprecated `DSP_TYPE.LOWPASS` |
| Initial polyphony | `FmodOscillatorInstrument` implements `IInstrument` with a preallocated fixed pool of eight reusable voice DSP graphs by default. Process-wide opaque handle IDs prevent cross-instrument ownership collisions, while a separate local monotonic start order makes oldest-voice stealing deterministic. `NoteOff` consumes only the exact owned handle, completed releases are reclaimed before stealing, velocity scales the configured per-voice gain, and instrument disposal releases every pooled graph |
| Studio bus routing | `FmodOscillatorInstrument` requires valid Core and Studio systems and routes every pooled voice through one locked `bus:/MUS_Synth` ChannelGroup. Locking is paired with checked `flushCommands` and `unlockChannelGroup`; `PrepareForBankReload` requires zero active voices and releases the lifecycle-bound handles, while `RestoreAfterBankReload` reacquires them only after the Master Bank is loaded |
| Computer keyboard input | `ComputerKeyboardNoteInput` polls Unity's legacy `Input` API, which is available because the project enables both input backends. Its fixed chromatic layout maps `Z S X D C V G B H N J M` to MIDI 60-71 and `Q 2 W 3 E R 5 T 6 Y 7 U` to MIDI 72-83; repeated key-downs are suppressed and each key-up consumes the exact retained `VoiceHandle`. `Panic` clears retained key state before calling `AllNotesOff`, so a failed native cleanup cannot leave a key logically stuck |
| Synth demo | `Assets/Scenes/scn_loom.unity` is the build-index-zero playground. A UI Toolkit document and `LoomSynthDemoController` own the eight-voice routed instrument, poll the keyboard adapter, display the two-octave layout, and apply waveform, ADSR, gain, octave, cutoff, and resonance changes at runtime |
| Demo lifecycle | Focus loss and application pause call the shared panic path without destroying the synth. Disable, destroy, application quit, and Editor assembly reload call one idempotent shutdown path that panics, disposes the instrument, and releases every pooled DSP and Studio bus handle |

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
| M0 | Repository rules and verified FMOD foundation | DONE |
| M1 | Playable polyphonic synth playground | DONE |
| M2 | Deterministic transport and step sequencer | READY |
| M3 | Multiple tracks, scale, harmony, and routing | NOT STARTED |
| M4 | Quantized mutation layer and game-facing API | NOT STARTED |
| M5 | Save/replay determinism and engine hardening | NOT STARTED |
| M6 | Authoring, MIDI, samples, and advanced synthesis | DEFERRED |

## M0 — Repository and FMOD Foundation

**Goal:** establish reliable project structure and prove that Unity, FMOD Core, FMOD Studio, and the mixer bus are connected.

### Work items

- [x] `DONE` Record agent workflow and architectural invariants in `AGENTS.md` and `CLAUDE.md`.
- [x] `DONE` Create this canonical implementation plan.
- [x] `DONE` Configure the Unity FMOD integration to use `fmod/loom/loom.fspro`.
- [x] `DONE` Create the Studio mixer bus `Master/MUS_Synth`.
- [x] `DONE` Build the Master Bank for desktop.
- [x] `DONE` Confirm Unity loads the bank and resolves `bus:/MUS_Synth`.
- [x] `DONE` Create the LOOM folder layout and assembly definitions.
- [x] `DONE` Confirm all assemblies compile with the intended dependency boundaries.

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
- Unity FMOD settings use repository-relative paths `fmod/loom/loom.fspro` and `fmod/loom/Build`.
- FMOD Studio contains the `Master/MUS_Synth` group bus and builds `Master.bank` plus `Master.strings.bank` under `fmod/loom/Build/Desktop`.
- In macOS Play Mode on 2026-08-01, `RuntimeManager.CoreSystem.getVersion` returned `FMOD_OK`, version `0x00020307`, build `150747`.
- In the same run, `RuntimeManager.StudioSystem.getBus("bus:/MUS_Synth")` returned `FMOD_OK`, the bus handle was valid, and no FMOD errors or warnings were present in the Unity Console.
- Exiting that Play Mode run produced no FMOD errors or warnings; two unrelated TextCore font-assignment errors remained in the existing project.
- `Assets/Scripts/Loom/` contains the planned `Loom.Core`, `Loom.Fmod`, `Loom.Unity`, `Loom.Demo`, `Loom.Tests.EditMode`, and `Loom.Tests.PlayMode` assemblies.
- `Loom.Core` uses `noEngineReferences: true`; `AssemblyBoundaryTests` verified that it references neither Unity nor FMOD.
- Unity Test Runner discovered and passed both EditMode assembly-boundary tests and the PlayMode runtime-assembly availability test on 2026-08-01 (3 passed, 0 failed, 0 skipped).

## M1 — Playable Polyphonic Synth Playground

**Goal:** play and shape notes interactively without sample assets.

### Work items

- [x] `DONE` Define `Note`, `NoteEvent`, `VoiceHandle`, and `IInstrument` contracts in Core.
- [x] `DONE` Implement MIDI-note-to-frequency conversion with tests.
- [x] `DONE` Implement one FMOD oscillator voice with explicit lifecycle and result checking.
- [x] `DONE` Add ADSR amplitude control using DSP-clock-aligned fade points or an equivalent verified FMOD graph.
- [x] `DONE` Add waveform selection, gain, octave, cutoff, and resonance controls supported by the initial graph.
- [x] `DONE` Add fixed-size polyphony and deterministic voice stealing.
- [x] `DONE` Route every voice through `bus:/MUS_Synth`.
- [x] `DONE` Add computer-keyboard note input.
- [x] `DONE` Add a demo UI and scene.
- [x] `DONE` Add shutdown, domain-reload, focus-loss, and `AllNotesOff` handling.

### Acceptance criteria

- At least eight simultaneous notes can sound without unmanaged-resource leaks.
- Key-down starts the correct pitch and key-up releases the same logical voice.
- ADSR, waveform, octave, gain, cutoff, and resonance can be changed from the demo where supported by the verified DSP graph.
- Audio is audible through `MUS_Synth`; muting that bus silences the synth.
- Voice stealing is repeatable and documented.
- Repeated Play Mode entry/exit produces no invalid-handle or leaked-DSP errors.
- The demo runs in the macOS Editor and has a documented Windows verification path.

### Desktop verification path

- macOS Editor: open `Assets/Scenes/scn_loom.unity`, enter Play Mode, click the Game view, play both documented keyboard rows, change every synth control while notes are active, mute `bus:/MUS_Synth`, and confirm the voice counter and Console remain healthy through exit.
- Windows: switch to a Windows x86_64 standalone target, confirm the Desktop FMOD banks are available, build with `scn_loom` at build index zero, run the player, repeat the key/control/bus-mute checks, then inspect the Player log after shutdown. This remains a hardware verification task rather than a macOS inference.

### Verification evidence

- `Note`, `NoteEvent`, and `VoiceHandle` are immutable value types in `Loom.Core`; constructor guards cover MIDI pitch, playable velocity, negative ticks, zero duration, interval overflow, and invalid zero voice IDs.
- `IInstrument` defines explicit `NoteOn`/`NoteOff` handle ownership, `AllNotesOff`, and `IDisposable` lifecycle semantics without Unity or FMOD dependencies.
- On 2026-08-01, the open Unity Editor discovered 14 focused contract test cases and passed the complete `Loom.Tests.EditMode` assembly: 16 passed, 0 failed, 0 skipped, including the two existing assembly-boundary tests.
- `Note.FrequencyHz` implements twelve-tone equal-temperament conversion as `440 * 2^((midi - 69) / 12)` in `double`, without adding mutable state to `Note`.
- Focused tests cover A0, A3, A4, middle C, MIDI endpoints 0 and 127, strict monotonicity across the complete MIDI range, and the 2:1 frequency ratio for every twelve-semitone interval.
- On 2026-08-01, `NoteContractTests` passed 18/18 cases and the complete `Loom.Tests.EditMode` assembly passed 24/24 tests after the final formatting pass.
- `FmodOscillatorVoice` creates a built-in oscillator DSP with sine as its default waveform, converts the Core frequency to FMOD `float`, starts the channel paused, applies normalized gain before unpausing, and exposes explicit `Start`, `Stop`, `Release`, and idempotent `Dispose` behavior.
- `FmodOperationException` preserves the failed operation, `FMOD.RESULT`, and native FMOD error text; construction/start cleanup failures surface both the primary and cleanup errors instead of ignoring a result.
- Five focused EditMode cases verify invalid-system rejection, gain validation, and contextual error reporting; the complete `Loom.Tests.EditMode` assembly passed 29/29 tests on 2026-08-01.
- The PlayMode oscillator test intentionally played an A4 sine at 440 Hz for 0.5 seconds, observed an active FMOD channel, stopped it, released the DSP, and repeated `Release` safely. The complete PlayMode assembly passed 2/2 tests with no FMOD errors or warnings after the final run.
- The PlayMode test creates a temporary non-persistent `StudioListener`; the production demo scene remains unchanged in this work item.
- `FmodAdsrEnvelope` validates attack, decay, sustain, and release; converts seconds to integer sample frames using the runtime FMOD sample rate; and exposes deterministic attack/decay/sustain level evaluation for scheduling and verification.
- `FmodOscillatorVoice.Start` schedules the attack, decay, and sustain fade points one DSP buffer ahead in the parent ChannelGroup clock domain. `BeginRelease` removes only future points, inserts the mathematically continuous release-start level, fades to zero over the configured release frames, and schedules the channel to stop at the release endpoint.
- Five focused ADSR EditMode tests passed, covering validation, runtime-rate frame conversion, positive sub-sample clamping, ADS level calculation, and the continuous zero-decay case. The complete `Loom.Tests.EditMode` assembly passed 35/35 tests on 2026-08-01.
- The PlayMode ADSR test released a live A4 oscillator during attack, verified the scheduled release-start level and exact release-frame interval, observed automatic channel completion, and safely released the DSP. The complete PlayMode assembly passed 2/2 tests with no FMOD errors or warnings on 2026-08-01.
- `FmodOscillatorSettings` validates all six documented FMOD oscillator waveforms, normalized gain, a practical -4 through +4 octave range, the resulting FMOD oscillator frequency, Multiband EQ cutoff, and resonance Q before native calls are made.
- The single-voice graph now owns a supported `MULTIBAND_EQ` DSP configured as band-A `LOWPASS_24DB`; the deprecated `LOWPASS` and `LOWPASS_SIMPLE` DSP types are not used. Live setters update waveform, gain, octave-derived oscillator rate, cutoff, and resonance only after the corresponding checked FMOD operation succeeds.
- Eleven new EditMode cases cover waveform-native-value mapping, defaults, null settings, invalid control values, octave resolution, and FMOD frequency limits. The complete `Loom.Tests.EditMode` assembly passed 46/46 tests on 2026-08-01.
- The PlayMode synth-control test created Saw Up at +1 octave with a 12 kHz/Q 1.5 filter, then changed the active voice to Square at -1 octave, gain 0.03, cutoff 2.4 kHz, and Q 3.0 before completing ADSR release. The complete PlayMode assembly passed 2/2 tests with no FMOD errors or warnings, and both owned DSP handles released cleanly.
- `FmodOscillatorInstrument` now provides the Core `IInstrument` contract through a preallocated eight-slot DSP pool, returns process-wide unique opaque handles, consumes exact handles on note-off, and selects the locally oldest occupied slot when all eight voices are in use. A ninth note invalidates, stops, retunes, and reuses the first slot without a managed allocation; completed ADSR releases are reclaimed before any active voice is stolen.
- Thirteen focused EditMode cases cover capacity and velocity validation, distinct cross-instrument ownership, stable note inputs, exact note-off, deterministic ninth-note stealing, completed-release reuse, `AllNotesOff`, idempotent disposal, and cleanup after a failed voice start. The complete `Loom.Tests.EditMode` assembly passed 59/59 tests on 2026-08-01.
- The live FMOD polyphony test played eight simultaneous notes from the preallocated pool, forced and verified reuse of the deterministic oldest slot for a ninth pitch, released targeted handles, ran `AllNotesOff`, observed all channels complete, and verified that instrument disposal released all eight graphs. The complete PlayMode assembly passed 3/3 tests with no FMOD errors or warnings on 2026-08-01.
- `FmodStudioBusRouting` now resolves `bus:/MUS_Synth`, locks and materializes its ChannelGroup with a checked Studio command flush, validates the cached lifecycle-bound group before each note start, and pairs every successful lock with an unlock during bank reload or disposal. The successful note-start path reuses the cached group without managed allocations.
- The routed polyphony PlayMode test observed all eight live Core channels in the same `MUS_Synth` ChannelGroup, set only that Studio bus to muted, and measured its ChannelGroup audibility at zero while all eight voices remained active.
- The bank lifecycle test rejected reload preparation while a voice was active, released the route after the voice completed, unloaded and reloaded the actual `Master` bank through `RuntimeManager`, restored the route, and played a new channel through the reacquired `MUS_Synth` group. The complete suites passed 60/60 EditMode and 4/4 PlayMode tests with no FMOD errors or warnings on 2026-08-01.
- `ComputerKeyboardNoteInput` adds an allocation-free 24-key polling loop in `Loom.Unity`, validates velocity, ignores unmapped and repeated key-downs, retains one stable handle per pressed key, clears stale pressed state even when a stolen voice rejects note-off, and rejects invalid handles returned by an instrument.
- Thirty focused EditMode cases verify every key-to-MIDI mapping entry, constructor guards, velocity forwarding, repeated key-down and key-up suppression, exact per-key handle release, unmapped-key isolation, stolen-voice recovery, and invalid-handle rejection. The complete suites passed 90/90 EditMode and 4/4 PlayMode tests on 2026-08-01; the console contained no LOOM or FMOD errors or warnings.
- `scn_loom` now contains a `LOOM Synth Demo` root with a `UIDocument` and `LoomSynthDemoController`; its responsive UI Toolkit layout presents oscillator, envelope, filter, routing/status, active-voice, and two-octave keyboard panels. The scene validates with zero missing scripts or broken prefabs and is enabled at build index zero.
- In the macOS Editor runtime smoke on 2026-08-01, the controller created all eight routed voices, resolved the styled UI at 1440x810, applied Triangle, gain 0.2, octave +1, cutoff 4.2 kHz, Q 1.8, and attack 0.08 seconds through live UI callbacks, and completed an actual C4 note-on/note-off using stable handle 1.
- The instrument control broadcast is covered by 15/15 focused EditMode cases. After the demo change, the complete suites passed 91/91 EditMode and 4/4 PlayMode tests with no failures or invalid-handle/leaked-DSP errors.
- `ComputerKeyboardNoteInput.Panic` clears all 24 retained handles before calling `AllNotesOff`, and `LoomSynthDemoController` routes focus loss, application pause, disable, destroy, application quit, and Editor assembly reload through panic or the idempotent shutdown path as appropriate.
- Thirty-two focused input cases passed, including repeated panic and cleanup-failure state recovery. `DemoPanicsAndShutsDownIdempotently` passed in Play Mode after exercising focus loss, pause, disable/re-enable, the assembly-reload callback, application quit, and repeated destruction while confirming each owned eight-voice pool released to zero created graphs.
- Three additional macOS Editor Play/Stop cycles each created eight routed graphs, started a real note with one owned handle, and exited without invalid-handle, leaked-DSP, or `SystemI::close` errors. The complete suites then passed 93/93 EditMode and 5/5 PlayMode tests on 2026-08-01.
- The long-lived Unity process still emits the FMOD Live Update port warning because its own PID 3112 retains port 9264; `lsof` confirmed no second process owns the port. FMOD falls back without Live Update, and this Editor-level warning produced no LOOM lifecycle or resource-cleanup failure.

## M2 — Deterministic Transport and Step Sequencer

**Goal:** schedule notes from integer musical time with no accumulating drift.

### Work items

- [ ] `READY` Implement `MusicalTime`, 4/4 meter, and 960 PPQN constants.
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

## Changelog

### 2026-08-01

- Created the canonical implementation plan.
- Narrowed the initial target to macOS and Windows desktop.
- Established engine-first scope: playable notes and a synth playground before sequencing and adaptive systems.
- Selected the built-in FMOD oscillator/DSP path for the first synth spike, with no sample dependency.
- Recorded the existing FMOD project at `fmod/loom/loom.fspro` and the unverified Unity project-link state.
- Added plan-maintenance and handoff rules for future agents.
- Configured Unity to read the repository-relative FMOD Studio project and its generated bank output.
- Added `Master/MUS_Synth`, built the Desktop Master Bank, and verified both FMOD Core access and Studio bus resolution in macOS Play Mode.
- Confirmed generated banks remain ignored build artifacts under `fmod/loom/Build/<platform>`.
- Verified a clean FMOD shutdown after leaving Play Mode and started the LOOM assembly scaffold work item.
- Created the full LOOM folder and assembly scaffold with explicit one-way dependencies.
- Added assembly-boundary and availability smoke tests; all two EditMode tests and one PlayMode test passed in the open Unity Editor.
- Completed M0 and advanced the current milestone to M1.
- Added immutable Core contracts for resolved MIDI notes, logical tick-based note events, stable voice handles, and disposable instrument ownership.
- Added focused Note and instrument contract tests; the complete EditMode assembly passed 16/16 tests in the open Unity Editor.
- Added double-precision MIDI-note-to-frequency conversion using A4 = 440 Hz and verified reference notes, endpoints, monotonicity, and octave ratios across the full MIDI range.
- Re-ran the complete EditMode assembly after the conversion change; all 24 tests passed.
- Added the first immediately playable FMOD Core sine-oscillator voice with explicit channel/DSP ownership, contextual result checking, bounded gain, and failure cleanup.
- Added focused EditMode and PlayMode verification; final suites passed 29/29 EditMode and 2/2 PlayMode, including a 0.5-second 440 Hz output and clean repeated release.
- Added a runtime-sample-rate ADSR model and DSP-clock-aligned amplitude scheduling to the oscillator voice, including continuous release from attack/decay/sustain and sample-accurate scheduled stop.
- Verified the ADSR work with five focused EditMode tests, the complete 35/35 EditMode suite, and the complete 2/2 PlayMode suite; the runtime release test produced no FMOD errors or warnings.
- Added validated live waveform, gain, octave, cutoff, and resonance controls to the single oscillator voice, using FMOD's supported Multiband EQ low-pass replacement and explicit ownership of both DSP handles.
- Verified the synth controls with eleven new EditMode cases, the complete 46/46 EditMode suite, and the complete 2/2 PlayMode suite; active parameter changes and final cleanup produced no FMOD errors or warnings.
- Added preallocated fixed-size eight-voice `IInstrument` polyphony with process-wide unique handles, exact note-off ownership, allocation-free successful note starts, deterministic local oldest-slot reuse, velocity-scaled gain, completed-release reuse, and explicit graph cleanup.
- Verified polyphony and resource ownership with thirteen focused contract cases, the complete 59/59 EditMode suite, and the complete 3/3 PlayMode suite; the live ninth-note steal and final disposal produced no FMOD errors or warnings.
- Routed every pooled oscillator voice through one persistent `MUS_Synth` Studio ChannelGroup with checked lock/flush/unlock ownership and explicit safe bank-reload boundaries.
- Verified eight-channel bus membership, zero audibility under `MUS_Synth` mute, active-voice reload rejection, real Master Bank unload/load and route restoration, the complete 60/60 EditMode suite, and the complete 4/4 PlayMode suite without FMOD errors or warnings.
- Added the fixed two-octave computer-keyboard adapter with repeated key-down suppression and exact per-key voice ownership; verified all 30 focused cases, the complete 90/90 EditMode suite, and the complete 4/4 PlayMode suite.
- Added the build-index-zero UI Toolkit synth playground in `scn_loom`, with live waveform, ADSR, gain, octave, cutoff, resonance, keyboard-layout, routing, and active-voice feedback wired to the routed eight-voice FMOD instrument.
- Verified the demo scene structure, resolved runtime styling and live callbacks, a real scene note-on/note-off, 15/15 focused instrument cases, 91/91 complete EditMode tests, and 4/4 complete PlayMode tests; advanced M1 to its final lifecycle/panic item.
- Added one idempotent demo shutdown path and a keyboard panic operation covering focus loss, application pause, disable/destroy, application quit, and Editor assembly reload while preserving fresh key-down behavior after focus returns.
- Verified lifecycle behavior with 32/32 focused input cases, the dedicated PlayMode lifecycle test, three real note-bearing Play/Stop cycles, 93/93 complete EditMode tests, and 5/5 complete PlayMode tests without invalid handles or leaked DSP errors.
- Completed M1 and advanced the current milestone to the ready M2 `MusicalTime` foundation item.
