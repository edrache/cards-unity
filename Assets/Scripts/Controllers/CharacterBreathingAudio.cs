using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Plays a non-spatial breathing loop whose pitch follows the player's stress.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public sealed class CharacterBreathingAudio : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AudioClip track;
        [SerializeField] private CharacterStress stress;

        [Tooltip("Optional movement source; defaults to the character on the stress object.")]
        [SerializeField] private ProceduralCharacter character;

        [Header("Playback")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
        [Tooltip("Seconds of fade-in when breathing starts. Zero starts at the full volume.")]
        [SerializeField, Min(0f)] private float fadeInDuration = 0.2f;
        [Tooltip("Playback speed/pitch at 0 stress.")]
        [SerializeField, Min(0.01f)] private float minimumPlaybackSpeed = 1f;
        [Tooltip("Playback speed/pitch at 100 stress.")]
        [SerializeField, Min(0.01f)] private float maximumPlaybackSpeed = 1.6f;
        [Tooltip("Seconds used to smoothly follow a change in stress.")]
        [SerializeField, Min(0.01f)] private float pitchResponseTime = 0.15f;

        [Tooltip("Multiplies the stress-driven playback speed while actually running. Final pitch is capped at 3.")]
        [SerializeField, Range(1f, 3f)] private float runningPlaybackMultiplier = 1.3f;

        private AudioSource source;
        private float fade;
        private float currentPitch;

        private void Awake()
        {
            ValidateSettings();
            if (character == null && stress != null) character = stress.GetComponent<ProceduralCharacter>();
            ConfigureSource();
        }

        private void OnEnable()
        {
            ConfigureSource();
            ResetPlayback();
        }

        private void OnDisable()
        {
            if (source != null) source.Stop();
        }

        private void LateUpdate()
        {
            if (source == null) return;

            float targetPitch = Mathf.Lerp(minimumPlaybackSpeed, maximumPlaybackSpeed, StressWeight);
            if (character != null && character.IsRunning) targetPitch *= runningPlaybackMultiplier;
            targetPitch = Mathf.Min(3f, targetPitch);
            float blend = 1f - Mathf.Exp(-Time.deltaTime / pitchResponseTime);
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, blend);
            source.pitch = currentPitch;

            fade = fadeInDuration > 0.01f
                ? Mathf.MoveTowards(fade, 1f, Time.deltaTime / fadeInDuration)
                : 1f;
            source.volume = volume * fade;
        }

        private float StressWeight => stress != null && stress.isActiveAndEnabled
            ? Mathf.Clamp01(stress.Stress / 100f)
            : 0f;

        private void ConfigureSource()
        {
            if (source == null) source = GetComponent<AudioSource>();
            source.clip = track;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            // Do not assign outputAudioMixerGroup: routing belongs to the dedicated source object.
        }

        private void ResetPlayback()
        {
            if (source == null) return;

            fade = 0f;
            currentPitch = minimumPlaybackSpeed;
            source.pitch = currentPitch;
            source.volume = 0f;
            if (source.clip != null && !source.isPlaying) source.Play();
        }

        private void OnValidate()
        {
            ValidateSettings();
        }

        private void ValidateSettings()
        {
            runningPlaybackMultiplier = Mathf.Clamp(runningPlaybackMultiplier, 1f, 3f);
            volume = Mathf.Clamp01(volume);
            minimumPlaybackSpeed = Mathf.Clamp(minimumPlaybackSpeed, 0.01f, 3f);
            maximumPlaybackSpeed = Mathf.Clamp(maximumPlaybackSpeed, minimumPlaybackSpeed, 3f);
            pitchResponseTime = Mathf.Max(0.01f, pitchResponseTime);
            fadeInDuration = Mathf.Max(0f, fadeInDuration);
        }
    }
}
