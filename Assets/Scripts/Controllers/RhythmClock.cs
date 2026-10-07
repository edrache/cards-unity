using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.Controllers
{
    public enum RhythmLocomotionMode
    {
        Sneak,
        Walk,
        Run
    }

    // Tempo of one locomotion mode: the beat rate and how many footsteps fall on each beat.
    [Serializable]
    public struct RhythmTempo
    {
        [Tooltip("Beats per minute of this mode.")]
        [Min(20f)] public float bpm;
        [Tooltip("Footsteps per beat. 1 steps on quarters, 2 on eighths.")]
        [Range(1, 4)] public int stepsPerBeat;

        public RhythmTempo(float bpm, int stepsPerBeat)
        {
            this.bpm = bpm;
            this.stepsPerBeat = stepsPerBeat;
        }

        public double BeatInterval => 60d / bpm;
        public float StepsPerSecond => bpm / 60f * stepsPerBeat;

        public RhythmTempo Validated()
        {
            return new RhythmTempo(Mathf.Max(20f, bpm), Mathf.Clamp(stepsPerBeat, 1, 4));
        }

        public bool SameAs(RhythmTempo other)
        {
            return Mathf.Approximately(bpm, other.bpm) && stepsPerBeat == other.stepsPerBeat;
        }
    }

    [Serializable]
    public sealed class RhythmSettings
    {
        [Tooltip("Tempo while sneaking.")]
        public RhythmTempo sneak = new RhythmTempo(60f, 1);
        [Tooltip("Tempo while walking. Also used before the first movement.")]
        public RhythmTempo walk = new RhythmTempo(90f, 1);
        [Tooltip("Tempo while running. Two steps per beat keeps running strides long at a musical tempo.")]
        public RhythmTempo run = new RhythmTempo(120f, 2);

        public RhythmTempo Get(RhythmLocomotionMode mode)
        {
            switch (mode)
            {
                case RhythmLocomotionMode.Sneak: return sneak.Validated();
                case RhythmLocomotionMode.Run: return run.Validated();
                default: return walk.Validated();
            }
        }

        public void Validate()
        {
            sneak = sneak.Validated();
            walk = walk.Validated();
            run = run.Validated();
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

    // Tempo-led beat grid. The locomotion mode picks the tempo; changes wait for the next beat so
    // the pulse never breaks. Besides beats it integrates a continuous step position whose whole
    // numbers are footsteps, aligned to the beat grid. Pure time math so it can be checked directly.
    public sealed class RhythmClock
    {
        // More beats than this in one advance is a hitch or a pause: they are skipped silently.
        public const int MaxCatchUpBeats = 4;

        private readonly RhythmSettings settings;
        private RhythmTempo tempo, pendingTempo;
        private bool hasPendingTempo;
        private double lastTime;
        private double beatFraction;
        private long stepsAtLastBeat;

        public RhythmClock(RhythmSettings settings)
        {
            this.settings = settings ?? new RhythmSettings();
            this.settings.Validate();
            tempo = this.settings.Get(RhythmLocomotionMode.Walk);
        }

        public RhythmTempo Tempo => tempo;
        public bool HasPendingTempo => hasPendingTempo;
        public double BeatInterval => tempo.BeatInterval;
        public float Bpm => tempo.bpm;
        public int StepsPerBeat => tempo.stepsPerBeat;
        public float StepsPerSecond => tempo.StepsPerSecond;
        public long NextBeatIndex { get; private set; }
        public bool HasBeaten => NextBeatIndex > 0;
        // Progress from the previous beat to the next one, 0..1.
        public float BeatPhase => Mathf.Clamp01((float)beatFraction);
        // Continuous beats: beat N sounds when this reaches N.
        public double BeatPosition => NextBeatIndex - 1L + beatFraction;
        // Continuous footsteps: step N lands when this reaches N. Even steps are the left foot.
        public double StepPosition => stepsAtLastBeat + beatFraction * tempo.stepsPerBeat;
        public double NextBeatTime => lastTime + (1d - beatFraction) * tempo.BeatInterval;

        // Starts at the Walk tempo; beat 0 (on step 0) sounds one beat interval after `now`.
        public void Initialize(double now)
        {
            tempo = settings.Get(RhythmLocomotionMode.Walk);
            hasPendingTempo = false;
            lastTime = now;
            beatFraction = 0d;
            NextBeatIndex = 0;
            stepsAtLastBeat = -tempo.stepsPerBeat;
        }

        // Queues a tempo for the next beat. Before the first beat it applies immediately.
        public void SetTempo(RhythmTempo next)
        {
            next = next.Validated();
            if (!HasBeaten)
            {
                tempo = next;
                hasPendingTempo = false;
                // Keep beat 0 on step 0, so the accented first beat is a left foot.
                stepsAtLastBeat = -tempo.stepsPerBeat;
                return;
            }
            hasPendingTempo = !next.SameAs(tempo);
            pendingTempo = next;
        }

        public void Advance(double now, List<RhythmBeat> output)
        {
            double remaining = now - lastTime;
            if (remaining <= 0d) return;
            lastTime = now;
            bool skipping = Math.Floor(beatFraction + remaining / tempo.BeatInterval) > MaxCatchUpBeats;
            while (true)
            {
                double toBoundary = (1d - beatFraction) * tempo.BeatInterval;
                if (remaining < toBoundary)
                {
                    beatFraction += remaining / tempo.BeatInterval;
                    return;
                }
                remaining -= toBoundary;
                CompleteBeat(now - remaining, skipping ? null : output);
                if (!skipping) continue;
                // Jump whole beats at once; beat and step counts move together to stay aligned.
                long whole = (long)Math.Floor(remaining / tempo.BeatInterval);
                if (whole <= 0L) continue;
                NextBeatIndex += whole;
                stepsAtLastBeat += whole * tempo.stepsPerBeat;
                remaining -= whole * tempo.BeatInterval;
            }
        }

        private void CompleteBeat(double time, List<RhythmBeat> output)
        {
            long index = NextBeatIndex;
            NextBeatIndex++;
            stepsAtLastBeat += tempo.stepsPerBeat;
            beatFraction = 0d;
            if (hasPendingTempo)
            {
                tempo = pendingTempo;
                hasPendingTempo = false;
            }
            output?.Add(new RhythmBeat(index, time, tempo.bpm));
        }
    }
}
