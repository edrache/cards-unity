using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CardsUnity.Controllers
{
    /// <summary>A small generated waterfall, including its visual pool and a lightweight flame contact volume.</summary>
    [ExecuteAlways, DefaultExecutionOrder(200), DisallowMultipleComponent]
    public sealed class CaveWater : MonoBehaviour
    {
        private const float MinimumSize = 0.01f;
        private static readonly List<CaveWater> activeWaters = new List<CaveWater>();

        [SerializeField] private Material waterMaterial;
        [SerializeField, Min(MinimumSize)] private float width = 0.8f;
        [SerializeField, Min(MinimumSize)] private float height = 2.5f;
        [SerializeField, Min(MinimumSize)] private float depth = 0.35f;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh generatedMesh;
        private Bounds localWaterBounds;
        private bool rebuildPending;

        /// <summary>Builds the waterfall at its floor-origin with the supplied shared material and dimensions.</summary>
        public void Configure(Material material, float waterfallWidth, float waterfallHeight, float waterfallDepth)
        {
            waterMaterial = material;
            width = Mathf.Max(MinimumSize, waterfallWidth);
            height = Mathf.Max(MinimumSize, waterfallHeight);
            depth = Mathf.Max(MinimumSize, waterfallDepth);
            rebuildPending = false;
            RebuildMesh();
        }

        private void OnEnable()
        {
            if (!activeWaters.Contains(this)) activeWaters.Add(this);
            RebuildMesh();
        }

        private void OnDisable()
        {
            activeWaters.Remove(this);
        }

        private void OnValidate()
        {
            width = Mathf.Max(MinimumSize, width);
            height = Mathf.Max(MinimumSize, height);
            depth = Mathf.Max(MinimumSize, depth);
            // OnValidate also runs while Unity is importing or deserializing this component. Component creation
            // is deferred until the next editor update, when it is safe to restore the generated mesh.
            rebuildPending = true;
        }

        private void LateUpdate()
        {
            if (rebuildPending)
            {
                rebuildPending = false;
                RebuildMesh();
            }
            if (Application.isPlaying) HandheldTorch.CheckWaterContacts();
        }

        private void OnDestroy()
        {
            activeWaters.Remove(this);
            ReleaseGeneratedMesh();
        }

        /// <summary>Tests a world-space flame segment, including a spherical flame radius, against this water volume.</summary>
        public bool IntersectsFlameSegment(Vector3 from, Vector3 to, float radius)
        {
            if (!isActiveAndEnabled) return false;
            Vector3 localFrom = transform.InverseTransformPoint(from);
            Vector3 localTo = transform.InverseTransformPoint(to);
            Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
            float worldRadius = Mathf.Max(0f, radius);
            // A world-space sphere maps to an ellipsoid. These row lengths give its exact local AABB extents.
            Vector3 localRadius = new Vector3(
                worldRadius * new Vector3(worldToLocal.m00, worldToLocal.m01, worldToLocal.m02).magnitude,
                worldRadius * new Vector3(worldToLocal.m10, worldToLocal.m11, worldToLocal.m12).magnitude,
                worldRadius * new Vector3(worldToLocal.m20, worldToLocal.m21, worldToLocal.m22).magnitude);
            Bounds sweptBounds = localWaterBounds;
            sweptBounds.Expand(localRadius * 2f);
            return SegmentIntersectsBounds(localFrom, localTo, sweptBounds);
        }

        /// <summary>Returns true when a flame segment reaches any enabled waterfall or pool.</summary>
        public static bool TouchesWater(Vector3 from, Vector3 to, float radius)
        {
            for (int i = activeWaters.Count - 1; i >= 0; i--)
            {
                CaveWater water = activeWaters[i];
                if (water == null)
                {
                    activeWaters.RemoveAt(i);
                    continue;
                }
                if (water.CanExtinguishFlame && water.IntersectsFlameSegment(from, to, radius)) return true;
            }
            return false;
        }

        private void RebuildMesh()
        {
            EnsureComponents();
            if (generatedMesh == null)
            {
                generatedMesh = new Mesh { name = "Cave waterfall mesh", hideFlags = HideFlags.DontSave };
                generatedMesh.MarkDynamic();
            }

            float halfWidth = width * 0.5f;
            float halfDepth = depth * 0.5f;
            float poolRadius = Mathf.Max(halfWidth, halfDepth) * 1.15f;
            // Two crossed falling quads and one horizontal pool quad. UV1 marks pool vertices for the shader.
            Vector3[] vertices =
            {
                new Vector3(-halfWidth, 0f, 0f), new Vector3(halfWidth, 0f, 0f), new Vector3(-halfWidth, height, 0f), new Vector3(halfWidth, height, 0f),
                new Vector3(0f, 0f, -halfDepth), new Vector3(0f, 0f, halfDepth), new Vector3(0f, height, -halfDepth), new Vector3(0f, height, halfDepth),
                new Vector3(-poolRadius, 0.002f, -poolRadius), new Vector3(poolRadius, 0.002f, -poolRadius), new Vector3(-poolRadius, 0.002f, poolRadius), new Vector3(poolRadius, 0.002f, poolRadius)
            };
            Vector2[] uv =
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f)
            };
            Vector2[] metadata =
            {
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.zero, Vector2.one, Vector2.one, Vector2.one, Vector2.one
            };
            int[] triangles = { 0, 2, 1, 1, 2, 3, 4, 6, 5, 5, 6, 7, 8, 10, 9, 9, 10, 11 };
            generatedMesh.Clear();
            generatedMesh.vertices = vertices;
            generatedMesh.uv = uv;
            generatedMesh.uv2 = metadata;
            generatedMesh.triangles = triangles;
            generatedMesh.RecalculateBounds();
            // The cosmetic pool is deliberately wider than its gameplay volume. Flame contact is the actual
            // waterfall footprint: floor-origin, one width wide and one depth deep.
            localWaterBounds = new Bounds(new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth));
            meshFilter.sharedMesh = generatedMesh;
            meshRenderer.sharedMaterial = waterMaterial;
        }

        private bool CanExtinguishFlame => waterMaterial != null && meshRenderer != null && meshRenderer.enabled &&
            meshRenderer.gameObject.activeInHierarchy;

        private void EnsureComponents()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
                if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                meshRenderer.lightProbeUsage = LightProbeUsage.Off;
                meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }
        }

        private void ReleaseGeneratedMesh()
        {
            if (generatedMesh == null) return;
            if (meshFilter != null && meshFilter.sharedMesh == generatedMesh) meshFilter.sharedMesh = null;
            if (Application.isPlaying) Destroy(generatedMesh); else DestroyImmediate(generatedMesh);
            generatedMesh = null;
        }

        private static bool SegmentIntersectsBounds(Vector3 from, Vector3 to, Bounds bounds)
        {
            Vector3 direction = to - from;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float entry = 0f;
            float exit = 1f;
            for (int axis = 0; axis < 3; axis++)
            {
                float start = from[axis];
                float delta = direction[axis];
                if (Mathf.Abs(delta) < 0.000001f)
                {
                    if (start < min[axis] || start > max[axis]) return false;
                    continue;
                }
                float inverse = 1f / delta;
                float near = (min[axis] - start) * inverse;
                float far = (max[axis] - start) * inverse;
                if (near > far) { float swap = near; near = far; far = swap; }
                entry = Mathf.Max(entry, near);
                exit = Mathf.Min(exit, far);
                if (entry > exit) return false;
            }
            return true;
        }
    }
}
