using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>A valuable world prop whose glints reveal it only in local light.</summary>
    public sealed class Treasure : MonoBehaviour
    {
        [SerializeField, Min(1)] private int value = 100;
        [SerializeField, Min(0.0001f)] private float lightThreshold = 0.005f;
        [SerializeField] private ParticleSystem glints;
        private MeshRenderer[] bodyRenderers;
        private static Light[] lights;
        private static float nextLightRefresh;
        private readonly RaycastHit[] hits = new RaycastHit[64];
        private float nextSample;
        public int Value => value;
        public float Exposure { get; private set; }
        public bool IsIlluminated { get; private set; }

        public void SetValue(int amount) => value = Mathf.Max(1, amount);
        private void OnValidate() { value = Mathf.Max(1, value); lightThreshold = Mathf.Max(0.0001f, lightThreshold); }
        private void OnEnable()
        {
            nextSample = 0f;
            lights = null;
            bodyRenderers = GetComponentsInChildren<MeshRenderer>();
            PositionGlints();
        }

        private void LateUpdate() => PositionGlints();

        private void PositionGlints()
        {
            if (glints == null || bodyRenderers == null || bodyRenderers.Length == 0) return;
            Bounds bounds = bodyRenderers[0].bounds;
            for (int i = 1; i < bodyRenderers.Length; i++)
                bounds.Encapsulate(bodyRenderers[i].bounds);
            // Keep the entire emission sphere above the model, even when the cup lies on its side.
            float radius = glints.shape.radius * Mathf.Max(glints.transform.lossyScale.x,
                Mathf.Max(glints.transform.lossyScale.y, glints.transform.lossyScale.z));
            glints.transform.SetPositionAndRotation(
                new Vector3(bounds.center.x, bounds.max.y + radius + 0.08f, bounds.center.z),
                Quaternion.identity);
        }
        private void OnDisable()
        {
            IsIlluminated = false;
            if (glints != null) glints.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        private void Update()
        {
            if (Time.time < nextSample) return;
            nextSample = Time.time + 0.15f;
            RefreshIllumination();
        }
        public void RefreshIllumination()
        {
            if (lights == null || Time.time >= nextLightRefresh)
            {
                lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                nextLightRefresh = Time.time + 1f;
            }
            Exposure = 0f;
            Vector3 point = transform.TransformPoint(new Vector3(0f, 0.55f, 0f));
            foreach (var light in lights)
            {
                if (light == null || !light.isActiveAndEnabled || light.intensity <= 0f
                    || (light.type != LightType.Point && light.type != LightType.Spot)
                    || (light.cullingMask & (1 << gameObject.layer)) == 0) continue;
                Vector3 delta = light.transform.position - point;
                float distance = delta.magnitude;
                if (distance >= light.range || light.range <= 0f) continue;
                if (light.type == LightType.Spot && Vector3.Angle(light.transform.forward, -delta) > light.spotAngle * 0.5f) continue;
                bool blocked = false;
                if (light.shadows != LightShadows.None && distance > 0.001f)
                {
                    int count = Physics.RaycastNonAlloc(point, delta / distance, hits, distance, ~0, QueryTriggerInteraction.Ignore);
                    var torch = light.GetComponentInParent<HandheldTorch>();
                    Transform emitter = torch != null ? torch.transform : light.transform;
                    blocked = count == hits.Length;
                    for (int i = 0; i < count && !blocked; i++)
                        if (!hits[i].transform.IsChildOf(transform) && !hits[i].transform.IsChildOf(emitter)) blocked = true;
                }
                if (!blocked) Exposure += light.intensity * Mathf.Pow(1f - distance / light.range, 2f);
            }
            // Hysteresis avoids chatter at the edge of flickering torchlight.
            IsIlluminated = Exposure >= lightThreshold * (IsIlluminated ? 0.7f : 1f);
            if (glints == null) return;
            if (IsIlluminated && !glints.isPlaying) glints.Play();
            else if (!IsIlluminated) glints.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
