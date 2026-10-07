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
            var settings = new RhythmSettings();
            double walkInterval = 60d / 90d;

            // Defaults per locomotion mode and validation of broken values.
            Check(Same(settings.sneak, 60f, 1) && Same(settings.walk, 90f, 1) && Same(settings.run, 120f, 2),
                "Default tempos Sneak 60x1, Walk 90x1, Run 120x2", ref assertions);
            Check(Same(settings.Get(RhythmLocomotionMode.Run), 120f, 2) && Same(settings.Get(RhythmLocomotionMode.Sneak), 60f, 1),
                "Mode lookup", ref assertions);
            var broken = new RhythmSettings { walk = new RhythmTempo(0f, 0) };
            broken.Validate();
            Check(Same(broken.walk, 20f, 1), "Validate clamps tempo", ref assertions);

            // Without input the clock free-runs at the Walk tempo; beat 0 sounds one interval after start.
            var clock = new RhythmClock(new RhythmSettings());
            clock.Initialize(0d);
            clock.Advance(2.05d, beats);
            bool grid = beats.Count == 3;
            for (int i = 0; grid && i < beats.Count; i++)
                grid = beats[i].Index == i && Math.Abs(beats[i].Time - (i + 1) * walkInterval) < 1e-6
                    && beats[i].Accent == (i % 2 == 0) && Mathf.Approximately(beats[i].Bpm, 90f);
            Check(grid, "Walk 90 BPM free-run, got " + beats.Count + " beats", ref assertions);
            Check(Math.Abs(clock.StepPosition - 2.075d) < 1e-6 && Mathf.Approximately(clock.StepsPerSecond, 1.5f),
                "Step position follows the beats, got " + clock.StepPosition, ref assertions);

            // Per-frame advancing lands on the same grid as one large advance.
            var framed = new RhythmClock(new RhythmSettings());
            framed.Initialize(0d);
            var frameBeats = new List<RhythmBeat>();
            for (int frame = 1; frame <= 123; frame++) framed.Advance(frame / 60d, frameBeats);
            grid = frameBeats.Count == beats.Count;
            for (int i = 0; grid && i < frameBeats.Count; i++) grid = Math.Abs(frameBeats[i].Time - beats[i].Time) < 1e-6;
            Check(grid && Math.Abs(framed.StepPosition - clock.StepPosition) < 1e-6, "Per-frame advance matches", ref assertions);

            // Before the first beat a tempo applies immediately.
            clock = new RhythmClock(new RhythmSettings());
            clock.Initialize(0d);
            clock.SetTempo(settings.Get(RhythmLocomotionMode.Sneak));
            Check(Mathf.Approximately(clock.Bpm, 60f) && !clock.HasPendingTempo, "Tempo before the first beat is immediate", ref assertions);
            beats.Clear();
            clock.Advance(1.05d, beats);
            Check(beats.Count == 1 && Math.Abs(beats[0].Time - 1d) < 1e-6 && Math.Abs(clock.StepPosition - 0.05d) < 1e-6,
                "First beat at the new tempo lands on step 0", ref assertions);

            // A tempo change waits for the next beat.
            clock = new RhythmClock(new RhythmSettings());
            clock.Initialize(0d);
            clock.Advance(1d, null);
            clock.SetTempo(settings.Get(RhythmLocomotionMode.Run));
            Check(Mathf.Approximately(clock.Bpm, 90f) && clock.HasPendingTempo, "Run tempo is queued", ref assertions);
            beats.Clear();
            clock.Advance(1.2d, beats);
            Check(beats.Count == 0 && Mathf.Approximately(clock.Bpm, 90f), "Tempo kept until the beat", ref assertions);
            clock.Advance(1.4d, beats);
            Check(beats.Count == 1 && Math.Abs(beats[0].Time - 2d * walkInterval) < 1e-6 && Mathf.Approximately(beats[0].Bpm, 120f)
                && Mathf.Approximately(clock.Bpm, 120f) && !clock.HasPendingTempo,
                "Run tempo starts on beat 1", ref assertions);
            Check(Math.Abs(clock.NextBeatTime - (2d * walkInterval + 0.5d)) < 1e-6, "Next beat at 120 BPM spacing", ref assertions);

            // Step position stays on the grid across 1 -> 2 -> 1 steps per beat.
            Check(Math.Abs(clock.StepPosition - (1d + (1.4d - 2d * walkInterval) / 0.5d * 2d)) < 1e-6,
                "Two steps per beat after the change, got " + clock.StepPosition, ref assertions);
            double midBeat = 2d * walkInterval + 0.75d;
            beats.Clear();
            clock.Advance(midBeat, beats);
            Check(beats.Count == 1 && Math.Abs(clock.StepPosition - 4d) < 1e-6 && Mathf.Approximately(clock.StepsPerSecond, 4f),
                "Beat 2 on step 3, half a run beat later step 4, got " + clock.StepPosition, ref assertions);
            clock.SetTempo(settings.Get(RhythmLocomotionMode.Walk));
            clock.Advance(midBeat + 0.25d + walkInterval * 0.5d, null);
            Check(Math.Abs(clock.StepPosition - 5.5d) < 1e-6 && Mathf.Approximately(clock.Bpm, 90f),
                "Back to walk: beat 3 on step 5, got " + clock.StepPosition, ref assertions);

            // Up to four beats in one advance are all emitted.
            clock = new RhythmClock(new RhythmSettings());
            clock.Initialize(0d);
            beats.Clear();
            clock.Advance(4d * walkInterval + 0.1d, beats);
            Check(beats.Count == 4, "Four beats catch up, got " + beats.Count, ref assertions);

            // A hitch longer than four beats skips silently and keeps beats and steps aligned.
            clock = new RhythmClock(new RhythmSettings());
            clock.Initialize(0d);
            clock.Advance(1d, null);
            beats.Clear();
            clock.Advance(11d, beats);
            Check(beats.Count == 0, "Hitch beats skipped, got " + beats.Count, ref assertions);
            Check(clock.NextBeatIndex == 16 && Mathf.Abs(clock.BeatPhase - 0.5f) < 1e-4f
                && Math.Abs(clock.BeatPosition - 15.5d) < 1e-6 && Math.Abs(clock.StepPosition - 15.5d) < 1e-6,
                "Skip keeps alignment, beat " + clock.BeatPosition + " step " + clock.StepPosition, ref assertions);
            clock.Advance(11.4d, beats);
            Check(beats.Count == 1 && beats[0].Index == 16 && beats[0].Accent && Math.Abs(beats[0].Time - 17d * walkInterval) < 1e-6,
                "Grid continues after the skip", ref assertions);

            // Accented (even) beats fall on even steps, i.e. the left foot, at one step per beat.
            clock = new RhythmClock(new RhythmSettings());
            clock.Initialize(0d);
            beats.Clear();
            bool parity = true;
            for (int frame = 1; frame <= 350; frame++)
            {
                int before = beats.Count;
                clock.Advance(frame / 60d, beats);
                if (beats.Count == before) continue;
                RhythmBeat beat = beats[beats.Count - 1];
                bool leftFoot = ((long)Math.Floor(clock.StepPosition) & 1L) == 0L;
                parity &= beats.Count == before + 1 && beat.Accent == ((beat.Index & 1L) == 0L) && leftFoot == beat.Accent;
            }
            Check(parity && beats.Count == 8, "Accent on the left foot, " + beats.Count + " beats", ref assertions);

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

        private static bool Same(RhythmTempo tempo, float bpm, int stepsPerBeat)
        {
            return Mathf.Approximately(tempo.bpm, bpm) && tempo.stepsPerBeat == stepsPerBeat;
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
