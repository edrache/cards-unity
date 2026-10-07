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
