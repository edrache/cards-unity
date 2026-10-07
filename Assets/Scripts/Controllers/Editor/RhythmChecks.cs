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

            // Starting to walk just after a free-running beat does not produce a double beat.
            clock = new RhythmClock(new RhythmSettings());
            clock.Initialize(0d);
            beats.Clear();
            clock.Advance(1d, beats);
            clock.ReportStep(1.1d, true);
            beats.Clear();
            clock.Advance(1.1d, beats);
            Check(beats.Count == 0 && Math.Abs(clock.NextBeatTime - 2.1d) < 1e-6,
                "No double beat when moving right after a beat", ref assertions);
            clock.Advance(2.1d, beats);
            Check(beats.Count == 1 && !beats[0].Accent, "Grid continues from the step; next beat is the other foot", ref assertions);

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
