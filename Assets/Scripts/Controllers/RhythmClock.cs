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

        [Header("Meter")]
        [Tooltip("Beats in one bar. 4 is 4/4 time; beat 1 of every bar is the downbeat that accents the step.")]
        [Range(1, 16)] public int beatsPerBar = 4;
        [Tooltip("Bars in one phrase. The phrase is the repeating section later music layers are arranged on.")]
        [Range(1, 16)] public int barsPerPhrase = 4;

        public int BeatsPerPhrase => beatsPerBar * barsPerPhrase;

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
            beatsPerBar = Mathf.Clamp(beatsPerBar, 1, 16);
            barsPerPhrase = Mathf.Clamp(barsPerPhrase, 1, 16);
        }
    }

    // One beat of the grid with its place in the musical structure: beats form bars (4/4 by
    // default) and bars form phrases. Positions count from the clock start, so the structure keeps
    // running while the knight stands still and movement joins the bar already in progress.
    public readonly struct RhythmBeat
    {
        public readonly long Index;
        public readonly int BeatInBar;
        public readonly long Bar;
        public readonly int BarInPhrase;
        // Footstep landing on this beat; whole beats always fall on whole steps.
        public readonly long StepIndex;
        public readonly double Time;
        public readonly float Bpm;

        public RhythmBeat(long index, long stepIndex, double time, float bpm, int beatsPerBar, int barsPerPhrase)
        {
            Index = index;
            beatsPerBar = Mathf.Max(1, beatsPerBar);
            BeatInBar = (int)Mod(index, beatsPerBar);
            Bar = (index - BeatInBar) / beatsPerBar;
            BarInPhrase = (int)Mod(Bar, Mathf.Max(1, barsPerPhrase));
            StepIndex = stepIndex;
            Time = time;
            Bpm = bpm;
        }

        // Beat 1 of a bar.
        public bool IsDownbeat => BeatInBar == 0;
        // Beat 1 of the first bar of a phrase.
        public bool IsPhraseStart => BeatInBar == 0 && BarInPhrase == 0;

        public static long Mod(long value, long divisor) => ((value % divisor) + divisor) % divisor;
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
        public int BeatsPerBar => settings.beatsPerBar;
        public int BarsPerPhrase => settings.barsPerPhrase;
        // Continuous position inside the current phrase, 0..beats per phrase.
        public double PhrasePosition
        {
            get
            {
                double length = settings.BeatsPerPhrase;
                double position = BeatPosition % length;
                return position < 0d ? position + length : position;
            }
        }
        public RhythmBeat LastBeat { get; private set; }

        // True when footstep `stepIndex` is the one that landed on the most recent beat and that
        // beat is a downbeat. Gait steps fire in the frame their beat is crossed, so this is read
        // in the same frame. `phraseStart` reports beat 1 of the first bar of a phrase.
        public bool IsDownbeatStep(long stepIndex, out bool phraseStart)
        {
            RhythmBeat beat = LastBeat;
            bool downbeat = HasBeaten && beat.StepIndex == stepIndex && beat.IsDownbeat;
            phraseStart = downbeat && beat.IsPhraseStart;
            return downbeat;
        }

        // Starts at the Walk tempo; beat 0 (on step 0) sounds one beat interval after `now`.
        public void Initialize(double now)
        {
            tempo = settings.Get(RhythmLocomotionMode.Walk);
            hasPendingTempo = false;
            lastTime = now;
            beatFraction = 0d;
            NextBeatIndex = 0;
            stepsAtLastBeat = -tempo.stepsPerBeat;
            LastBeat = default;
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
            LastBeat = new RhythmBeat(index, stepsAtLastBeat, time, tempo.bpm, settings.beatsPerBar, settings.barsPerPhrase);
            output?.Add(LastBeat);
        }
    }
}
