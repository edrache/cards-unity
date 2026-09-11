using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>A valuable world prop whose glints reveal it only in local light.</summary>
    public sealed class Treasure : MonoBehaviour
    {
        // Generated cave props use DontSave and are omitted by Unity's global object search.
        private static readonly List<Treasure> activeTreasures = new List<Treasure>();
        public static IReadOnlyList<Treasure> ActiveTreasures => activeTreasures;

        [SerializeField, Min(1)] private int value = 100;
        [SerializeField, Min(0.0001f)] private float lightThreshold = 0.005f;
        [SerializeField] private ParticleSystem glints;
        [SerializeField, Min(0.01f)] private float minimumSizeDistance = 10f;
        [SerializeField, Range(0f, 1f)] private float distantSizeFraction = 0.01f;
        private Transform player;
        private float nextPlayerSearch;
        private bool originalSizeEnabled;
        private ParticleSystem.MinMaxCurve originalSizeX, originalSizeY, originalSizeZ;
        private MeshRenderer[] bodyRenderers;
        private static Light[] lights;
        private static float nextLightRefresh;
        private readonly RaycastHit[] hits = new RaycastHit[64];
        private float nextSample;
        public int Value => value;
        public string DisplayName
        {
            get
            {
                string itemName = name.Split('(')[0].Trim();
                switch (itemName)
                {
                    case "Golden Chalice": return "Złoty kielich";
                    case "Royal Crown": return "Królewska korona";
                    case "Royal Orb": return "Królewskie jabłko";
                    case "Golden Idol": return "Złoty bożek";
                    case "Silver Flask": return "Srebrna flasza";
                    case "Gold Coin": return "Złota moneta";
                    default: return itemName;
                }
            }
        }
        public float Exposure { get; private set; }
        public bool IsIlluminated { get; private set; }

        public void SetValue(int amount) => value = Mathf.Max(1, amount);
        private void OnValidate() { value = Mathf.Max(1, value); lightThreshold = Mathf.Max(0.0001f, lightThreshold); }
        private void OnEnable()
        {
            if (!activeTreasures.Contains(this)) activeTreasures.Add(this);
            nextSample = 0f;
            lights = null;
            bodyRenderers = GetComponentsInChildren<MeshRenderer>();
            if (glints != null)
            {
                var size = glints.sizeOverLifetime;
                originalSizeEnabled = size.enabled;
                originalSizeX = size.x;
                originalSizeY = size.y;
                originalSizeZ = size.z;
            }
            PositionGlints();
        }

        private void LateUpdate()
        {
            PositionGlints();
            UpdateDistanceSize();
        }

        private void UpdateDistanceSize()
        {
            if (glints == null) return;
            if (player == null && Time.time >= nextPlayerSearch)
            {
                nextPlayerSearch = Time.time + 1f;
                foreach (var candidate in FindObjectsByType<ProceduralCharacter>(FindObjectsSortMode.None))
                    if (candidate.gameObject.scene == gameObject.scene) { player = candidate.transform; break; }
            }
            float distance = player != null ? Vector3.Distance(player.position, transform.position) : 0f;
            float factor = Mathf.Lerp(1f, distantSizeFraction,
                Mathf.Clamp01(distance / Mathf.Max(0.01f, minimumSizeDistance)));
            var size = glints.sizeOverLifetime;
            size.enabled = true;
            // This module affects existing particles too and leaves the authored start size intact.
            size.x = ScaleSize(originalSizeEnabled ? originalSizeX : new ParticleSystem.MinMaxCurve(1f), factor);
            if (size.separateAxes)
            {
                size.y = ScaleSize(originalSizeEnabled ? originalSizeY : new ParticleSystem.MinMaxCurve(1f), factor);
                size.z = ScaleSize(originalSizeEnabled ? originalSizeZ : new ParticleSystem.MinMaxCurve(1f), factor);
            }
        }

        private static ParticleSystem.MinMaxCurve ScaleSize(ParticleSystem.MinMaxCurve curve, float factor)
        {
            if (curve.mode == ParticleSystemCurveMode.Constant) curve.constant *= factor;
            else if (curve.mode == ParticleSystemCurveMode.TwoConstants)
            {
                curve.constantMin *= factor;
                curve.constantMax *= factor;
            }
            else curve.curveMultiplier *= factor;
            return curve;
        }

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
            activeTreasures.Remove(this);
            IsIlluminated = false;
            if (glints != null)
            {
                glints.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var size = glints.sizeOverLifetime;
                size.enabled = originalSizeEnabled;
                size.x = originalSizeX;
                if (size.separateAxes) { size.y = originalSizeY; size.z = originalSizeZ; }
            }
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
