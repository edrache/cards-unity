using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Looping player ambience driven by local gameplay illumination.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public sealed class DarknessWhisperAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip track;
        [SerializeField] private Transform player;
        [SerializeField] private Vector3 sampleOffset = new Vector3(0f, 1f, 0f);
        [SerializeField, Range(0f, 1f)] private float maximumVolume = 1f;
        [Tooltip("Exposure at which whispers are silent. Point/spot intensity uses squared range falloff; moonlight is ignored, as in creature gameplay.")]
        [SerializeField, Min(0.0001f)] private float silentExposure = 3f;
        [Tooltip("Seconds required to fade across the full volume range.")]
        [SerializeField, Min(0.01f)] private float fadeDuration = 3f;
        [SerializeField] private LayerMask environmentMask = ~0;

        private AudioSource source;
        [Tooltip("Optional shared player probe; legacy probe settings below remain the fallback.")]
        [SerializeField] private PlayerLightExposure lightExposure;
        private readonly GameplayLightSampler sampler = new GameplayLightSampler();
        public float Exposure { get; private set; }
        public float TargetVolume { get; private set; }

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.clip = track;
            source.volume = 0f;
        }

        private void OnEnable()
        {
            source.volume = 0f;
            if (source.clip != null) source.Play();
        }

        private void OnDisable()
        {
            source.Stop();
            source.volume = 0f;
        }

        private void Update()
        {
            bool sharedProbe = lightExposure != null && lightExposure.isActiveAndEnabled;
            Exposure = sharedProbe ? lightExposure.Exposure
                : player != null ? sampler.Sample(player.TransformPoint(sampleOffset), player, environmentMask) : 0f;
            TargetVolume = sharedProbe ? maximumVolume * lightExposure.Darkness
                : player != null ? maximumVolume * (1f - Mathf.Clamp01(Exposure / Mathf.Max(0.0001f, silentExposure))) : 0f;
            source.volume = Mathf.MoveTowards(source.volume, TargetVolume,
                Time.deltaTime / Mathf.Max(0.01f, fadeDuration));
        }

    }
}
