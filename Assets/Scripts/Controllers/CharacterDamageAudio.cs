using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Plays 2D player injury and death one-shots from health events.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public sealed class CharacterDamageAudio : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CharacterHealth health;

        [Header("Injury")]
        [Tooltip("One clip is chosen at random for each nonlethal accepted hit. Null entries are ignored.")]
        [SerializeField] private AudioClip[] injuryClips = System.Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f)] private float injuryVolume = 1f;
        [SerializeField, Min(0.01f)] private float injuryMinimumPitch = 0.9f;
        [SerializeField, Min(0.01f)] private float injuryMaximumPitch = 1.1f;

        [Header("Death")]
        [Tooltip("One clip is chosen at random when the character dies. Null entries are ignored.")]
        [SerializeField] private AudioClip[] deathClips = System.Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f)] private float deathVolume = 1f;
        [SerializeField, Min(0.01f)] private float deathMinimumPitch = 0.9f;
        [SerializeField, Min(0.01f)] private float deathMaximumPitch = 1.1f;

        private AudioSource source;
        private bool deathPlayed;
        private readonly System.Random random = new System.Random();

        private void Awake()
        {
            ClampSettings();
            source = GetComponent<AudioSource>();
            ConfigureSource();
        }

        private void OnEnable()
        {
            if (source == null) source = GetComponent<AudioSource>();
            ConfigureSource();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (source != null) source.Stop();
        }

        private void OnValidate()
        {
            ClampSettings();
        }

        private void ClampSettings()
        {
            injuryVolume = Mathf.Clamp01(injuryVolume);
            deathVolume = Mathf.Clamp01(deathVolume);
            injuryMinimumPitch = Mathf.Clamp(injuryMinimumPitch, 0.01f, 3f);
            injuryMaximumPitch = Mathf.Clamp(injuryMaximumPitch, injuryMinimumPitch, 3f);
            deathMinimumPitch = Mathf.Clamp(deathMinimumPitch, 0.01f, 3f);
            deathMaximumPitch = Mathf.Clamp(deathMaximumPitch, deathMinimumPitch, 3f);
        }

        private void Subscribe()
        {
            if (health == null) return;
            health.Damaged -= PlayInjury;
            health.Damaged += PlayInjury;
            health.Died -= PlayDeath;
            health.Died += PlayDeath;
        }

        private void Unsubscribe()
        {
            if (health == null) return;
            health.Damaged -= PlayInjury;
            health.Died -= PlayDeath;
        }

        private void ConfigureSource()
        {
            if (source == null) return;
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            // Keep outputAudioMixerGroup unchanged: routing belongs to this dedicated source object.
        }

        private void PlayInjury()
        {
            if (deathPlayed) return;
            PlayRandom(injuryClips, injuryVolume, injuryMinimumPitch, injuryMaximumPitch);
        }

        private void PlayDeath()
        {
            if (deathPlayed) return;
            deathPlayed = true;
            if (source == null) return;

            source.Stop();
            PlayRandom(deathClips, deathVolume, deathMinimumPitch, deathMaximumPitch);
        }

        private void PlayRandom(AudioClip[] clips, float volume, float minimumPitch, float maximumPitch)
        {
            if (source == null || clips == null || clips.Length == 0) return;

            int validCount = 0;
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] != null) validCount++;
            if (validCount == 0) return;

            int choice = random.Next(validCount);
            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip clip = clips[i];
                if (clip == null) continue;
                if (choice-- != 0) continue;

                source.pitch = Mathf.Lerp(minimumPitch, maximumPitch, (float)random.NextDouble());
                source.PlayOneShot(clip, volume);
                return;
            }
        }
    }
}
