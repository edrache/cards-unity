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
        [SerializeField, Range(1, 6)] private int voiceCount = 4;
        [Tooltip("Unity voice priority, 0 = most important, 256 = least. When more sounds play than the "
            + "project's real voice limit, the least important ones are silenced first, so steps stay "
            + "ahead of centipede leg clicks.")]
        [SerializeField, Range(0, 256)] private int priority = 32;
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
            source.priority = priority;
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
            // Prefer a voice that has finished; only when all are still ringing is the oldest one reused.
            int index = nextVoice;
            for (int i = 0; i < voices.Length; i++)
            {
                int candidate = (nextVoice + i) % voices.Length;
                if (!voices[candidate].isPlaying) { index = candidate; break; }
            }
            AudioSource source = voices[index];
            filters[index].cutoffFrequency = cutoff;
            nextVoice = (index + 1) % voices.Length;
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
