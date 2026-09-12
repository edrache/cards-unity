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
        private Light[] lights;
        private float refreshTime;
        private readonly RaycastHit[] hits = new RaycastHit[32];
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
            refreshTime = 0f;
            if (source.clip != null) source.Play();
        }

        private void OnDisable()
        {
            source.Stop();
            source.volume = 0f;
        }

        private void Update()
        {
            refreshTime -= Time.deltaTime;
            if (lights == null || refreshTime <= 0f)
            {
                lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                refreshTime = 1f;
            }
            Exposure = player != null ? SampleExposure(player.TransformPoint(sampleOffset)) : 0f;
            TargetVolume = player != null
                ? maximumVolume * (1f - Mathf.Clamp01(Exposure / Mathf.Max(0.0001f, silentExposure))) : 0f;
            source.volume = Mathf.MoveTowards(source.volume, TargetVolume,
                Time.deltaTime / Mathf.Max(0.01f, fadeDuration));
        }

        private float SampleExposure(Vector3 point)
        {
            float exposure = 0f;
            foreach (var light in lights)
            {
                if (light == null || !light.isActiveAndEnabled || light.intensity <= 0f || light.range <= 0f
                    || (light.type != LightType.Point && light.type != LightType.Spot)
                    || (light.cullingMask & (1 << player.gameObject.layer)) == 0) continue;
                Vector3 delta = light.transform.position - point;
                float distance = delta.magnitude;
                if (distance >= light.range) continue;
                if (light.type == LightType.Spot
                    && Vector3.Angle(light.transform.forward, -delta) > light.spotAngle * 0.5f) continue;
                if (light.shadows != LightShadows.None && distance > 0.001f)
                {
                    int count = Physics.RaycastNonAlloc(point, delta / distance, hits, distance,
                        environmentMask, QueryTriggerInteraction.Ignore);
                    // Avoid losing an occluder when a dense ray exceeds the reusable buffer.
                    var rayHits = count == hits.Length
                        ? Physics.RaycastAll(point, delta / distance, distance, environmentMask, QueryTriggerInteraction.Ignore)
                        : hits;
                    if (rayHits != hits) count = rayHits.Length;
                    var torch = light.GetComponentInParent<HandheldTorch>();
                    Transform emitter = torch != null ? torch.transform : light.transform;
                    bool blocked = false;
                    for (int i = 0; i < count; i++)
                    {
                        Transform blocker = rayHits[i].transform;
                        if (blocker.IsChildOf(player) || blocker.IsChildOf(emitter)) continue;
                        blocked = true;
                        break;
                    }
                    if (blocked) continue;
                }
                float falloff = 1f - distance / light.range;
                exposure += light.intensity * falloff * falloff;
            }
            return exposure;
        }
    }
}
