using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.Controllers
{
    // Shared beat clock whose tempo follows the knight's locomotion mode (Sneak/Walk/Run). The gait
    // reads StepPosition so the legs step on the grid; other systems read the tempo and phase or
    // subscribe to Beat. Audio that must land exactly on a beat can use NextBeatDspTime with
    // AudioSource.PlayScheduled. Runs before ProceduralCharacter so the gait sees this frame's clock.
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(CartoonCharacterGait))]
    public sealed class RhythmConductor : MonoBehaviour
    {
        [Tooltip("Shared rhythm tuning. Without a profile the local settings below are used.")]
        [SerializeField] private RhythmProfile profile;
        [SerializeField] private RhythmSettings localSettings = new RhythmSettings();
        [Tooltip("Seconds a new locomotion mode must be requested continuously before the tempo follows. "
            + "Filters the Sneak zone a gamepad stick passes through when it is pushed or released.")]
        [SerializeField, Range(0f, 1f)] private float modeChangeDelay = 0.2f;

        public static RhythmConductor Current { get; private set; }
        public event Action<RhythmBeat> Beat;

        public bool IsReady => clock != null;
        public float Bpm => clock != null ? clock.Bpm : 0f;
        public int StepsPerBeat => clock != null ? clock.StepsPerBeat : 1;
        public float StepsPerSecond => clock != null ? clock.StepsPerSecond : 0f;
        public double StepPosition => clock != null ? clock.StepPosition : 0d;
        public double BeatPosition => clock != null ? clock.BeatPosition : 0d;
        public long NextBeatIndex => clock != null ? clock.NextBeatIndex : 0;
        public bool HasPendingTempo => clock != null && clock.HasPendingTempo;
        // The clock runs on scaled game time, audio on dsp time: convert the remaining wait.
        // While the game is paused (time scale 0) the next beat never arrives.
        public double NextBeatDspTime => clock == null ? 0d
            : Time.timeScale <= 0f ? double.PositiveInfinity
            : AudioSettings.dspTime + (clock.NextBeatTime - Time.timeAsDouble) / Time.timeScale;
        public float BeatPhase => clock != null ? clock.BeatPhase : 0f;
        public RhythmBeat LastBeat { get; private set; }
        public RhythmLocomotionMode Mode { get; private set; } = RhythmLocomotionMode.Walk;
        public bool IsMoving { get; private set; }

        private RhythmClock clock;
        private RhythmLocomotionMode candidate = RhythmLocomotionMode.Walk;
        private float candidateTime;
        private readonly List<RhythmBeat> crossed = new List<RhythmBeat>();

        private RhythmSettings Settings => profile != null ? profile.Settings : localSettings;

        private void Awake() => EnsureClock();

        // The clock is runtime-only state; a script reload during Play Mode drops it, so recreate it.
        private void EnsureClock()
        {
            if (clock != null) return;
            clock = new RhythmClock(Settings);
            clock.Initialize(Time.timeAsDouble);
        }

        private void OnEnable()
        {
            Current = this;
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
        }

        // Called every frame by the character. The mode only changes while there is movement
        // intent, so standing still keeps the last tempo; the clock applies it on the next beat.
        public void SetLocomotionMode(RhythmLocomotionMode mode, bool moving)
        {
            IsMoving = moving;
            if (!moving || clock == null)
            {
                candidateTime = 0f;
                return;
            }
            if (mode == Mode)
            {
                candidateTime = 0f;
                clock.SetTempo(Settings.Get(mode));
                return;
            }
            // A different mode must hold for a moment, so a stick sweeping through the Sneak zone
            // on its way out or back to centre does not change the tempo.
            if (mode != candidate) candidateTime = 0f;
            candidate = mode;
            candidateTime += Time.deltaTime;
            if (candidateTime < modeChangeDelay) return;
            Mode = mode;
            candidateTime = 0f;
            clock.SetTempo(Settings.Get(mode));
        }

        private void Update()
        {
            EnsureClock();
            crossed.Clear();
            clock.Advance(Time.timeAsDouble, crossed);
            for (int i = 0; i < crossed.Count; i++)
            {
                LastBeat = crossed[i];
                Beat?.Invoke(crossed[i]);
            }
        }

        private void OnValidate() => localSettings?.Validate();
    }
}
