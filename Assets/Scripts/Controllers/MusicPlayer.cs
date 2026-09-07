using UnityEngine;
using UnityEngine.Audio;

namespace CardsUnity.Controllers
{
    // Scene background music: one looping track, faded in so it does not slam in on load.
    [RequireComponent(typeof(AudioSource))]
    public sealed class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip track;
        [Tooltip("Optional mixer group. Leave empty to play straight into the default output.")]
        [SerializeField] private AudioMixerGroup output;
        [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
        [Tooltip("Seconds of fade-in when playback starts. Zero starts at the full volume.")]
        [SerializeField, Range(0f, 20f)] private float fadeInDuration = 4f;
        [Tooltip("Seconds of fade-out used by Stop().")]
        [SerializeField, Range(0f, 20f)] private float fadeOutDuration = 2f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool playOnStart = true;

        private AudioSource source;
        private float fade;
        private bool stopping;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.clip = track;
            source.loop = loop;
            source.playOnAwake = false;
            // Music is not part of the world, so keep it out of the 3D panning and rolloff.
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            if (output != null) source.outputAudioMixerGroup = output;
            source.volume = 0f;
        }

        private void Start()
        {
            if (playOnStart) Play();
        }

        public void Play()
        {
            if (source == null || source.clip == null) return;
            stopping = false;
            if (!source.isPlaying) source.Play();
        }

        public void Stop()
        {
            stopping = true;
        }

        private void Update()
        {
            if (source == null) return;
            float target = stopping ? 0f : 1f;
            float duration = stopping ? fadeOutDuration : fadeInDuration;
            fade = duration > 0.01f
                ? Mathf.MoveTowards(fade, target, Time.unscaledDeltaTime / duration)
                : target;
            source.volume = fade * volume;
            if (stopping && fade <= 0f && source.isPlaying) source.Stop();
        }

        private void OnValidate()
        {
            // Keep live tuning audible while the scene is playing.
            if (source == null) return;
            source.loop = loop;
            source.volume = fade * volume;
        }
    }
}
