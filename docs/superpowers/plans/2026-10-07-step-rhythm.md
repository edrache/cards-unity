# Step Rhythm and Synth Audio Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Footstep-driven rhythm clock, tonal synth footsteps chosen by camera-relative direction, step dust particles, turn shuffles and a constant C-minor cave drone.

**Architecture:** Pure C# logic (`RhythmClock`, `StepDirectionSelector`, `VariantPicker`) is verified by editor checks without scene objects. Thin MonoBehaviours (`RhythmConductor`, rewritten `FootstepAudio`, `FootstepDust`) subscribe to `CartoonCharacterGait.Footstep`. Tuning lives in ScriptableObjects (`RhythmProfile`, `StepSoundSet`).

**Tech Stack:** Unity 6000.3.10f1, URP 17.3, C# (`CardsUnity.Controllers`, Assembly-CSharp, no asmdef), `synth` CLI (Surge XT) for audio, Unity MCP for editor work.

**Spec:** `docs/superpowers/specs/2026-10-07-step-rhythm-design.md`

## Global Constraints

- Step sounds **never change pitch** (no `AudioSource.pitch` other than 1).
- Direction is camera-relative: forward C2, left Es2, right G2, back As1; sector hysteresis default 12°.
- Rhythm: steps lead, tempo snaps to tiers (default 48, 60, 72, 90, 120, 144 BPM), phase softly follows steps, clock free-runs at the last tempo when idle.
- Drone never follows the tempo.
- Code, comments and docs in English; no reflection (Unity MCP relay constraint); no project asmdef — checks go in the editor `ConfigurationChecks` pattern.
- Only the parent agent mutates the Unity Editor. Sub-agents edit `.cs` files only and never create `.meta` files.
- Do **not** `git commit`; leave changes in the working tree. The parent commits only when the user asks.
- Preserve the `FootstepAudio` script GUID (edit the file in place) and serialized field names `output`, `masterVolume`, `sneakVolume`, `walkVolume`, `runVolume`, `speedInfluence`, `spatialBlend`, `minDistance`, `maxDistance`, `minimumInterval`.

## Review Focus

- Long pause then walking again: first step must re-anchor the beat (beat lands on the step), not emit a burst of stale beats → check "restart after gap" and "Advance skips stale beats" in Task 1.
- Speed hovering at a tier boundary must not flip BPM every step → hysteresis + confirm-steps check in Task 1.
- Diagonal movement exactly on a sector border must not alternate notes → hysteresis check in Task 1.
- Sound sets with 0 or 1 clip per slot must not throw or loop forever → `VariantPicker` count 0/1 check in Task 1 and null/empty guard in Task 3.
- Knight standing still and rotating slowly must trigger shuffles only past the angle, not while walking → turn logic guarded by `turnIdleSpeed` in Task 3, verified in Play Mode (Task 6).

---

### Task 1: Pure rhythm and selection logic + checks  (model: sonnet)

**Files:**
- Create: `Assets/Scripts/Controllers/RhythmClock.cs`
- Create: `Assets/Scripts/Controllers/StepSelection.cs`
- Create: `Assets/Scripts/Controllers/Editor/RhythmChecks.cs`
- Modify: `Assets/Scripts/Controllers/Editor/ConfigurationChecks.cs` (call `RhythmChecks.RunAll(ref assertions)` just before the final `Debug.Log` in `Run`)

**Interfaces — Produces:**
- `[Serializable] public sealed class RhythmSettings` with public fields `defaultBpm`, `tempoTiers` (float[]), `tierHysteresis`, `tierConfirmSteps`, `tempoSmoothing`, `phaseCorrection`, `maxStepGap`, method `void Validate()`.
- `public readonly struct RhythmBeat { long Index; bool Accent; int BeatInBar; double Time; float Bpm; }`
- `public sealed class RhythmClock(RhythmSettings)`: `void Initialize(double now)`, `void ReportStep(double time, bool accent)`, `void Advance(double now, List<RhythmBeat> output)`, `float GetPhase(double now)`, props `float Bpm`, `double BeatInterval`, `double NextBeatTime`, `long NextBeatIndex`, `int TierIndex`, `float MeasuredBpm`.
- `public enum StepDirection { Forward, Right, Back, Left }`
- `public sealed class StepDirectionSelector`: `StepDirection Current`, `StepDirection Select(Vector2 move, Vector2 viewForward, float hysteresisDegrees)`.
- `public static class VariantPicker`: `int Pick(int count, int previous, Func<int, int> randomBelow)` → returns -1 when count ≤ 0.

- [ ] **Step 1: Write the checks** — `Assets/Scripts/Controllers/Editor/RhythmChecks.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CardsUnity.Controllers.Editor
{
    /// <summary>Direct-call checks for the step rhythm clock and step sound selection.</summary>
    public static class RhythmChecks
    {
        [MenuItem("Tools/Cards Unity/Run Rhythm Checks")]
        public static void Run()
        {
            int assertions = 0;
            RunAll(ref assertions);
            Debug.Log("Rhythm checks passed: " + assertions + " assertions.");
        }

        public static void RunAll(ref int assertions)
        {
            var beats = new List<RhythmBeat>();

            // Before any step the clock free-runs at the default tempo.
            var clock = new RhythmClock(new RhythmSettings());
            clock.Initialize(0d);
            clock.Advance(2.05d, beats);
            Check(beats.Count == 2 && Math.Abs(beats[0].Time - 1d) < 1e-6 && Math.Abs(beats[1].Time - 2d) < 1e-6,
                "Default 60 BPM free-run", ref assertions);

            // Steady steps every 0.5 s snap to the 120 BPM tier.
            clock = Walk(new RhythmSettings(), 0.5d, 8, out double t);
            Check(Mathf.Approximately(clock.Bpm, 120f), "Snap to 120 tier, got " + clock.Bpm, ref assertions);

            // 0.8 s steps (75 BPM) land on the nearest tier, 72.
            clock = Walk(new RhythmSettings(), 0.8d, 8, out t);
            Check(Mathf.Approximately(clock.Bpm, 72f), "Snap 75 BPM to 72, got " + clock.Bpm, ref assertions);

            // Hysteresis: at 72, 82 BPM is past the 81 midpoint but inside the 10% margin (82.8), so it stays.
            clock = Walk(new RhythmSettings(), 60d / 72d, 8, out t);
            clock = Continue(clock, t, 60d / 82d, 8, out t);
            Check(Mathf.Approximately(clock.Bpm, 72f), "Hysteresis keeps 72 at 82 BPM", ref assertions);
            clock = Continue(clock, t, 60d / 86d, 6, out t);
            Check(Mathf.Approximately(clock.Bpm, 90f), "86 BPM confirms the 90 tier", ref assertions);

            // Confirm steps: a single fast outlier does not change the tier.
            var oneOff = new RhythmSettings { tempoSmoothing = 1f };
            clock = Walk(oneOff, 1d, 6, out t);
            clock.ReportStep(t + 0.5d, false);
            Check(Mathf.Approximately(clock.Bpm, 60f), "One outlier step keeps the tier", ref assertions);

            // Phase correction: a step 0.1 s late on a 60 BPM grid moves the next beat by half the error.
            var phase = new RhythmSettings { phaseCorrection = 0.5f };
            clock = Walk(phase, 1d, 6, out t);
            double before = clock.NextBeatTime;
            clock.ReportStep(t + 0.1d, true);
            Check(Math.Abs(clock.NextBeatTime - (before + 0.05d)) < 1e-6, "Phase correction 50%", ref assertions);

            // Restart after a long gap: the next step is itself a beat and keeps the last tempo.
            clock = Walk(new RhythmSettings(), 0.5d, 8, out t);
            beats.Clear();
            clock.Advance(t, beats);
            double restart = t + 10d;
            clock.ReportStep(restart, true);
            beats.Clear();
            clock.Advance(restart, beats);
            Check(beats.Count == 1 && Math.Abs(beats[0].Time - restart) < 1e-6 && beats[0].Accent,
                "Restart re-anchors an accented beat on the step", ref assertions);
            Check(Mathf.Approximately(clock.Bpm, 120f), "Restart keeps the last tempo", ref assertions);

            // Advance after a long hitch skips stale beats instead of bursting.
            beats.Clear();
            clock.Advance(restart + 30d, beats);
            Check(beats.Count <= 2, "Stale beats skipped, got " + beats.Count, ref assertions);

            // Accent parity follows the left foot.
            clock = new RhythmClock(new RhythmSettings());
            clock.Initialize(0d);
            clock.ReportStep(0.3d, false);
            beats.Clear();
            clock.Advance(0.3d, beats);
            Check(beats.Count == 1 && !beats[0].Accent, "Right-foot restart is unaccented", ref assertions);

            // Phase helper stays in 0..1.
            float p = clock.GetPhase(clock.NextBeatTime - clock.BeatInterval * 0.25d);
            Check(p > 0.7f && p < 0.8f, "Beat phase 0.75, got " + p, ref assertions);

            // Direction sectors relative to the view forward (+Y). Right of forward is +X.
            var selector = new StepDirectionSelector();
            Check(selector.Select(new Vector2(0f, 1f), Vector2.up, 12f) == StepDirection.Forward, "Forward", ref assertions);
            Check(selector.Select(new Vector2(1f, 0f), Vector2.up, 12f) == StepDirection.Right, "Right", ref assertions);
            Check(selector.Select(new Vector2(0f, -1f), Vector2.up, 12f) == StepDirection.Back, "Back", ref assertions);
            Check(selector.Select(new Vector2(-1f, 0f), Vector2.up, 12f) == StepDirection.Left, "Left", ref assertions);
            selector.Select(Vector2.up, Vector2.up, 12f);
            Check(selector.Select(Rotate(Vector2.up, 50f), Vector2.up, 12f) == StepDirection.Forward,
                "Hysteresis keeps forward at 50 deg", ref assertions);
            Check(selector.Select(Rotate(Vector2.up, 60f), Vector2.up, 12f) == StepDirection.Right,
                "Leaves forward past 57 deg", ref assertions);
            Check(selector.Select(Vector2.zero, Vector2.up, 12f) == StepDirection.Right,
                "Zero movement keeps the current sector", ref assertions);

            // Variant picker never repeats and handles tiny sets.
            Check(VariantPicker.Pick(0, -1, n => 0) == -1, "Empty set", ref assertions);
            Check(VariantPicker.Pick(1, 0, n => 0) == 0, "Single clip repeats", ref assertions);
            for (int previous = 0; previous < 4; previous++)
            for (int roll = 0; roll < 3; roll++)
            {
                int pick = VariantPicker.Pick(4, previous, n => roll);
                Check(pick != previous && pick >= 0 && pick < 4, "No repeat " + previous + "/" + roll, ref assertions);
            }
        }

        private static RhythmClock Walk(RhythmSettings settings, double interval, int steps, out double time)
        {
            var clock = new RhythmClock(settings);
            clock.Initialize(0d);
            return Continue(clock, 0d, interval, steps, out time);
        }

        private static RhythmClock Continue(RhythmClock clock, double start, double interval, int steps, out double time)
        {
            var sink = new List<RhythmBeat>();
            time = start;
            for (int i = 0; i < steps; i++)
            {
                time += interval;
                clock.ReportStep(time, i % 2 == 0);
                clock.Advance(time, sink);
            }
            return clock;
        }

        // Clockwise rotation in the XZ-as-XY plane, so positive degrees turn toward +X (right).
        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = -degrees * Mathf.Deg2Rad;
            return new Vector2(v.x * Mathf.Cos(r) - v.y * Mathf.Sin(r), v.x * Mathf.Sin(r) + v.y * Mathf.Cos(r));
        }

        private static void Check(bool condition, string label, ref int assertions)
        {
            if (!condition) throw new InvalidOperationException("Rhythm check failed: " + label);
            assertions++;
        }
    }
}
```

In `ConfigurationChecks.Run`, insert `RhythmChecks.RunAll(ref assertions);` on the line before `Debug.Log("Configuration checks passed: ...`.

- [ ] **Step 2: Implement `RhythmClock.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.Controllers
{
    [Serializable]
    public sealed class RhythmSettings
    {
        [Tooltip("Tempo used before the first step, in beats per minute.")]
        [Min(20f)] public float defaultBpm = 60f;
        [Tooltip("Musical tempos the measured step rate snaps to, ascending, in BPM.")]
        public float[] tempoTiers = { 48f, 60f, 72f, 90f, 120f, 144f };
        [Tooltip("Extra margin past the midpoint between two tiers, as a fraction of their gap, before the tempo may change.")]
        [Range(0f, 0.45f)] public float tierHysteresis = 0.1f;
        [Tooltip("Consecutive steps that must agree on a new tier before it is used.")]
        [Range(1, 8)] public int tierConfirmSteps = 2;
        [Tooltip("Weight of the newest step interval in the smoothed tempo. 1 uses only the last step.")]
        [Range(0.05f, 1f)] public float tempoSmoothing = 0.35f;
        [Tooltip("Fraction of a step's timing error applied to the beat grid. 0 ignores steps, 1 snaps the grid to every step.")]
        [Range(0f, 1f)] public float phaseCorrection = 0.35f;
        [Tooltip("Seconds without a step after which the next step restarts the grid on itself.")]
        [Min(0.5f)] public float maxStepGap = 2.5f;

        public void Validate()
        {
            defaultBpm = Mathf.Max(20f, defaultBpm);
            if (tempoTiers == null || tempoTiers.Length == 0) tempoTiers = new[] { defaultBpm };
            for (int i = 0; i < tempoTiers.Length; i++) tempoTiers[i] = Mathf.Max(20f, tempoTiers[i]);
            Array.Sort(tempoTiers);
            tierHysteresis = Mathf.Clamp(tierHysteresis, 0f, 0.45f);
            tierConfirmSteps = Mathf.Clamp(tierConfirmSteps, 1, 8);
            tempoSmoothing = Mathf.Clamp(tempoSmoothing, 0.05f, 1f);
            phaseCorrection = Mathf.Clamp01(phaseCorrection);
            maxStepGap = Mathf.Max(0.5f, maxStepGap);
        }
    }

    public readonly struct RhythmBeat
    {
        public readonly long Index;
        public readonly bool Accent;
        public readonly int BeatInBar;
        public readonly double Time;
        public readonly float Bpm;

        public RhythmBeat(long index, double time, float bpm)
        {
            Index = index;
            Accent = (index & 1L) == 0L;
            BeatInBar = (int)(((index % 4L) + 4L) % 4L);
            Time = time;
            Bpm = bpm;
        }
    }

    // Beat grid that follows the knight's steps: one step is one beat, the tempo snaps to musical
    // tiers and the phase is nudged toward each real step. Pure time math so it can be checked directly.
    public sealed class RhythmClock
    {
        private readonly RhythmSettings settings;
        private double lastStepTime = double.NaN;
        private double smoothedInterval;
        private int pendingTier = -1, pendingCount;

        public RhythmClock(RhythmSettings settings)
        {
            this.settings = settings ?? new RhythmSettings();
            this.settings.Validate();
        }

        public double BeatInterval { get; private set; } = 1d;
        public double NextBeatTime { get; private set; }
        public long NextBeatIndex { get; private set; }
        public int TierIndex { get; private set; }
        public float MeasuredBpm { get; private set; }
        public float Bpm => (float)(60d / BeatInterval);

        public void Initialize(double now)
        {
            TierIndex = NearestTier(settings.defaultBpm);
            BeatInterval = 60d / settings.defaultBpm;
            MeasuredBpm = settings.defaultBpm;
            NextBeatTime = now + BeatInterval;
            NextBeatIndex = 0;
            lastStepTime = double.NaN;
            smoothedInterval = 0d;
            pendingTier = -1;
            pendingCount = 0;
        }

        public void ReportStep(double time, bool accent)
        {
            bool restart = double.IsNaN(lastStepTime) || time - lastStepTime > settings.maxStepGap;
            double interval = restart ? 0d : time - lastStepTime;
            lastStepTime = time;
            if (restart || interval <= 1e-4)
            {
                // The player starts moving: the step itself becomes the beat.
                if (restart)
                {
                    NextBeatTime = time;
                    if (((NextBeatIndex & 1L) == 0L) != accent) NextBeatIndex++;
                }
                return;
            }

            smoothedInterval = smoothedInterval <= 0d
                ? interval
                : smoothedInterval + (interval - smoothedInterval) * settings.tempoSmoothing;
            MeasuredBpm = (float)(60d / smoothedInterval);
            UpdateTier(MeasuredBpm);

            // Nearest grid beat to the step, then move the grid part of the way toward the step.
            long k = (long)Math.Round((time - NextBeatTime) / BeatInterval);
            double nearest = NextBeatTime + k * BeatInterval;
            double error = time - nearest;
            NextBeatTime += error * settings.phaseCorrection;
            if (accent && (((NextBeatIndex + k) & 1L) != 0L)) NextBeatIndex++;
        }

        public void Advance(double now, List<RhythmBeat> output)
        {
            if (now - NextBeatTime > BeatInterval * 2d)
            {
                // A hitch or a long pause: skip silently to the latest beat instead of bursting.
                long skip = (long)Math.Floor((now - NextBeatTime) / BeatInterval);
                NextBeatTime += skip * BeatInterval;
                NextBeatIndex += skip;
            }
            while (NextBeatTime <= now)
            {
                output?.Add(new RhythmBeat(NextBeatIndex, NextBeatTime, Bpm));
                NextBeatIndex++;
                NextBeatTime += BeatInterval;
            }
        }

        public float GetPhase(double now)
        {
            return Mathf.Clamp01((float)(1d - (NextBeatTime - now) / BeatInterval));
        }

        private void UpdateTier(float bpm)
        {
            int candidate = NearestTier(bpm);
            if (candidate == TierIndex)
            {
                pendingTier = -1;
                pendingCount = 0;
                return;
            }
            float current = settings.tempoTiers[TierIndex];
            int neighbour = candidate > TierIndex ? TierIndex + 1 : TierIndex - 1;
            float next = settings.tempoTiers[neighbour];
            float threshold = (current + next) * 0.5f + (next - current) * settings.tierHysteresis;
            bool beyond = next > current ? bpm > threshold : bpm < threshold;
            if (!beyond)
            {
                pendingTier = -1;
                pendingCount = 0;
                return;
            }
            pendingCount = pendingTier == candidate ? pendingCount + 1 : 1;
            pendingTier = candidate;
            if (pendingCount < settings.tierConfirmSteps) return;

            // Keep the last beat where it was and stretch only the coming interval.
            double previousBeat = NextBeatTime - BeatInterval;
            TierIndex = candidate;
            BeatInterval = 60d / settings.tempoTiers[TierIndex];
            NextBeatTime = previousBeat + BeatInterval;
            pendingTier = -1;
            pendingCount = 0;
        }

        private int NearestTier(float bpm)
        {
            int best = 0;
            for (int i = 1; i < settings.tempoTiers.Length; i++)
                if (Mathf.Abs(settings.tempoTiers[i] - bpm) < Mathf.Abs(settings.tempoTiers[best] - bpm)) best = i;
            return best;
        }
    }
}
```

Note on the initial tier: `Initialize` sets `BeatInterval` from `defaultBpm` and `TierIndex` to its nearest tier; tier changes only happen through `UpdateTier`. If `defaultBpm` is not itself a tier, the first confirmed tier change replaces it.

- [ ] **Step 3: Implement `StepSelection.cs`**

```csharp
using System;
using UnityEngine;

namespace CardsUnity.Controllers
{
    public enum StepDirection { Forward, Right, Back, Left }

    // Picks the movement sector relative to a view direction, holding the current sector until the
    // angle passes its edge by the hysteresis margin so diagonal movement does not flicker.
    public sealed class StepDirectionSelector
    {
        public StepDirection Current { get; private set; } = StepDirection.Forward;

        public StepDirection Select(Vector2 move, Vector2 viewForward, float hysteresisDegrees)
        {
            if (move.sqrMagnitude < 1e-6f || viewForward.sqrMagnitude < 1e-6f) return Current;
            // Unity's SignedAngle is counter-clockwise positive; right of forward should be positive.
            float angle = -Vector2.SignedAngle(viewForward, move);
            if (Mathf.Abs(Mathf.DeltaAngle(angle, Centre(Current))) <= 45f + Mathf.Max(0f, hysteresisDegrees))
                return Current;
            StepDirection best = StepDirection.Forward;
            float bestDelta = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                float delta = Mathf.Abs(Mathf.DeltaAngle(angle, Centre((StepDirection)i)));
                if (delta < bestDelta) { bestDelta = delta; best = (StepDirection)i; }
            }
            Current = best;
            return Current;
        }

        public void Reset(StepDirection direction = StepDirection.Forward) => Current = direction;

        private static float Centre(StepDirection direction) => (int)direction * 90f;
    }

    public static class VariantPicker
    {
        /// <summary>Random index below <paramref name="count"/> that differs from <paramref name="previous"/> when possible.</summary>
        public static int Pick(int count, int previous, Func<int, int> randomBelow)
        {
            if (count <= 0) return -1;
            if (count == 1) return 0;
            if (previous < 0 || previous >= count) return randomBelow(count);
            int roll = randomBelow(count - 1);
            return roll >= previous ? roll + 1 : roll;
        }
    }
}
```

- [ ] **Step 4: Static self-review** (sub-agent has no editor): re-read both files for compile errors (namespaces, `using`s, `readonly struct` constructors), then report. The parent compiles and runs **Tools > Cards Unity > Run Rhythm Checks** in Task 6; expected log `Rhythm checks passed: N assertions.`

---

### Task 2: Rhythm profile, conductor and live inspector  (model: sonnet)

**Files:**
- Create: `Assets/Scripts/Controllers/RhythmProfile.cs`
- Create: `Assets/Scripts/Controllers/RhythmConductor.cs`
- Create: `Assets/Scripts/Controllers/Editor/RhythmConductorEditor.cs`

**Interfaces:**
- Consumes: `RhythmSettings`, `RhythmClock`, `RhythmBeat` (Task 1); `CartoonCharacterGait.Footstep` (`event Action<GaitFootstep>`), `GaitFootstep.LeftFoot`.
- Produces: `RhythmConductor.Current` (static), `event Action<RhythmBeat> Beat`, `float Bpm`, `float BeatPhase`, `double NextBeatDspTime`, `long NextBeatIndex`, `int TierIndex`, `float MeasuredBpm`.

- [ ] **Step 1: `RhythmProfile.cs`**

```csharp
using UnityEngine;

namespace CardsUnity.Controllers
{
    [CreateAssetMenu(menuName = "Cards Unity/Gameplay/Rhythm Profile", fileName = "Rhythm Profile")]
    public sealed class RhythmProfile : ScriptableObject
    {
        [SerializeField] private RhythmSettings settings = new RhythmSettings();
        public RhythmSettings Settings => settings;
        private void OnValidate() => settings.Validate();
    }
}
```

- [ ] **Step 2: `RhythmConductor.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.Controllers
{
    // Shared beat clock derived from the knight's footsteps. Other systems read the tempo and phase
    // or subscribe to Beat; audio that must land exactly on a beat can use NextBeatDspTime with
    // AudioSource.PlayScheduled. Steps themselves are never delayed to the grid.
    [RequireComponent(typeof(CartoonCharacterGait))]
    public sealed class RhythmConductor : MonoBehaviour
    {
        [Tooltip("Shared rhythm tuning. Without a profile the local settings below are used.")]
        [SerializeField] private RhythmProfile profile;
        [SerializeField] private RhythmSettings localSettings = new RhythmSettings();

        public static RhythmConductor Current { get; private set; }
        public event Action<RhythmBeat> Beat;

        public float Bpm => clock != null ? clock.Bpm : 0f;
        public float MeasuredBpm => clock != null ? clock.MeasuredBpm : 0f;
        public int TierIndex => clock != null ? clock.TierIndex : 0;
        public long NextBeatIndex => clock != null ? clock.NextBeatIndex : 0;
        public double NextBeatDspTime => clock != null ? clock.NextBeatTime : 0d;
        public float BeatPhase { get; private set; }
        public RhythmBeat LastBeat { get; private set; }

        private CartoonCharacterGait gait;
        private RhythmClock clock;
        private readonly List<RhythmBeat> crossed = new List<RhythmBeat>();

        private RhythmSettings Settings => profile != null ? profile.Settings : localSettings;

        private void Awake()
        {
            gait = GetComponent<CartoonCharacterGait>();
            clock = new RhythmClock(Settings);
            clock.Initialize(AudioSettings.dspTime);
        }

        private void OnEnable()
        {
            Current = this;
            if (gait != null) gait.Footstep += OnFootstep;
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
            if (gait != null) gait.Footstep -= OnFootstep;
        }

        private void OnFootstep(GaitFootstep step)
        {
            clock.ReportStep(AudioSettings.dspTime, step.LeftFoot);
        }

        private void Update()
        {
            double now = AudioSettings.dspTime;
            crossed.Clear();
            clock.Advance(now, crossed);
            for (int i = 0; i < crossed.Count; i++)
            {
                LastBeat = crossed[i];
                Beat?.Invoke(crossed[i]);
            }
            BeatPhase = clock.GetPhase(now);
        }

        private void OnValidate() => localSettings?.Validate();
    }
}
```

- [ ] **Step 3: `RhythmConductorEditor.cs`** (live readout; editor code may use IMGUI)

```csharp
using UnityEditor;
using UnityEngine;

namespace CardsUnity.Controllers.Editor
{
    [CustomEditor(typeof(RhythmConductor))]
    public sealed class RhythmConductorEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (!Application.isPlaying) return;
            var conductor = (RhythmConductor)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live rhythm", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Tempo", conductor.Bpm.ToString("0") + " BPM (tier " + conductor.TierIndex
                + ", measured " + conductor.MeasuredBpm.ToString("0.0") + ")");
            EditorGUILayout.LabelField("Next beat", conductor.NextBeatIndex + "  (bar beat " + (conductor.NextBeatIndex % 4 + 1) + ")");
            Rect bar = EditorGUILayout.GetControlRect(false, 16f);
            EditorGUI.ProgressBar(bar, conductor.BeatPhase, "Phase " + conductor.BeatPhase.ToString("0.00"));
        }
    }
}
```

- [ ] **Step 4: Static self-review**, report. Parent verifies compile and live inspector in Task 6.

---

### Task 3: Step sound set and rewritten FootstepAudio  (model: sonnet)

**Files:**
- Create: `Assets/Scripts/Controllers/StepSoundSet.cs`
- Modify (rewrite in place, keep file/GUID): `Assets/Scripts/Controllers/FootstepAudio.cs`

**Interfaces:**
- Consumes: `StepDirection`, `StepDirectionSelector`, `VariantPicker` (Task 1); `GaitFootstep` fields `LeftFoot`, `Intensity`, `SneakAmount`, `RunAmount`, `Position`; `CartoonCharacterGait.Footstep`.
- Produces: `StepSoundSet.GetSteps(StepDirection, bool leftFoot) : AudioClip[]`, `StepSoundSet.GetTurn(bool turnRight) : AudioClip[]`, `StepSoundSet.SlotIndex(StepDirection, bool) : int`; `FootstepAudio.LastDirection` (public read-only `StepDirection`).

- [ ] **Step 1: `StepSoundSet.cs`**

```csharp
using System;
using UnityEngine;

namespace CardsUnity.Controllers
{
    // Tonal footstep clips: the movement direction picks the note (C minor), the foot picks the
    // weight. Each slot holds interchangeable variants that are never played twice in a row.
    [CreateAssetMenu(menuName = "Cards Unity/Audio/Step Sound Set", fileName = "Step Sound Set")]
    public sealed class StepSoundSet : ScriptableObject
    {
        [Serializable]
        public sealed class FootPair
        {
            public AudioClip[] leftFoot = Array.Empty<AudioClip>();
            public AudioClip[] rightFoot = Array.Empty<AudioClip>();
        }

        [Tooltip("Moving away from the camera. Synth set: C2.")]
        [SerializeField] private FootPair forward = new FootPair();
        [Tooltip("Moving to the camera's right. Synth set: G2.")]
        [SerializeField] private FootPair right = new FootPair();
        [Tooltip("Moving toward the camera. Synth set: A-flat 1.")]
        [SerializeField] private FootPair back = new FootPair();
        [Tooltip("Moving to the camera's left. Synth set: E-flat 2.")]
        [SerializeField] private FootPair left = new FootPair();
        [Tooltip("Toneless shuffle when turning left in place.")]
        [SerializeField] private AudioClip[] turnLeft = Array.Empty<AudioClip>();
        [Tooltip("Toneless shuffle when turning right in place.")]
        [SerializeField] private AudioClip[] turnRight = Array.Empty<AudioClip>();

        public const int SlotCount = 10;

        public AudioClip[] GetSteps(StepDirection direction, bool leftFoot)
        {
            FootPair pair = direction switch
            {
                StepDirection.Right => right,
                StepDirection.Back => back,
                StepDirection.Left => left,
                _ => forward
            };
            return (leftFoot ? pair?.leftFoot : pair?.rightFoot) ?? Array.Empty<AudioClip>();
        }

        public AudioClip[] GetTurn(bool turnRight) => (turnRight ? this.turnRight : turnLeft) ?? Array.Empty<AudioClip>();

        public static int SlotIndex(StepDirection direction, bool leftFoot) => (int)direction * 2 + (leftFoot ? 0 : 1);
        public static int TurnSlotIndex(bool turnRight) => turnRight ? 9 : 8;
    }
}
```

- [ ] **Step 2: Rewrite `FootstepAudio.cs`** (full file)

```csharp
using UnityEngine;
using UnityEngine.Audio;

namespace CardsUnity.Controllers
{
    // Tonal footsteps on the gait's foot contacts. The camera-relative movement direction picks the
    // note from a StepSoundSet, the foot picks the weight, and speed only darkens or brightens the
    // sound: pitch is never changed so steps stay in tune with the cave drone. Overlapping tails
    // play on a small round-robin voice pool. Slow turns in place play a toneless shuffle.
    [RequireComponent(typeof(CartoonCharacterGait))]
    public sealed class FootstepAudio : MonoBehaviour
    {
        [Header("Sounds")]
        [SerializeField] private StepSoundSet soundSet;
        [Tooltip("Optional mixer group for all footstep voices.")]
        [SerializeField] private AudioMixerGroup output;
        [Tooltip("View used for the movement direction. Empty uses the main camera.")]
        [SerializeField] private Transform directionReference;
        [Tooltip("Degrees past a sector edge before the note changes, so diagonal walking does not flicker.")]
        [SerializeField, Range(0f, 30f)] private float sectorHysteresis = 12f;

        [Header("Volume per locomotion style")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [Tooltip("Volume of a pure Sneak or Tip Toe step.")]
        [SerializeField, Range(0f, 1f)] private float sneakVolume = 0.22f;
        [Tooltip("Volume of a step in any regular walking style.")]
        [SerializeField, Range(0f, 1f)] private float walkVolume = 0.6f;
        [Tooltip("Volume of a pure Run or Fast Run step.")]
        [SerializeField, Range(0f, 1f)] private float runVolume = 1f;
        [Tooltip("How much the current movement speed scales the step volume. Zero keeps the style volume exactly.")]
        [SerializeField, Range(0f, 1f)] private float speedInfluence = 0.25f;

        [Header("Speed colour (never pitch)")]
        [Tooltip("Horizontal speed in m/s treated as full speed for brightness.")]
        [SerializeField, Min(0.1f)] private float fullSpeed = 3f;
        [Tooltip("Low-pass cutoff of the slowest step, in Hz.")]
        [SerializeField, Range(200f, 22000f)] private float slowCutoff = 1200f;
        [Tooltip("Low-pass cutoff of a full-speed step, in Hz.")]
        [SerializeField, Range(200f, 22000f)] private float fastCutoff = 9000f;
        [Tooltip("Stereo pan applied for pure sideways movement.")]
        [SerializeField, Range(0f, 1f)] private float sidewaysPan = 0.2f;

        [Header("Turning in place")]
        [Tooltip("Below this horizontal speed (m/s) rotation counts as turning in place.")]
        [SerializeField, Min(0f)] private float turnIdleSpeed = 0.25f;
        [Tooltip("Degrees of rotation in place per shuffle sound.")]
        [SerializeField, Range(20f, 180f)] private float turnShuffleAngle = 75f;
        [SerializeField, Range(0f, 1f)] private float turnVolume = 0.5f;
        [Tooltip("Stereo pan toward the turn direction.")]
        [SerializeField, Range(0f, 1f)] private float turnPan = 0.35f;

        [Header("Voices and spatialization")]
        [Tooltip("Simultaneous step voices. Tails overlap instead of being cut.")]
        [SerializeField, Range(1, 6)] private int voiceCount = 3;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.85f;
        [SerializeField, Min(0.1f)] private float minDistance = 1.5f;
        [SerializeField, Min(0.2f)] private float maxDistance = 25f;

        [Header("Guards")]
        [Tooltip("Shortest time between two steps, in seconds. Protects against duplicate contacts at very high cadence.")]
        [SerializeField, Range(0f, 0.3f)] private float minimumInterval = 0.06f;

        public StepDirection LastDirection => selector.Current;

        private CartoonCharacterGait gait;
        private AudioSource[] voices;
        private AudioLowPassFilter[] filters;
        private int nextVoice;
        private readonly StepDirectionSelector selector = new StepDirectionSelector();
        private readonly int[] lastVariant = new int[StepSoundSet.SlotCount];
        private float lastStepTime = -1f;
        private Vector3 lastPosition;
        private Vector3 horizontalVelocity;
        private float lastYaw, turnAccumulator;

        private void Awake()
        {
            gait = GetComponent<CartoonCharacterGait>();
            for (int i = 0; i < lastVariant.Length; i++) lastVariant[i] = -1;
            voices = new AudioSource[voiceCount];
            filters = new AudioLowPassFilter[voiceCount];
            for (int i = 0; i < voiceCount; i++) CreateVoice(i);
            lastPosition = transform.position;
            lastYaw = transform.eulerAngles.y;
        }

        private void OnEnable()
        {
            if (gait != null) gait.Footstep += OnFootstep;
            lastPosition = transform.position;
            lastYaw = transform.eulerAngles.y;
            turnAccumulator = 0f;
        }

        private void OnDisable()
        {
            if (gait != null) gait.Footstep -= OnFootstep;
        }

        private void CreateVoice(int index)
        {
            var holder = new GameObject("Footstep Voice " + (index + 1));
            holder.transform.SetParent(transform, false);
            var source = holder.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.pitch = 1f;
            source.spatialBlend = spatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = Mathf.Max(maxDistance, minDistance + 0.1f);
            source.dopplerLevel = 0f;
            if (output != null) source.outputAudioMixerGroup = output;
            voices[index] = source;
            filters[index] = holder.AddComponent<AudioLowPassFilter>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 displacement = Vector3.ProjectOnPlane(transform.position - lastPosition, Vector3.up);
            lastPosition = transform.position;
            horizontalVelocity = Vector3.Lerp(horizontalVelocity, displacement / dt, 1f - Mathf.Exp(-12f * dt));

            float yaw = transform.eulerAngles.y;
            float delta = Mathf.DeltaAngle(lastYaw, yaw);
            lastYaw = yaw;
            if (horizontalVelocity.magnitude > turnIdleSpeed)
            {
                turnAccumulator = 0f;
                return;
            }
            turnAccumulator += delta;
            if (Mathf.Abs(turnAccumulator) < turnShuffleAngle) return;
            bool turnRight = turnAccumulator > 0f;
            turnAccumulator -= Mathf.Sign(turnAccumulator) * turnShuffleAngle;
            PlayTurn(turnRight);
        }

        private void PlayTurn(bool turnRight)
        {
            if (soundSet == null) return;
            AudioClip clip = PickClip(soundSet.GetTurn(turnRight), StepSoundSet.TurnSlotIndex(turnRight));
            if (clip == null) return;
            Play(clip, transform.position, turnVolume * masterVolume, turnRight ? turnPan : -turnPan, slowCutoff);
        }

        private void OnFootstep(GaitFootstep step)
        {
            if (soundSet == null) return;
            if (minimumInterval > 0f && lastStepTime >= 0f && Time.time - lastStepTime < minimumInterval) return;

            Transform view = directionReference != null ? directionReference
                : Camera.main != null ? Camera.main.transform : transform;
            Vector3 forward3 = Vector3.ProjectOnPlane(view.forward, Vector3.up);
            Vector3 move3 = horizontalVelocity.sqrMagnitude > 1e-4f ? horizontalVelocity : transform.forward;
            var forward = new Vector2(forward3.x, forward3.z);
            var move = new Vector2(move3.x, move3.z);
            StepDirection direction = selector.Select(move, forward, sectorHysteresis);

            AudioClip clip = PickClip(soundSet.GetSteps(direction, step.LeftFoot),
                StepSoundSet.SlotIndex(direction, step.LeftFoot));
            if (clip == null) return;
            lastStepTime = Time.time;

            float sneak = Mathf.Clamp01(step.SneakAmount);
            float run = Mathf.Clamp01(step.RunAmount);
            float speed01 = Mathf.Clamp01(horizontalVelocity.magnitude / fullSpeed);
            float volume = Mathf.Lerp(Mathf.Lerp(walkVolume, sneakVolume, sneak), runVolume, run);
            volume *= masterVolume * Mathf.Lerp(1f - speedInfluence, 1f, Mathf.Max(speed01, Mathf.Clamp01(step.Intensity)));

            // Sideways share of the movement in view space: +1 is pure right.
            Vector3 viewRight = Vector3.Cross(Vector3.up, forward3.normalized);
            float side = move3.sqrMagnitude > 1e-6f ? Vector3.Dot(move3.normalized, viewRight) : 0f;
            float cutoff = Mathf.Lerp(slowCutoff, fastCutoff, speed01);
            Play(clip, step.Position, volume, side * sidewaysPan, cutoff);
        }

        private AudioClip PickClip(AudioClip[] clips, int slot)
        {
            if (clips == null || clips.Length == 0) return null;
            int index = VariantPicker.Pick(clips.Length, lastVariant[slot], n => Random.Range(0, n));
            lastVariant[slot] = index;
            return index >= 0 ? clips[index] : null;
        }

        private void Play(AudioClip clip, Vector3 position, float volume, float pan, float cutoff)
        {
            AudioSource source = voices[nextVoice];
            filters[nextVoice].cutoffFrequency = cutoff;
            nextVoice = (nextVoice + 1) % voices.Length;
            source.transform.position = position;
            source.panStereo = Mathf.Clamp(pan, -1f, 1f);
            source.pitch = 1f;
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.Play();
        }

        private void OnValidate()
        {
            maxDistance = Mathf.Max(maxDistance, minDistance + 0.1f);
            fastCutoff = Mathf.Max(fastCutoff, slowCutoff);
        }
    }
}
```

Note: each voice plays one clip with `Play()` (not `PlayOneShot`) so the per-voice filter and pan apply to exactly that step; with `voiceCount` 3 the oldest voice is reused, matching the 2–3 overlapping tails in the spec.

- [ ] **Step 3: Static self-review**, report. Parent wires the asset and verifies in Task 6.

---

### Task 4: Step dust emitter  (model: haiku)

**Files:**
- Create: `Assets/Scripts/Controllers/FootstepDust.cs`

**Interfaces:**
- Consumes: `CartoonCharacterGait.Footstep`, `GaitFootstep.LeftFoot/Intensity/Position`.
- Produces: component with serialized `dustPrefab` (`ParticleSystem`).

- [ ] **Step 1: Implement**

```csharp
using UnityEngine;

namespace CardsUnity.Controllers
{
    // Puffs of dust under the planted foot so the step rhythm is visible. One world-space particle
    // system is instantiated from the designer's prefab and emits in place for every step.
    [RequireComponent(typeof(CartoonCharacterGait))]
    public sealed class FootstepDust : MonoBehaviour
    {
        [Tooltip("Particle system prefab. Emission rate is ignored; particles are emitted per step.")]
        [SerializeField] private ParticleSystem dustPrefab;
        [Tooltip("Particles for a full-intensity right-foot step.")]
        [SerializeField, Range(0, 64)] private int particlesPerStep = 8;
        [Tooltip("Share of the particles that a very light step still emits.")]
        [SerializeField, Range(0f, 1f)] private float lightStepShare = 0.35f;
        [Tooltip("Multiplier for the accented left-foot step.")]
        [SerializeField, Range(1f, 4f)] private float accentMultiplier = 1.6f;
        [Tooltip("Offset from the foot contact, in world space.")]
        [SerializeField] private Vector3 contactOffset = new Vector3(0f, 0.03f, 0f);

        private CartoonCharacterGait gait;
        private ParticleSystem dust;

        private void Awake()
        {
            gait = GetComponent<CartoonCharacterGait>();
            if (dustPrefab == null) return;
            dust = Instantiate(dustPrefab);
            dust.name = dustPrefab.name + " (" + name + ")";
            var main = dust.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            var emission = dust.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            dust.Play();
        }

        private void OnEnable()
        {
            if (gait != null) gait.Footstep += OnFootstep;
        }

        private void OnDisable()
        {
            if (gait != null) gait.Footstep -= OnFootstep;
        }

        private void OnDestroy()
        {
            if (dust != null) Destroy(dust.gameObject);
        }

        private void OnFootstep(GaitFootstep step)
        {
            if (dust == null) return;
            float share = Mathf.Lerp(lightStepShare, 1f, Mathf.Clamp01(step.Intensity));
            int count = Mathf.RoundToInt(particlesPerStep * share * (step.LeftFoot ? accentMultiplier : 1f));
            if (count <= 0) return;
            var emit = new ParticleSystem.EmitParams
            {
                position = step.Position + contactOffset,
                applyShapeToPosition = true
            };
            dust.Emit(emit, count);
        }
    }
}
```

- [ ] **Step 2: Static self-review**, report.

---

### Task 5: Synth sounds — turn shuffle generation and export  (parent, `synth-sound` skill)

**Files:**
- Create (synth repo): `recipes/sfx/steps/dc_turn_shuffle_l.json`, `dc_turn_shuffle_r.json` (via the skill)
- Create: `Assets/Audio/Synth/Steps/*.wav` (32), `Assets/Audio/Synth/Turn/*.wav`, `Assets/Audio/Synth/Ambient/dc_despair_cave_drone_60s_v01.wav`

- [ ] **Step 1:** Invoke `synth-sound`: toneless, dark, short (≈0.4–0.6 s) boot-on-stone shuffle, 4 variants each for left/right turn, quieter than the steps, no pitched content (must not clash with C minor). Render, check spectrogram/QC, play for the user, iterate on feedback.
- [ ] **Step 2:** Export WAVs:

```bash
cd /Users/marek/OfflineDocuments/Repo/Antigravity/cards-unity
for d in fwd back left right; do for f in l r; do synth export dc_step_${d}_${f} --to Assets/Audio/Synth/Steps --flat --format wav; done; done
synth export dc_turn_shuffle_l --to Assets/Audio/Synth/Turn --flat --format wav
synth export dc_turn_shuffle_r --to Assets/Audio/Synth/Turn --flat --format wav
synth export dc_despair_cave_drone_60s --to Assets/Audio/Synth/Ambient --flat --format wav
ls Assets/Audio/Synth/*/ | wc -l
```

Expected: 32 step files, 8 turn files, 1 drone.

---

### Task 6: Unity integration and verification  (parent only, Unity MCP)

- [ ] **Step 1:** Refresh assets, wait for compile, read console; fix compile errors (escalate to a sonnet sub-agent only for non-trivial fixes).
- [ ] **Step 2:** Import settings: steps/turn clips `Decompress On Load`, mono not forced (keep stereo pan baked in), no preload change; drone `Streaming`, `Load In Background`.
- [ ] **Step 3:** Run **Tools > Cards Unity > Run Rhythm Checks** and **Run Configuration Checks**; both must log "passed".
- [ ] **Step 4:** Create assets: `Assets/Settings/Gameplay/Rhythm Default.asset` (`RhythmProfile`, defaults), `Assets/Settings/Gameplay/Step Sounds Synth.asset` (`StepSoundSet`: forward=fwd, right=right, back=back, left=left; `_l` → leftFoot, `_r` → rightFoot; turns).
- [ ] **Step 5:** Create `Assets/Prefabs/Step Dust.prefab`: `ParticleSystem` world space, not looping, emission 0, start lifetime 0.5–0.9, start speed 0.2–0.6, start size 0.04–0.12, gravity modifier 0.05, shape hemisphere radius 0.08, colour over lifetime fading to alpha 0, size over lifetime growing, renderer material reused from the torch smoke in `Assets/Prefabs/Handheld Torch.prefab`.
- [ ] **Step 6:** On `Assets/Prefabs/Shadow Knight.prefab`: assign `soundSet` on `FootstepAudio`; add `RhythmConductor` (profile = Rhythm Default) and `FootstepDust` (prefab = Step Dust). Check the `ProceduralCave`/`TorchNight` scene instances have no overrides that block this.
- [ ] **Step 7:** In `ProceduralCave.unity` (and `TorchNight.unity` if it has a `MusicPlayer`) set `MusicPlayer.track` to the drone; keep `output`. Volume: start at current value, then by ear the drone ≈8 dB under steps.
- [ ] **Step 8:** Play Mode in `ProceduralCave` (runInBackground on): walk forward/sideways/back relative to camera, sneak, run, stop, turn in place; watch the conductor inspector (tier changes, phase), listen, capture the game view for dust. Restore Edit Mode.

---

### Task 7: Designer documentation  (model: haiku)

**Files:**
- Modify: `docs/designer-guide/system-notes.json` (`FootstepAudio` rewritten; add `RhythmConductor`, `RhythmProfile`, `StepSoundSet`, `FootstepDust`; update `MusicPlayer` note: cave drone, constant tempo)
- Modify: `docs/designer-guide/index.html` (new "Rhythm and step audio" section: how direction picks notes, foot accents, tempo tiers and what each `RhythmProfile` field does, dust prefab tuning, turn shuffle, how to regenerate sounds with `synth`, mixer groups as a manual step, planned centipede/treasure use of `RhythmConductor.Beat`)
- Modify: `docs/gameplay-reference.md` (relevant audio section), `AGENTS.md` project map line "Collection/audio" (add `RhythmConductor.cs`, `FootstepDust.cs`)
- Run: `python3 docs/designer-guide/generate_inventory.py`, then `git diff --check`

- [ ] **Step 1:** Edit the files above to match the implemented code (read the final `.cs` files, not this plan, for field names and defaults).
- [ ] **Step 2:** Run the generator and `git diff --check`; report.

---

## Execution order

Task 1 → (Tasks 2, 3, 4 in parallel; different files) → Task 5 (parent, needs the user's ears) → Task 6 (parent, editor) → Task 7 → whole-branch review (opus) → report; commit only when the user asks.
