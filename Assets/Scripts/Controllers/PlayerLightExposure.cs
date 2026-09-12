using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Shared player light probe for darkness ambience and stress.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed class PlayerLightExposure : MonoBehaviour
    {
        [SerializeField] private Vector3 sampleOffset = new Vector3(0f, 1f, 0f);
        [Tooltip("Exposure considered fully lit. Ambient and directional moonlight are ignored.")]
        [SerializeField, Min(0.0001f)] private float fullyLitExposure = 3f;
        [SerializeField] private LayerMask environmentMask = ~0;
        private readonly GameplayLightSampler sampler = new GameplayLightSampler();
        public float Exposure { get; private set; }
        public float Darkness => isActiveAndEnabled
            ? 1f - Mathf.Clamp01(Exposure / Mathf.Max(0.0001f, fullyLitExposure)) : 0f;
        private void OnEnable() => RefreshExposure();
        private void Update() => RefreshExposure();
        public void RefreshExposure()
        {
            Exposure = sampler.Sample(transform.TransformPoint(sampleOffset), transform, environmentMask);
        }
    }

    internal sealed class GameplayLightSampler
    {
        private Light[] lights;
        private float refreshTime;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        public float Sample(Vector3 point, Transform player, LayerMask environmentMask)
        {
            if (player == null) return 0f;
            if (lights == null || Time.time >= refreshTime)
            {
                lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                refreshTime = Time.time + 1f;
            }
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
