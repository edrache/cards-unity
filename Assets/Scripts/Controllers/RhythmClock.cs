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
        private double lastBeatTime = double.NaN;
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
            lastBeatTime = double.NaN;
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
                    if (!double.IsNaN(lastBeatTime) && time - lastBeatTime < BeatInterval * 0.5d)
                    {
                        // A free-running beat has just sounded: let it stand for this step instead of
                        // emitting a second beat a moment later, and continue the grid from the step.
                        NextBeatTime = time + BeatInterval;
                        if (((NextBeatIndex - 1L) & 1L) == 0L != accent) NextBeatIndex++;
                    }
                    else
                    {
                        NextBeatTime = time;
                        if (((NextBeatIndex & 1L) == 0L) != accent) NextBeatIndex++;
                    }
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
                lastBeatTime = NextBeatTime;
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
