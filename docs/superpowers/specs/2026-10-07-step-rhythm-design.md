# Step rhythm and synth audio design

Date: 2026-10-07
Source sound set: `synth` repository, `docs/PODSUMOWANIE.md` (prefix `dc_`, key of C minor).

## Goal

Give the game a pulse driven by the knight's footsteps. Steps are heard (tonal synth steps whose
note follows the movement direction) and seen (dust under the planted foot). A central rhythm clock
derives a tempo from the steps so that later systems (centipedes, treasure melodies) can act on the
beat. The background music is replaced by a constant-tempo C-minor cave drone.

Out of scope now: centipede rhythm, treasure melodies, tempo-synced drone.

## Decisions taken with the user

- Direction is measured **relative to the camera** (the knight always turns toward its velocity,
  so "relative to facing" would almost always be forward).
- Rhythm model is the **hybrid**: steps lead, the tempo snaps to musical tiers, the clock phase
  softly follows the real steps. The player's control is never altered.
- When the knight stands still, the clock **keeps ticking at the last tempo**.
- The drone **replaces** the current music track and never follows the tempo.
- The missing in-place turn shuffle sounds are generated now with the `synth-sound` skill.

## Components

All runtime code lives in `Assets/Scripts/Controllers/` (`CardsUnity.Controllers`), following the
existing profile pattern: shared tuning in ScriptableObjects under `Assets/Settings/Gameplay/`,
mutable state on instances.

### `RhythmClock` (plain C#, no Unity objects)

Pure, testable tempo/phase logic.

- `ReportStep(double time, bool accent)`: measures the interval since the previous step. Intervals
  longer than `maxStepGap` are a restart: the clock re-anchors the beat phase hard to this step and
  keeps the last tempo. Otherwise the interval is smoothed (exponential average, `tempoSmoothing`).
- Tempo snapping: the smoothed BPM is mapped to the nearest entry of `tempoTiers` (sorted BPM list,
  default 48, 60, 72, 90, 120, 144). A tier change needs the measured BPM to pass the midpoint
  between tiers by `tierHysteresis` (fraction of the gap) on `tierConfirmSteps` consecutive steps.
- Phase lock: on each step the signed error to the nearest beat (wrapped to ±half a beat) moves the
  next beat time by `error * phaseCorrection` (0..1). Accented steps also realign the accent parity
  so that left-foot steps land on even beats.
- `Advance(double now)`: returns beats crossed since the last call (index, accent, scheduled time).
  With no steps the clock free-runs at the last tempo; before any step it uses `defaultBpm`.
- Exposes `Bpm`, `BeatInterval`, `BeatPhase` (0..1), `NextBeatTime`, `BeatIndex`, `BeatInBar`
  (4 beats per bar).

### `RhythmProfile` (ScriptableObject)

`defaultBpm`, `tempoTiers`, `tierHysteresis`, `tierConfirmSteps`, `tempoSmoothing`,
`phaseCorrection`, `maxStepGap`. Asset: `Assets/Settings/Gameplay/Rhythm Default.asset`.

### `RhythmConductor` (MonoBehaviour on the knight)

Subscribes to `CartoonCharacterGait.Footstep`, feeds a `RhythmClock` using `AudioSettings.dspTime`
as the time base, and in `Update` raises `event Action<RhythmBeat> Beat` for every crossed beat.
Public read-only `Bpm`, `BeatPhase`, `NextBeatDspTime` so later systems can schedule sounds with
`AudioSource.PlayScheduled`. A static `Current` accessor (last enabled conductor) lets other
objects find it without scene wiring. A custom inspector shows live BPM, tier, phase and beat index
in Play Mode.

### `StepDirectionSelector` and `VariantPicker` (plain C#)

- Direction: angle between the horizontal movement vector and the camera's horizontal forward,
  four sectors (forward −45..45°, right 45..135°, back, left), with `sectorHysteresis` degrees
  (default 12) before leaving the current sector.
- Variant: random index from N that never repeats the previous index (N ≥ 2).

### `StepSoundSet` (ScriptableObject)

Four directions × two feet, each a list of clips (4 synth variants). Notes: forward C2, left Es2,
right G2, back As1. Also the turn shuffle clips (left/right lists). Asset:
`Assets/Settings/Gameplay/Step Sounds Synth.asset`.

### `FootstepAudio` (rewritten in place)

Keeps the script GUID and the prefab component; replaces the two old clips and pitch settings.

- Reads `StepSoundSet`, selects direction from the knight's horizontal velocity (measured from
  position change) against the movement camera, picks a non-repeating variant.
- Voice pool of 3 `AudioSource`s round-robin; tails are never cut.
- **Never changes pitch.** Speed maps to volume and an `AudioLowPassFilter` cutoff per voice
  (slow = darker/quieter); sneak/run weights scale volume only; lateral movement pans ±0.2.
- Turn in place: when horizontal speed is below `turnIdleSpeed`, accumulated yaw over
  `turnShuffleAngle` (default 75°) plays a shuffle panned toward the turn direction.
- Keeps `output` mixer group, spatialization and `minimumInterval` guard.

### `FootstepDust` (MonoBehaviour on the knight)

Subscribes to `Footstep` and emits from one world-space `ParticleSystem` (instantiated from the
`Assets/Prefabs/Step Dust.prefab` the designer edits) at the contact point with
`EmitParams.position`. Count scales with step intensity; accented (left) steps emit
`accentMultiplier` times more. No per-step instantiation.

## Audio assets and mix

- Export from the synth library to `Assets/Audio/Synth/`: 32 step WAVs, `dc_despair_cave_drone_60s`,
  generated `dc_turn_shuffle_l/_r` variants.
- `MusicPlayer` in the cave scenes plays the 60 s drone instead of the current track (constant
  tempo, loop) at a volume about 8 dB under the steps. `Deep.mixer` currently has only `Master`;
  adding `Steps`/`Ambient` groups needs the mixer editor (no scripting API without reflection), so
  it is a manual designer step and the components keep their optional `output` group fields.
  Step ducking of the drone is deferred until it is heard in game.

## Verification

- `ConfigurationChecks` (existing editor menu, no test assembly) gains checks for `RhythmClock`
  (tier snapping, hysteresis, phase correction, restart after a gap, free-running beats),
  `StepDirectionSelector` (sectors and hysteresis) and `VariantPicker` (no repeats).
- Compile in the open editor, inspect console; Play Mode in `ProceduralCave`: walk/run/stop/turn,
  watch conductor inspector, listen, check dust.
- Update designer handbook (`docs/designer-guide/index.html`, `system-notes.json`, regenerate
  catalog).

---

## Revision 2026-10-07 (b): input-driven tempo, clock-driven gait

Playtest feedback: obstacles slow the knight, so measured step tempo drops. The user wants the
**tempo chosen by the controller state** and the **legs to move in that rhythm** regardless of
the physical speed. This replaces the step-led hybrid above (tier snapping from measured steps,
phase correction, restart re-anchoring are removed).

Decisions taken with the user:

- Tempo per locomotion mode, editable in `Rhythm Default.asset`: Sneak 60 BPM × 1 step per beat,
  Walk 90 × 1, Run 120 × 2 (steps on eighths so running strides stay ~1.75 m at 7 m/s).
- Keyboard: Run held → Run, Sneak toggle → Sneak, otherwise Walk. Gamepad: stick deflection picks
  Sneak/Walk exactly as today (`activeSneak`/`activeWalk`), Run button → Run. Speed still follows
  stick deflection; only the cadence is fixed per mode, so a lighter push makes **shorter steps in
  the same tempo**. Scripted moves (intro) use Walk.
- Standing still keeps the last mode's tempo; the mode only updates while there is movement intent.
- Mode changes take effect on the next beat so the pulse never breaks.
- **1a:** pushing into a wall or barely deflecting the stick keeps stepping in place in rhythm
  (steps, sound and dust continue).
- **2b:** rhythm is perfectly continuous. The gait phase always follows the clock, so when the
  player starts moving the legs pick up the running phase immediately and the first foot contact
  lands on the step grid (never later than one step interval).
- This deliberately replaces the AGENTS.md rule "gait phase/footsteps use actual distance,
  including stopping at walls" for the knight when a `RhythmConductor` is present; without one the
  gait keeps the distance-driven behaviour.

### Clock

`RhythmClock` becomes a tempo-led clock (time base: `Time.timeAsDouble`, smooth per frame; dsp
time only for `NextBeatDspTime`):

- `RhythmTempo { float bpm; int stepsPerBeat; }`; `RhythmSettings { RhythmTempo sneak, walk, run; }`.
- `SetTempo(RhythmTempo)` queues a change applied at the next beat boundary (immediately before
  the first beat).
- `Advance(double now, List<RhythmBeat>)` integrates `BeatPosition` (continuous beats) and
  `StepPosition` (continuous steps, `+= dBeats × stepsPerBeat` with the multiplier switching only on
  beat boundaries, so whole steps stay aligned to the grid). More than 4 beats behind skips ahead
  silently, keeping alignment.
- Beat accent: even beat index. Step parity: even step index = left foot, so with 1 step per beat
  the accented beat is the left foot.

### Conductor

`RhythmConductor` (execution order before `ProceduralCharacter`) advances the clock every frame and
exposes `StepPosition`, `StepsPerSecond`, `Bpm`, `BeatPhase`, `NextBeatDspTime`, `Beat` event, and
`SetLocomotionMode(RhythmLocomotionMode mode, bool moving)` called by `ProceduralCharacter` each
frame (`Sneak`, `Walk`, `Run`).

### Gait

`CartoonCharacterGait` uses the conductor when present (`GetComponent`):

- Cycle: `continuousCycle = StepPosition / 2`, `cycle = frac(continuousCycle)`; footstep reporting
  unchanged (half-cycle index parity), so contacts fall exactly on the step grid.
- No-slip stride: stance sweep (step length) = actual horizontal speed / `StepsPerSecond`, clamped
  to `maximumRhythmStepLength` (default 1.8 m); stride = 2 × step length. At zero speed the foot
  sweep is zero: stepping in place.
- Movement intent sets an activity floor (`rhythmMarchActivity`, default 0.35) so the legs keep
  lifting at a wall or with a tiny stick push; without intent activity decays as today and the
  steps stop.
- Distance-driven behaviour remains the fallback without a conductor.

### Verification

Rewrite `RhythmChecks` for the new clock: default Walk tempo free-runs; tempo change waits for the
next beat; step position stays integer-aligned across a 1→2 steps-per-beat change; hitch skip keeps
alignment; accent/parity. Play Mode: walk into a wall (steps continue, BPM unchanged), sneak/walk/
run tempos, stop (tempo kept), start (first contact on the grid).
