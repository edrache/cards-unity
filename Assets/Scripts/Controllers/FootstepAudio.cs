using UnityEngine;
using UnityEngine.Audio;

namespace CardsUnity.Controllers
{
    // Plays one-shot footstep samples on the gait's foot contacts. Pitch is randomized per step and
    // the volume follows the blended Sneak/Walk/Run weights, so mixed styles stay audible in between.
    [RequireComponent(typeof(CartoonCharacterGait))]
    public sealed class FootstepAudio : MonoBehaviour
    {
        [Header("Clips")]
        [SerializeField] private AudioClip leftStep;
        [SerializeField] private AudioClip rightStep;
        [Tooltip("Optional mixer group for both footstep voices.")]
        [SerializeField] private AudioMixerGroup output;

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

        [Header("Pitch")]
        [SerializeField, Range(0.5f, 1.5f)] private float sneakPitch = 0.93f;
        [SerializeField, Range(0.5f, 1.5f)] private float walkPitch = 1f;
        [SerializeField, Range(0.5f, 1.5f)] private float runPitch = 1.08f;
        [Tooltip("Random pitch offset applied to every single step.")]
        [SerializeField, Range(0f, 0.4f)] private float pitchJitter = 0.07f;

        [Header("Spatialization")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.85f;
        [SerializeField, Min(0.1f)] private float minDistance = 1.5f;
        [SerializeField, Min(0.2f)] private float maxDistance = 25f;

        [Header("Guards")]
        [Tooltip("Shortest time between two steps, in seconds. Protects against duplicate contacts at very high cadence.")]
        [SerializeField, Range(0f, 0.3f)] private float minimumInterval = 0.06f;

        private CartoonCharacterGait gait;
        private AudioSource leftSource, rightSource;
        private float lastStepTime = -1f;

        private void Awake()
        {
            gait = GetComponent<CartoonCharacterGait>();
            leftSource = CreateSource("Footstep Left");
            rightSource = CreateSource("Footstep Right");
        }

        private void OnEnable()
        {
            if (gait != null) gait.Footstep += OnFootstep;
        }

        private void OnDisable()
        {
            if (gait != null) gait.Footstep -= OnFootstep;
        }

        private AudioSource CreateSource(string sourceName)
        {
            var holder = new GameObject(sourceName);
            holder.transform.SetParent(transform, false);
            var source = holder.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = spatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = Mathf.Max(maxDistance, minDistance + 0.1f);
            source.dopplerLevel = 0f;
            if (output != null) source.outputAudioMixerGroup = output;
            return source;
        }

        private void OnFootstep(GaitFootstep step)
        {
            AudioSource source = step.LeftFoot ? leftSource : rightSource;
            AudioClip clip = step.LeftFoot ? leftStep : rightStep;
            if (source == null || clip == null) return;
            if (minimumInterval > 0f && lastStepTime >= 0f && Time.time - lastStepTime < minimumInterval) return;
            lastStepTime = Time.time;
            // Sneak and Run pull away from the walking defaults; a blended mix lands in between.
            float sneak = Mathf.Clamp01(step.SneakAmount);
            float run = Mathf.Clamp01(step.RunAmount);
            float volume = Mathf.Lerp(Mathf.Lerp(walkVolume, sneakVolume, sneak), runVolume, run);
            float pitch = Mathf.Lerp(Mathf.Lerp(walkPitch, sneakPitch, sneak), runPitch, run);
            volume *= masterVolume * Mathf.Lerp(1f - speedInfluence, 1f, Mathf.Clamp01(step.Intensity));
            source.transform.position = step.Position;
            source.pitch = Mathf.Max(0.05f, pitch + Random.Range(-pitchJitter, pitchJitter));
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private void OnValidate()
        {
            maxDistance = Mathf.Max(maxDistance, minDistance + 0.1f);
        }
    }
}
