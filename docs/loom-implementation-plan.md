# LOOM Implementation Plan

**Plan version:** 1.39
**Last updated:** 2026-08-02
**Current milestone:** M4 — Mutation layer
**Current status:** READY
**Next action:** Define versioned mutation verbs, targets, values, priorities, and sequence numbers without exposing sequencer internals to game code.

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
| Studio buses | `bus:/MUS_Synth`, `bus:/MUS_Drums`, `bus:/MUS_Bass`, `bus:/MUS_Lead`, `bus:/MUS_Pad`, and `bus:/MUS_FX`; every group is a direct child of the Master Bus |
| Musical clock | Integer `long` ticks, 960 PPQN |
| Initial meter | 4/4 |
| Musical-time decomposition | `MusicalTime` accepts ticks from zero through `long.MaxValue` and exposes zero-based `long` bar, `int` beat-within-bar, and `int` tick-within-beat values without reconstructive multiplication |
| Initial tempo | 120 BPM |
| Tempo map | `TempoSegment` stores a non-negative start tick and finite positive `double` beats per minute whose seconds-per-beat duration is representable. `TempoMap` owns a defensive copy of at least one strictly ordered segment beginning at tick zero, defaults to one 120 BPM segment, and uses allocation-free binary lookup |
| Tick-to-sample conversion | `TickSampleConverter` uses a positive runtime sample rate, sums absolute tempo regions from tick zero without an incremental playhead, rounds the final non-negative frame once with `MidpointRounding.AwayFromZero`, returns `ulong`, and throws on values at or beyond 2^64. Its allocation-free inverse returns the greatest tick on or before a frame, selecting the final tick of a rounding plateau and treating forward overflow as beyond the target |
| Tick-to-DSP-clock mapping | `FmodTickDspClockMapper` reads FMOD's runtime sample rate, captures one immutable tick/sample-frame/DSP-clock anchor with an optional future lead, and maps absolute ticks by checked unsigned addition or subtraction. Its inverse translates a supplied clock through that anchor without rereading native state and explicitly reports preroll before logical frame zero. Its clock source is the own DSP clock of the ChannelGroup that will parent scheduled channels; the mapper borrows that lifecycle-bound handle and must be recreated after device, bank, or routing changes |
| Intended sample rate | 48 kHz, verified at runtime rather than assumed |
| Scheduler lookahead | 200 ms initial tuning value |
| Determinism contract | Identical logical event stream for identical seed, state, and mutation log |
| Stateless randomness | `StatelessRng` hashes the ordered unsigned lanes seed, stable stream ID, non-negative absolute tick, and frozen nonzero `RandomSlot` through the documented unchecked SplitMix64 finalizer. Slot identifiers are serialized contracts that are never renumbered or reused; `Float01` maps the upper 24 bits exactly into `[0, 1)` without mutable PRNG state |
| Scheduling buffer | `SchedulingBuffer<T>` is a single-owner, fixed-capacity FIFO for value types. It allocates its array only at construction, never overwrites or drops unread values when full, preserves insertion order through wraparound, clears dequeued slots, and exposes only non-enumerating enqueue, peek, dequeue, and clear operations on the hot path |
| Step-pattern data | `PatternStep` is an immutable resolved `Note` or canonical default rest with playable velocity, positive tick duration, finite normalized probability, one through 255 total ratchet attacks, and an integer microtiming offset. `StepPattern` owns a non-empty defensive copy on a positive divisor of 960 PPQN, bounds note offsets inside one step, and requires no more ratchets than distinct ticks in that step |
| Track definitions | `TrackId` and `PatternId` are separate nonzero stable unsigned identities. Immutable `TrackDefinition` owns one resolved-note `StepPattern`, derives its length directly from that pattern, and creates sequencers whose stateless-random stream is the track ID. Pattern identity does not alter track randomness |
| Scheduled FMOD voices | The interactive `IInstrument` API remains unchanged. `FmodOscillatorInstrument.ScheduleNote` owns future voices separately and uses hard `Channel.stop` cancellation rather than the interactive release path. A scheduled voice validates the routed parent clock immediately before native start; if submission consumed the remaining lead, it shifts start and release forward together, preserving gate duration and reporting the adjustment to the dispatcher |
| Scheduled dispatch | `FmodScheduledNoteDispatcher` reads the current parent clock once per batch, reserves two DSP buffers of initial lead so one remains available during ordinary submission work, preserves mapped gate duration when shifting late events, promotes rounding-plateau gates to one sample frame, and dequeues only after atomic instrument success. Dispatcher and final native-lead adjustments count once as late timing evidence, separately from plateau corrections and the deterministic logical stream |
| Core transport | `Transport` owns one preallocated event buffer and a `StepSequencer`, accepts monotonic audio-derived current and target ticks, completes an older partial target before a newer coalesced horizon, and reports underrun separately from buffer backpressure. Pause and panic discard stale lookahead and restart generation at an explicit held/current tick; stop returns the deterministic sequence domain to zero |
| Runtime scheduler | `FmodTransportScheduler` borrows the routed instrument, drives Core time from one routed-bus DSP-clock snapshot per pump, uses an exact 200 ms integer-frame horizon and full preroll anchor, bounds dispatch work, and creates a new mapper for start/resume/playing panic. A regressed bus clock hard-cancels voices from the stale domain, resets pending generation at the last accepted logical tick, and reanchors without moving Core time backward; unrelated dispatch failures still stop fail-closed |
| Core boundary | Pure C#, with no Unity or FMOD references |
| Resolved pitch contract | `ScaleDegreePitch` stores a signed zero-based degree plus an additional octave offset. Immutable `Scale` owns one strictly increasing octave of unique 0-11 semitone intervals beginning at zero and resolves against an absolute tonic with floor wrapping in `long` arithmetic. Results outside MIDI 0-127 are rejected; the existing pattern, sequencer, and `NoteEvent` boundary remains resolved `Note` data |
| Percussion note contract | `PercussionNote` is a separate immutable value identified by a nonzero instrument-local `ulong` sample-slot ID. It has no MIDI pitch, scale degree, octave, asset path, Unity, or FMOD dependency; the assigned percussion backend owns slot-to-resource mapping |
| Percussion pattern pipeline | `PercussionPatternStep`, `PercussionStepPattern`, `PercussionNoteEvent`, and `PercussionStepSequencer` retain nonzero sample-slot identity through deterministic timing without MIDI, scale, harmony, Unity, or FMOD conversion |
| Harmony resolution | Immutable positive-duration `HarmonyStep` values form one defensively owned cyclic `HarmonyPlan`. `HarmonyResolver` keeps leads free in the scale, maps bass to the active root, and maps signed pad degrees through root-third-fifth diatonic tones using floor wrapping. `TrackRole` contains only pitched roles; percussion never enters this resolver |
| Pitched pattern pipeline | `PitchedPatternStep` and `PitchedStepPattern` retain unresolved scale-degree pitches. `PitchedStepSequencer` applies the selected `HarmonyResolver` policy at the final onset tick after microtiming and ratchets, then emits the existing resolved `ScheduledNoteEvent` boundary |
| Track bus paths | `FmodTrackBusPaths` is the shared code contract for every authored Studio track bus. Generated Desktop banks remain ignored build artifacts; committed FMOD metadata is the source configuration |
| Track mixer controls | `FmodTrackMixerControls` owns one selected Studio bus fader, mute state, and runtime Multiband EQ 24 dB/octave low-pass. It restores preexisting bus state on dispose or reload preparation, recreates its owned DSP after routing restoration, and never mutates another bus |
| Multi-track ordering | `FourTrackSequencer` owns bounded per-track buffers and merges drums, bass, lead, and pad by `(StartTick, TrackId.Value, TrackSequenceNumber)`. The merge is partition-invariant, backpressure-safe, resettable, and allocation-free after initialization |
| Four-track demo | The disabled-by-default `LOOM Four Track Demo` object in `Assets/Scenes/scn_loom.unity` runs drums, bass, lead, and pad in fixed pump order on direct track buses with independent 3/4/5/7-beat resolved-note patterns. Drums use an oscillator/noise rendering placeholder; explicit `PercussionNote` slots remain the canonical Core data contract until a sample backend is selected |
| Scheduled voice cleanup | Per-voice filters are detached explicitly before a scheduled or hard channel stop. Scheduled delays keep the channel alive through the silent release boundary so cleanup never loses the handle while its DSP remains connected |
| Pitch-to-frequency conversion | `Note.FrequencyHz` returns `double` using twelve-tone equal temperament and A4 = MIDI 69 = 440 Hz; conversion to FMOD `float` occurs explicitly at the adapter boundary |
| Logical note event | `NoteEvent` stores a playable velocity from 1 through 127 and a non-overflowing `[StartTick, EndTick)` interval in `long` ticks |
| Voice ownership | `IInstrument.NoteOn` returns a non-zero stable `VoiceHandle`; `NoteOff` consumes that exact handle, `AllNotesOff` invalidates outstanding handles, and the instrument owns disposable resources |
| Initial oscillator voice | `FmodOscillatorVoice` owns one configurable `DSP_TYPE.OSCILLATOR` plus one `DSP_TYPE.MULTIBAND_EQ` low-pass, starts its channel paused before applying gain and filter, checks every `FMOD.RESULT`, accepts an explicit target `ChannelGroup`, and stops the channel before releasing both DSPs; its parameterless `Start` remains a focused standalone Core-output path |
| Initial ADSR envelope | `FmodAdsrEnvelope` stores validated seconds and a normalized sustain level, resolves durations against FMOD's runtime sample rate, and schedules linear `addFadePoint` segments in the parent ChannelGroup clock domain; voice start and note-off use a one-buffer scheduling lead, release begins continuously from the calculated future envelope level, and `setDelay` stops the channel at release end |
| Initial synth controls | FMOD oscillator waveform values map explicitly to sine, square, saw up, saw down, triangle, and noise. Octave is an integer from -4 through +4 and must keep the resolved oscillator rate within FMOD's 1–22000 Hz range; gain is 0–1. The resonant low-pass uses supported Multiband EQ band A at 24 dB/octave, with cutoff 20–22000 Hz and Q 0.1–10, instead of deprecated `DSP_TYPE.LOWPASS` |
| Initial polyphony | `FmodOscillatorInstrument` implements `IInstrument` with a preallocated fixed pool of eight reusable voice DSP graphs by default. Process-wide opaque handle IDs prevent cross-instrument ownership collisions, while a separate local monotonic start order makes oldest-voice stealing deterministic. `NoteOff` consumes only the exact owned handle, completed releases are reclaimed before stealing, velocity scales the configured per-voice gain, and instrument disposal releases every pooled graph |
| Studio bus routing | `FmodOscillatorInstrument` requires valid Core and Studio systems and routes every pooled voice through one explicitly declared, locked Studio `ChannelGroup`; the default remains `bus:/MUS_Synth`. Locking is paired with checked `flushCommands` and `unlockChannelGroup`; `PrepareForBankReload` requires zero active voices and releases lifecycle-bound handles, while `RestoreAfterBankReload` reacquires them only after the Master Bank is loaded |
| Computer keyboard input | `ComputerKeyboardNoteInput` polls Unity's legacy `Input` API, which is available because the project enables both input backends. Its fixed chromatic layout maps `Z S X D C V G B H N J M` to MIDI 60-71 and `Q 2 W 3 E R 5 T 6 Y 7 U` to MIDI 72-83; repeated key-downs are suppressed and each key-up consumes the exact retained `VoiceHandle`. `Panic` clears retained key state before calling `AllNotesOff`, so a failed native cleanup cannot leave a key logically stuck |
| Synth demo | `Assets/Scenes/scn_loom.unity` is the build-index-zero playground. A UI Toolkit document and `LoomSynthDemoController` own the eight-voice routed instrument, poll the keyboard adapter, display the two-octave layout, and apply waveform, ADSR, gain, octave, cutoff, and resonance changes at runtime |
| Demo lifecycle | Focus loss and application pause call the shared panic path without destroying the synth. Disable, destroy, application quit, and Editor assembly reload call one idempotent shutdown path that panics, disposes the instrument, and releases every pooled DSP and Studio bus handle |
| Coding standard | `docs/coding-standards.md` is the canonical first-party LOOM coding, structure, and naming standard. `.editorconfig` supplies scoped IDE/Roslyn rules; `scripts/check_coding_standards.py` provides dependency-free deterministic checks; GitHub Actions runs the same checker on pushes and pull requests |

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
| M2 | Deterministic transport and step sequencer | DONE |
| M3 | Multiple tracks, scale, harmony, and routing | DONE |
| M4 | Quantized mutation layer and game-facing API | READY |
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
- [x] `DONE` Formalize and automatically verify the first-party LOOM coding standard.

### Acceptance criteria

- Unity reports no FMOD initialization or version mismatch errors.
- The FMOD project path stored in Unity is non-empty and points to `fmod/loom/loom.fspro` or a stable repository-relative equivalent.
- Desktop banks build successfully.
- Runtime code can acquire the Core system and the `MUS_Synth` Studio bus channel group.
- `Loom.Core` compiles without Unity or FMOD references.
- EditMode and PlayMode test assemblies are discoverable.
- First-party coding, structure, and naming rules are documented and pass the repository checker.

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
- `docs/coding-standards.md`, the scoped `.editorconfig`, the dependency-free checker, and the GitHub Actions workflow formalize and enforce first-party LOOM conventions without reformatting imported packages.
- On 2026-08-01, `python3 scripts/check_coding_standards.py` passed all 38 checked text files plus Unity metadata parity; `git diff --check` also passed.
- The open Unity 6000.3.10f1 Editor imported the two line-wrap-only C# changes, completed compilation, and reported zero Console errors.

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

- [x] `DONE` Implement `MusicalTime`, 4/4 meter, and 960 PPQN constants.
- [x] `DONE` Implement immutable tempo-map segments, initially one 120 BPM segment.
- [x] `DONE` Implement tick-to-sample/DSP-clock conversion with explicit rounding rules.
- [x] `DONE` Implement stateless hashed RNG and named random slots.
- [x] `DONE` Implement a bounded, allocation-free scheduling buffer.
- [x] `DONE` Implement one step pattern with rests, velocity, duration, probability, ratchets, and microtiming tick offsets.
- [x] `DONE` Add transport start, stop, pause, resume, and panic behavior.
- [x] `DONE` Integrate the Core transport with a Unity/FMOD 200 ms runtime scheduling loop.
- [x] `DONE` Add a ten-minute timing/drift measurement.

### Acceptance criteria

- Boundary tests cover beats, bars, negative/invalid inputs, long sessions, and conversion rounding.
- Repeated scheduling windows emit every logical event exactly once.
- The same seed and pattern produce the same serialized event stream.
- Scheduler steady-state allocations are zero in the measured hot path.
- The ten-minute test shows no accumulating musical-clock drift; reported onset jitter is measured separately.

### Verification evidence

- `MusicalTime` is an immutable Core value with a non-negative `long` tick contract, fixed 960 PPQN and 4/4 constants, and zero-based bar/beat/tick decomposition that remains safe at `long.MaxValue`.
- On 2026-08-02, all 18 focused `MusicalTimeTests` and the complete 111/111 `Loom.Tests.EditMode` cases passed in the open Unity Editor. The repository coding-standard checker and `git diff --check` also passed; the final Console contained no LOOM, compilation, or test-cleanup errors.
- `TempoSegment` validates non-negative ticks and finite positive tempo. `TempoMap` rejects missing, invalid, duplicate, and unordered segments; owns a defensive array copy; and resolves half-open tempo regions with allocation-free binary search through `long.MaxValue`.
- On 2026-08-02, all 38 focused tempo-map cases and the complete 149/149 `Loom.Tests.EditMode` cases passed in the open Unity Editor.
- `TickSampleConverter` converts from absolute musical ticks through every applicable tempo region, rounds only the final sample-frame position, validates runtime sample rate, and reports `UInt64` overflow explicitly without referencing Unity or FMOD.
- On 2026-08-02, all 27 focused conversion cases and the complete 176/176 `Loom.Tests.EditMode` cases passed in the open Unity Editor. Coverage includes 44.1 and 48 kHz, tempo boundaries, midpoint rounding, a ten-minute no-drift result, and representable and overflowing `long.MaxValue` conversions.
- `FmodTickDspClockMapper` captures FMOD's runtime sample rate and the own clock of the ChannelGroup that parents scheduled channels, retains one stable affine anchor, exposes a checked current-clock read in the same domain, and reports native failures with operation context. It borrows rather than releases FMOD handles and documents recreation after lifecycle changes.
- On 2026-08-02, all 21 focused mapper cases, the complete 197/197 `Loom.Tests.EditMode` cases, the focused live `MUS_Synth` clock-domain smoke, and the complete 6/6 `Loom.Tests.PlayMode` cases passed in the open Unity Editor. The repository coding-standard checker and `git diff --check` also passed.
- `StatelessRng` implements the design-document SplitMix64 lane fold without mutable state, validates non-negative ticks and frozen named slots without reflection, and converts the upper 24 hash bits into a deterministic single-precision `[0, 1)` value. Stable stream IDs and slot values are explicit serialized inputs rather than runtime object or string hashes.
- On 2026-08-02, all 17 focused RNG cases and the complete 214/214 `Loom.Tests.EditMode` cases passed in the open Unity Editor. Coverage includes frozen golden vectors, every input lane, slot identifiers, invalid inputs, evaluation-order independence, full integer boundaries, and exact unit-float endpoints.
- `SchedulingBuffer<T>` preallocates bounded value-type storage, provides FIFO enqueue/peek/dequeue/clear operations without enumeration or synchronization, returns explicit backpressure when full without mutation, and preserves order through repeated ring-index wraparound.
- On 2026-08-02, all 13 focused scheduling-buffer cases and the complete 227/227 `Loom.Tests.EditMode` cases passed in the open Unity Editor. A warmed 10,000-operation enqueue/dequeue loop measured zero current-thread managed allocations.
- `PatternStep` distinguishes a canonical rest from playable MIDI note zero, validates every note-level field, and normalizes zero probability. `StepPattern` owns its steps, derives integer grid and pattern lengths from 960 PPQN, bounds microtiming to one step, and guarantees distinct integer ratchet onsets.
- On 2026-08-02, all 38 focused pattern-contract cases and the complete 265/265 `Loom.Tests.EditMode` cases passed in the open Unity Editor. Coverage includes ownership, equality, every field boundary, representative grid divisors, microtiming edges, uneven ratchets, rests, and index bounds.
- `StepSequencer` precompiles stable final-onset templates, evaluates one named stateless probability roll per source step, emits immutable sequenced note events through half-open windows, preserves deterministic same-onset ordering, skips negative preroll onsets, and resumes without loss or duplication after fixed-buffer backpressure.
- On 2026-08-02, all 22 new sequencer/event cases and the complete 287/287 `Loom.Tests.EditMode` cases passed in the open Unity Editor. Coverage includes rests, probability boundaries and a frozen stream, uneven ratchets, cross-cycle microtiming, same-onset ordering, partition invariance, backpressure, reset replay, overflow, and zero steady-state allocations.
- `TickSampleConverter` now projects a sample frame to the final qualifying tick with an exception-free upper-bound search, including rounding plateaus and the last representable tick below forward overflow. `FmodTickDspClockMapper` performs the same inverse through its immutable affine anchor without another native clock read and exposes non-throwing preroll detection.
- On 2026-08-02, all 74 focused converter/mapper cases, the complete 313/313 `Loom.Tests.EditMode` cases, the focused live mapper smoke, and the complete 6/6 `Loom.Tests.PlayMode` cases passed in the open Unity Editor. A warmed 10,000-call inverse-conversion loop measured zero current-thread managed allocations.
- `FmodOscillatorVoice` now validates one-buffer lead before creating an exact-clock channel, sets one start/stop delay, truncates ADSR points at the resolved release, and supports hard cancellation before onset. The instrument distinguishes scheduled ownership from keyboard notes, while `FmodScheduledNoteDispatcher` clamps late batches without shortening gates and retains a failed head for retry.
- On 2026-08-02, all 30 focused instrument/dispatcher cases, the complete 328/328 `Loom.Tests.EditMode` cases, the exact-clock live voice test, and the complete 7/7 `Loom.Tests.PlayMode` cases passed in the open Unity Editor. A warmed 10,000-event dispatcher loop measured zero current-thread managed allocations.
- `StepSequencer.Reset(long)` seeks by final microtimed onset and starts a new deterministic sequence domain without allocation. Core `Transport` implements strict lifecycle states, explicit playhead restart on pause/panic, monotonic lookahead coalescing across buffer backpressure, separate underrun flags, and owned peek/dequeue access.
- On 2026-08-02, all 23 focused sequencer cases, all 10 focused transport cases, and the complete 342/342 `Loom.Tests.EditMode` cases passed in the open Unity Editor. A warmed 10,000-cycle transport start/update/dequeue/stop loop measured zero current-thread managed allocations.
- `FmodTransportScheduler` now anchors each playing session with the full 200 ms preroll, derives current and horizon ticks from one synth-bus clock snapshot, pumps a bounded dispatcher batch, tracks underrun/backpressure/late/plateau telemetry, and cancels or panics scheduled ownership across every lifecycle transition.
- On 2026-08-02, all 8 focused dispatcher cases, the live 200 ms scheduler lifecycle smoke, the complete 343/343 `Loom.Tests.EditMode` cases, and the complete 8/8 `Loom.Tests.PlayMode` cases passed in the open Unity Editor. The live smoke verified exact runtime-rate lookahead, routed future ownership, pause/resume reanchoring, panic, stop, and zero retained scheduled voices.
- `TenMinuteTransportHasNoDriftCadenceDependencyOrAllocations` accelerates the complete 600-second default-tempo timeline through integer sample-frame updates, including deterministic 150 ms cadence spikes inside the 200 ms horizon, and compares every event against independent closed-form tick and frame oracles.
- On 2026-08-02, the ten-minute measurement emitted exactly 1,200 ordered quarter-note events through tick 1,151,040, with a start-tick sum of 690,624,000, no missing or duplicate events, zero tick drift, zero planned-frame drift, zero final-frame error at frame 28,800,000, zero underrun/backpressure reports, cadence-identical checksums, and zero current-thread hot-path allocations. Dispatcher late adjustments remained separately reported by runtime telemetry; actual hardware/output onset jitter was not measured and is not claimed.

## M3 — Tracks, Scale, Harmony, and Mixer

**Goal:** coordinate multiple sequencers into a harmonically resolved multi-track engine.

### Work items

- [x] `DONE` Add track definitions and independent pattern lengths.
- [x] `DONE` Add scale-degree resolution for pitched tracks.
- [x] `DONE` Add a separate percussion/sample-slot note model rather than routing drums through scale degrees.
- [x] `DONE` Add harmony-plan and track-role resolution.
- [x] `DONE` Add `MUS_Drums`, `MUS_Bass`, `MUS_Lead`, `MUS_Pad`, and `MUS_FX` buses when required.
- [x] `DONE` Add a harmony-aware pitched pattern-to-event pipeline.
- [x] `DONE` Add an explicit percussion-slot pattern-to-event pipeline.
- [x] `DONE` Add deterministic four-track coordination and canonical same-tick ordering.
- [x] `DONE` Add explicit per-track FMOD instrument routing.
- [x] `DONE` Add track gain, mute, and basic effect parameters.
- [x] `DONE` Demonstrate independent pattern lengths and stable scheduling order.
- [x] `DONE` Recover from runtime bus DSP-clock regression without disabling playback.
- [x] `DONE` Preserve native scheduling lead across per-event FMOD submission work.

### Acceptance criteria

- [x] Four tracks run concurrently with independent lengths.
- [x] Pitched tracks resolve deterministically through scale and harmony data.
- [x] Percussion uses explicit instrument slots.
- [x] Mixer controls affect only their intended tracks.
- [x] Same-tick event ordering is stable and tested.
- [x] A regressed runtime bus DSP clock reanchors at the last accepted logical tick without disabling playback.
- [x] Native submission preserves gate duration and playback when its clock advances beyond the dispatch snapshot.

### Verification evidence

- `TrackId` and `PatternId` provide separate immutable nonzero value contracts, while `TrackDefinition` owns one immutable resolved-note pattern and exposes only its derived tick length.
- `TrackDefinition.CreateSequencer` permanently maps track identity to the existing stateless-random stream input; pattern identity remains available for later mutation/save targeting without perturbing that stream.
- Focused EditMode coverage verified invalid identities and missing patterns, value semantics, defensive pattern ownership, deterministic replay, distinct track RNG domains, and independent 12-step/16-step cycle lengths that realign after 48 steps.
- On 2026-08-02, the focused track-contract set passed 9/9 and the complete `Loom.Tests.EditMode` assembly passed 353/353 in the open Unity Editor. The repository checker passed 73 text files plus Unity metadata parity, and `git diff --check` passed.
- Unity compilation completed with no C# or LOOM errors. The post-test Console contained only two Test Runner API result-save messages classified as exceptions despite both jobs reporting `Passed`.
- `ScaleDegreePitch` provides signed zero-based degree and octave-offset value semantics. `Scale` defensively owns a validated one-octave interval set and resolves positive, wrapped, and negative degrees against an absolute tonic without mutable state.
- Resolution uses floor division and modulo semantics with `long` intermediate arithmetic, accepts exact MIDI boundary results, and reports controlled out-of-range failures even for extreme `int` degree and octave inputs.
- Existing `PatternStep`, `StepSequencer`, and `NoteEvent` contracts remain resolved-note boundaries. Focused integration coverage verifies that a scale-resolved note passes through that pipeline unchanged, and a warmed 10,000-call resolution loop allocates zero managed bytes.
- On 2026-08-02, the focused scale-contract set passed 30/30 and the complete `Loom.Tests.EditMode` assembly passed 383/383 in the open Unity Editor. The repository checker passed 77 text files plus Unity metadata parity, and `git diff --check` passed. The post-test Console contained no C# or LOOM errors, only four Test Runner API result-save messages from the current and preceding test jobs.
- `PercussionNote` provides an explicit nonzero instrument-local sample-slot identity whose full `ulong` range and value semantics are independent from the MIDI note range. Default remains an invalid sentinel rather than a playable slot.
- The bounded percussion model adds no scale conversion, sample path, FMOD mapping, pattern event, sequencer, or dispatcher behavior. Those integrations remain deferred until their ordering, backend, and ownership contracts are selected explicitly.
- On 2026-08-02, the focused percussion-note set passed 4/4 and the complete `Loom.Tests.EditMode` assembly passed 387/387 in the open Unity Editor. The repository checker passed 79 text files plus Unity metadata parity, and `git diff --check` passed. The post-test Console contained no C# or LOOM errors, only six Test Runner API result-save messages from successful jobs.
- `HarmonyStep` and `HarmonyPlan` provide an immutable cyclic chord-root timeline with positive durations, checked total length, defensive ownership, half-open boundaries, binary lookup, and deterministic `long.MaxValue` behavior.
- `HarmonyResolver` preserves free scale degrees for lead, maps bass to the active root, and maps signed pad degrees through a diatonic root-third-fifth triad. It rejects undefined roles, leaves `PercussionNote` structurally separate, and emits the existing resolved `Note` boundary without mutable state.
- On 2026-08-02, the focused harmony set passed 39/39 and the complete `Loom.Tests.EditMode` assembly passed 426/426 in the open Unity Editor. A warmed 10,000-call resolver loop allocated zero managed bytes. The repository checker passed 86 text files plus Unity metadata parity, and `git diff --check` passed; the Console contained no C# or LOOM errors, only Test Runner result-save messages.
- FMOD Studio now owns five direct Master Bus children for drums, bass, lead, pad, and effects alongside the existing synth bus. The Desktop Master Bank was rebuilt from the saved Studio project on 2026-08-02.
- `FmodTrackBusPaths` freezes the six authored Studio paths as one shared adapter contract, and the existing synth instrument aliases that contract rather than duplicating its literal path.
- The focused live bus test resolved and materialized valid ChannelGroups for all five new paths. The complete suites passed 426/426 EditMode and 9/9 PlayMode in the open Unity Editor; the repository coding-standard checker passed 88 text files plus Unity metadata parity.
- `PitchedPatternStep` and `PitchedStepPattern` preserve unresolved scale-degree intent with the same validated probability, ratchet, duration, and microtiming contract as the resolved-note pattern path.
- `PitchedStepSequencer` resolves each final onset through the selected bass, lead, or pad policy and retains deterministic half-open windows, stateless probability, backpressure replay, reset, overflow handling, and zero warmed allocations.
- On 2026-08-02, all 32 focused pitched-pipeline EditMode cases passed in the open Unity Editor. The combined pitched/percussion focused run also passed 102/102 while both explicitly parallelized slices were present.
- The percussion pipeline carries explicit `PercussionNote` sample-slot identities through immutable events and deterministic scheduling while preserving the resolved-note pipeline's timing, RNG, ratchet, microtiming, reset, backpressure, and overflow semantics.
- On 2026-08-02, all 70 focused percussion-pipeline EditMode cases passed in the open Unity Editor, including zero-allocation warmed scheduling and full-width slot identity coverage.
- `MultiTrackScheduledEvent` keeps pitched and percussion payloads structurally typed while exposing shared ordering keys. `FourTrackSequencer` coordinates exactly drums, bass, lead, and pad through bounded owned buffers.
- Canonical merge ordering is `(StartTick, TrackId.Value, TrackSequenceNumber)` and does not depend on registration order or unordered collections. Focused coverage proves independent three-, four-, five-, and seven-beat pattern cycles, partition invariance, exact backpressure replay, reset replay, and zero warmed allocations.
- On 2026-08-02, all 9 focused four-track coordinator EditMode cases passed in the open Unity Editor.
- `FmodOscillatorInstrument` now accepts an explicit authored bus path while retaining `MUS_Synth` as the source-compatible default. Every pooled voice borrows the routing object's actual parent group, so scheduled clocks remain in the correct bus domain.
- On 2026-08-02, all 23 focused instrument-contract EditMode cases passed, and a focused live PlayMode case created, played, released, and disposed an oscillator instrument bound to `bus:/MUS_Lead`.
- `FmodTrackMixerControls` provides validated linear gain, mute, and 20–22000 Hz low-pass cutoff for one declared Studio bus. It owns only its runtime EQ DSP, preserves the original bus state, and follows explicit prepare/restore/dispose lifecycle rules.
- On 2026-08-02, all 12 focused mixer-control EditMode cases and 2/2 focused live PlayMode cases passed. Live coverage verified state round trips, lifecycle restoration, and that lead controls leave the bass bus unchanged.
- `LoomFourTrackDemoController` creates four oscillator instruments on `MUS_Drums`, `MUS_Bass`, `MUS_Lead`, and `MUS_Pad`, then pumps their schedulers in fixed drums/bass/lead/pad order. The scene stores the demo object disabled by default so explicit activation cannot interfere with the interactive synth or bank-reload lifecycle tests.
- The live demo uses independent 3/4/5/7-beat patterns and dispatched at least one event on every declared bus during the focused PlayMode smoke. The separate Core coordinator remains the canonical source of cross-track same-tick ordering.
- Four-track cleanup exposed and fixed a scheduled-voice lifetime defect: the channel could auto-stop before its attached low-pass DSP was removed. Delayed stops now retain the channel through the release boundary, detach the filter explicitly, and then stop and release cleanly.
- Final M3 verification on 2026-08-02 passed 549/549 `Loom.Tests.EditMode` cases and 13/13 `Loom.Tests.PlayMode` cases in the open Unity Editor. The repository checker passed 114 text files plus Unity metadata parity, `git diff --check` passed, and the final Console contained no C#, LOOM, or FMOD errors.
- `FmodTransportScheduler` now detects a raw routed-bus DSP clock below its last observation before projecting Core time. Recovery cancels voices scheduled in the stale domain, resets pending deterministic generation at the unchanged accepted tick, recreates the mapper and dispatcher with fresh preroll, and reports a cumulative recovery count.
- The focused live clock-regression case passed 1/1, then the complete open-Editor suites passed 549/549 EditMode and 14/14 PlayMode cases. The four-track demo smoke remained green, the repository checker passed 114 text files plus Unity metadata parity, and `git diff --check` passed; the Console contained no C#, LOOM, or FMOD errors.
- The dispatcher now reserves two native DSP buffers from its batch snapshot. `FmodOscillatorVoice` performs a final fresh-clock validation and moves a stale start/release interval together instead of throwing, while the instrument reports that adjustment so late-event telemetry remains exact without double-counting.
- Focused final-lead coverage passed 1/1 EditMode and 1/1 PlayMode, including a deliberately one-buffer-stale snapshot that reproduced the original exception before the final-boundary correction. Complete open-Editor suites passed 550/550 EditMode and 15/15 PlayMode cases; the active demo was temporarily disabled only for the isolated bus-ownership suite.
- A separate live smoke kept `LOOM Four Track Demo` active, maximized the Game view, and toggled normal-to-maximized while playing. All four schedulers remained active, dispatch counters advanced from `28/21/22/12` to `87/65/65/38`, and the Console reported zero errors after the transition.

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

### 2026-08-02

- Added immutable, fixed-meter `MusicalTime` to `Loom.Core`, including explicit 960 PPQN and 4/4 constants, non-negative absolute ticks, and zero-based decomposition safe through `long.MaxValue`.
- Added 18 focused EditMode boundary and value-semantics cases; the focused suite passed 18/18 and the complete EditMode assembly passed 111/111 in the open Unity Editor.
- Completed the first M2 work item and advanced the next action to immutable tempo-map segment data with a 120 BPM default.
- Added immutable validated `TempoSegment` values and an owned, strictly ordered `TempoMap` with a single-segment 120 BPM default and allocation-free binary lookup.
- Added 38 focused tempo-map validation, ownership, ordering, and lookup cases; the focused suite passed 38/38 and the complete EditMode assembly passed 149/149 in the open Unity Editor.
- Completed the second M2 work item and advanced the next action to explicit tempo-aware tick-to-sample-frame conversion.
- Added Core `TickSampleConverter` with validated runtime sample rate, absolute multi-segment accumulation, one explicit midpoint-away rounding step, and checked `UInt64` range handling.
- Added 27 focused conversion cases; the focused suite passed 27/27 and the complete EditMode assembly passed 176/176 in the open Unity Editor.
- Advanced the active timing item to the FMOD DSP-clock anchoring adapter while leaving scheduler behavior out of this bounded change.
- Added `FmodTickDspClockMapper` with runtime sample-rate discovery, one immutable tick/sample/DSP anchor, optional scheduling lead, checked forward and backward mapping, contextual FMOD result handling, and explicit borrowed-handle lifecycle rules.
- Added 21 focused mapper cases and a live `MUS_Synth` PlayMode clock-domain smoke; the complete suites passed 197/197 EditMode and 6/6 PlayMode in the open Unity Editor.
- Completed the tick-to-sample/DSP-clock work item and advanced M2 to the stateless hashed RNG and named-slot contract.
- Added Core `StatelessRng` and frozen `RandomSlot` identifiers using the design-document SplitMix64 finalizer, explicit unsigned input lanes, and an exact upper-24-bit unit-float conversion.
- Added 17 focused RNG cases; the focused suite passed 17/17 and the complete EditMode assembly passed 214/214 in the open Unity Editor.
- Completed the stateless RNG work item and advanced M2 to the fixed-capacity scheduling buffer.
- Added generic value-type `SchedulingBuffer<T>` with fixed owned storage, explicit full-buffer backpressure, FIFO peek/dequeue semantics, wraparound-safe indices, and reusable clear behavior.
- Added 13 focused buffer cases; the focused suite passed 13/13 and the complete EditMode assembly passed 227/227, including a zero-allocation hot-path measurement.
- Completed the bounded scheduling-buffer work item and advanced M2 to immutable step-pattern contracts.
- Added immutable `PatternStep` and defensively owned `StepPattern` data for resolved notes, canonical rests, 960-PPQN subdivisions, velocity, duration, probability, ratchets, and bounded microtiming.
- Added 38 focused pattern-contract cases; the focused suite passed 38/38 and the complete EditMode assembly passed 265/265 in the open Unity Editor.
- Advanced the active step-pattern item to deterministic half-open window generation and backpressure recovery.
- Added immutable `ScheduledNoteEvent` values and an allocation-free `StepSequencer` with precompiled onset ordering, stateless per-step probability, ratchet and microtiming cycle normalization, exact half-open windowing, and resumable bounded-buffer backpressure.
- Added 22 focused sequencer/event cases; the complete EditMode assembly passed 287/287, including deterministic partition/backpressure replay and zero warmed hot-path allocations.
- Completed the step-pattern work item and advanced the transport item to inverse audio-clock projection required for deterministic lookahead windows.
- Added allocation-free inverse sample-frame and DSP-clock projection with rightmost plateau semantics, checked affine translation, explicit preroll detection, and no native clock rereads.
- Added 26 inverse-projection cases; the focused converter/mapper set passed 74/74, the complete suites passed 313/313 EditMode and 6/6 PlayMode, and the inverse hot path measured zero allocations after warmup.
- Advanced the transport item to exact-clock scheduled voice dispatch and hard cancellation before lifecycle orchestration.
- Added exact-clock scheduled oscillator starts and releases, scheduled-voice ownership and cancellation, and a retry-safe FIFO dispatcher with late-event and rounding-plateau accounting.
- Added 15 EditMode cases and one live PlayMode case; the focused instrument/dispatcher set passed 30/30, the complete suites passed 328/328 EditMode and 7/7 PlayMode, and the dispatcher hot path measured zero allocations after warmup.
- Advanced the transport item to the pure Core lifecycle and lookahead state machine.
- Added arbitrary final-onset sequencer reset and the pure Core `Transport` state machine with owned bounded output, monotonic target coalescing, exact pending-window retries, pause/resume, panic, stop, and underrun reporting.
- Added 14 sequencer/transport cases; the focused sets passed 23/23 and 10/10, the complete EditMode suite passed 342/342, and the transport lifecycle hot path measured zero allocations after warmup.
- Completed the Core transport lifecycle work item and advanced M2 to the Unity/FMOD 200 ms runtime scheduler integration.
- Added `FmodTransportScheduler` with one-clock-snapshot pumping, exact 200 ms lookahead, full preroll anchors, bounded dispatch, fail-closed errors, lifecycle reanchoring, and separate timing/backpressure counters.
- Added transport-owned dispatcher coverage and a live scheduler lifecycle smoke; the complete suites passed 343/343 EditMode and 8/8 PlayMode with no retained scheduled voices.
- Completed runtime scheduler integration and advanced M2 to the accelerated ten-minute drift and allocation measurement.
- Added an accelerated ten-minute transport measurement with independent quarter-note tick/frame oracles, regular-versus-irregular cadence equivalence, explicit missing/duplicate/order counters, and hot-path allocation accounting.
- The focused timing case passed with 1,200/1,200 events, zero logical or planned-frame drift, zero cadence-dependent differences, zero underrun/backpressure, and zero allocations.
- Completed every M2 work item and acceptance criterion, marked M2 done, and advanced the plan to the first immutable track-definition slice of M3.
- Added separate immutable `TrackId` and `PatternId` value contracts plus `TrackDefinition`, which owns one resolved-note pattern, derives its independent length, and creates sequencers using track identity as the deterministic random stream.
- Added nine focused identity, ownership, replay, RNG-domain, and 12-step/16-step polymetric cycle cases; the focused set passed 9/9 and the complete EditMode assembly passed 353/353 in the open Unity Editor.
- Completed the first M3 work item and advanced the next action to immutable scale and scale-degree resolution contracts while keeping percussion resolution separate.
- Added immutable `ScaleDegreePitch` and defensively owned `Scale` contracts with frozen signed-degree wrapping, octave offsets, checked MIDI boundaries, and stateless deterministic resolution.
- Added 30 focused scale validation, ownership, value, boundary, integration, determinism, and allocation cases; the focused set passed 30/30 and the complete EditMode assembly passed 383/383 in the open Unity Editor.
- Completed the second M3 work item and advanced the next action to a separate percussion/sample-slot note model.
- Added immutable `PercussionNote` with a nonzero instrument-local sample-slot identity that is structurally separate from MIDI notes and scale degrees.
- Added four focused percussion identity, boundary, and value-semantics cases; the focused set passed 4/4 and the complete EditMode assembly passed 387/387 in the open Unity Editor.
- Completed the third M3 work item and advanced the next action to harmony-plan and track-role resolution contracts.
- Added immutable cyclic `HarmonyPlan` data, pitched `TrackRole` policies, and allocation-free `HarmonyResolver` behavior for free lead, root bass, and diatonic triad pad resolution.
- Added 39 focused harmony validation, boundary, cyclic lookup, role, determinism, overflow, integration, and allocation cases; the focused set passed 39/39 and the complete EditMode assembly passed 426/426.
- Completed the fourth M3 work item and advanced the next action to the five required FMOD Studio track buses.
- Added five direct-child FMOD Studio track buses, rebuilt the Desktop Master Bank, froze all authored bus paths in `FmodTrackBusPaths`, and verified every new bus as a valid live ChannelGroup.
- Completed the FMOD bus work item and advanced M3 to harmony-aware pitched and explicit percussion-slot pattern-to-event pipelines required by the four-track coordinator.
- Added the unresolved pitched pattern pipeline and final-onset harmony resolution for bass, lead, and pad tracks; 32/32 focused EditMode cases passed.
- Completed the pitched half of the typed pipeline work and kept the explicitly parallel percussion and mixer-control slices in progress.
- Added the explicit percussion-slot pattern and event pipeline without any MIDI or harmony conversion; 70/70 focused EditMode cases passed.
- Completed both typed pipeline slices and advanced the Core work to deterministic four-track coordination while mixer controls remain explicitly parallelized.
- Added the bounded `FourTrackSequencer` and typed cross-track event union with canonical same-tick ordering; 9/9 focused EditMode cases passed.
- Completed deterministic Core coordination and advanced the audio boundary to explicit per-track FMOD instrument binding.
- Generalized pooled oscillator instruments to an explicit authored track bus and verified a live lead-bus voice; 23/23 focused EditMode and 1/1 focused PlayMode cases passed.
- Completed per-track audio routing and advanced to the already parallelized mixer-control slice.
- Added lifecycle-safe per-track gain, mute, and low-pass controls with live cross-bus isolation; 12/12 focused EditMode and 2/2 focused PlayMode cases passed.
- Completed the mixer-control work item and advanced M3 to its playable four-track demonstration and final milestone verification.
- Added the disabled-by-default scene-backed four-track demo with independent 3/4/5/7-beat patterns, direct Drums/Bass/Lead/Pad routing, and fixed runtime pump order.
- Fixed scheduled voice cleanup by retaining the channel through its release boundary and detaching the owned low-pass before stopping the channel.
- Passed the final 549/549 EditMode and 13/13 PlayMode suites, completed every M3 work item and acceptance criterion, marked M3 done, and advanced the plan to M4 mutation contracts.
- Stabilized the completed M3 runtime scheduler against routed-bus DSP-clock regression observed during an Editor view-mode transition; focused recovery and complete 549/549 EditMode plus 14/14 PlayMode verification passed before returning M4 to ready.
- Closed the remaining maximized-Game scheduling race by combining two-buffer dispatcher headroom with a final fresh-clock interval shift at the voice boundary; focused tests, 550/550 EditMode, 15/15 PlayMode, and an active four-track maximized-view smoke passed before returning M4 to ready.

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
- Formalized the first-party LOOM coding, structure, naming, testing, and asset conventions in `docs/coding-standards.md` and linked it from both agent instruction files.
- Added a LOOM-scoped `.editorconfig`, a dependency-free repository checker, and a GitHub Actions workflow that runs the checker for pushes and pull requests.
- Verified the coding standard across 38 text files plus Unity metadata parity, passed `git diff --check`, and confirmed a clean compile with zero Console errors in the open Unity Editor before returning the active milestone to M2.
