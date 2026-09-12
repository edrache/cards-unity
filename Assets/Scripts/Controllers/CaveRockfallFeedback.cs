using UnityEngine;
using UnityEngine.Audio;

namespace CardsUnity.Controllers
{
    /// <summary>Scene-authored presentation settings shared by the generator's disposable rockfall traps.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProceduralCave))]
    public sealed class CaveRockfallFeedback : MonoBehaviour
    {
        [Header("Camera shake")]
        [Tooltip("Optional explicit follow camera; otherwise resolves the main camera.")]
        [SerializeField] private CharacterFollowCamera followCamera;
        [SerializeField] private bool shakeEnabled = true;
        [Min(0f)] [SerializeField] private float collapseAmplitude = 0.14f;
        [Min(0f)] [SerializeField] private float landingAmplitude = 0.09f;
        [Min(0.01f)] [SerializeField] private float shakeDuration = 0.65f;
        [Min(0.1f)] [SerializeField] private float shakeFrequency = 24f;
        [Tooltip("Full strength within this distance of the player, fading to zero at Shake Max Distance.")]
        [Min(0f)] [SerializeField] private float shakeFullDistance = 4f;
        [Min(0.1f)] [SerializeField] private float shakeMaxDistance = 18f;

        [Header("Collapse audio")]
        [Tooltip("One non-empty entry is selected randomly per collapse. Empty entries are ignored.")]
        [SerializeField] private AudioClip[] collapseClips = System.Array.Empty<AudioClip>();
        // Preserve assignments made before the single clip became a list.
        [SerializeField, HideInInspector] private AudioClip collapseClip;
        [SerializeField] private AudioMixerGroup output;
        [Range(0f, 1f)] [SerializeField] private float volume = 0.8f;
        [SerializeField] private Vector2 pitchRange = new Vector2(0.85f, 1.15f);
        [Min(0.1f)] [SerializeField] private float audioMinDistance = 3f;
        [Min(0.1f)] [SerializeField] private float audioMaxDistance = 28f;

        // Presentation must not consume the trap's seeded geometry/timing stream.
        private readonly System.Random audioRandom = new System.Random();

        private void OnValidate()
        {
            if (collapseClip == null) return;
            if (collapseClips == null || collapseClips.Length == 0)
                collapseClips = new[] { collapseClip };
            collapseClip = null;
        }

        private AudioClip SelectCollapseClip()
        {
            int count = 0;
            if (collapseClips != null)
                foreach (AudioClip clip in collapseClips)
                    if (clip != null) count++;
            // Also supports legacy serialized instances loaded directly in a build.
            if (count == 0) return collapseClip;
            int selected = audioRandom.Next(count);
            foreach (AudioClip clip in collapseClips)
                if (clip != null && selected-- == 0) return clip;
            return null;
        }

        internal void PlayCollapse(Transform trap, Vector3 position, Transform player)
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            PlayShake(position, player, collapseAmplitude);
            if (volume <= 0f) return;
            AudioClip selectedClip = SelectCollapseClip();
            if (selectedClip == null) return;
            var voice = new GameObject("Rockfall Collapse Audio");
            voice.transform.SetParent(trap, false);
            voice.transform.position = position;
            var source = voice.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = selectedClip;
            source.outputAudioMixerGroup = output;
            source.volume = volume;
            float low = Mathf.Clamp(Mathf.Min(pitchRange.x, pitchRange.y), 0.1f, 3f);
            float high = Mathf.Clamp(Mathf.Max(pitchRange.x, pitchRange.y), low, 3f);
            source.pitch = Mathf.Lerp(low, high, (float)audioRandom.NextDouble());
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = Mathf.Max(0.1f, audioMinDistance);
            source.maxDistance = Mathf.Max(source.minDistance + 0.1f, audioMaxDistance);
            source.Play();
            Destroy(voice, selectedClip.length / source.pitch + 0.2f);
        }

        internal void PlayLanding(Vector3 position, Transform player)
        {
            if (Application.isPlaying && isActiveAndEnabled)
                PlayShake(position, player, landingAmplitude);
        }

        private void PlayShake(Vector3 position, Transform player, float amplitude)
        {
            if (!shakeEnabled || player == null) return;
            if (followCamera == null && Camera.main != null)
                followCamera = Camera.main.GetComponent<CharacterFollowCamera>();
            if (followCamera == null) return;
            float full = Mathf.Max(0f, shakeFullDistance);
            float maximum = Mathf.Max(full + 0.1f, shakeMaxDistance);
            float attenuation = 1f - Mathf.InverseLerp(full, maximum, Vector3.Distance(position, player.position));
            followCamera.Shake(amplitude * attenuation, shakeDuration, shakeFrequency);
        }
    }
}
