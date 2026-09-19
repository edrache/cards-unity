using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CardsUnity.Controllers
{
    /// <summary>One controller per cave; immutable chunk meshes and GPU-only blade deformation.</summary>
    [ExecuteAlways]
    public sealed class CaveGrass : MonoBehaviour
    {
        public const float ChunkSize = 6f;
        private static readonly int PlayerId = Shader.PropertyToID("_GrassPlayer");
        private static readonly int TrailId = Shader.PropertyToID("_GrassTrail");
        private static readonly int ViewOriginId = Shader.PropertyToID("_GrassViewOrigin");
        private static readonly int DistanceId = Shader.PropertyToID("_GrassDrawDistance");
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<MeshRenderer> chunks = new List<MeshRenderer>();
        private Material material;
        private Transform player;
        private Vector3 trail;
        private float bendRadius;
        private float drawDistanceSquared;
        private bool resourcesReleased;
        public int BladeCount { get; private set; }
        public int ChunkCount => chunks.Count;

        public void Initialize(Dictionary<Vector2Int, List<Vector3>> patches, CaveGenerationSettings settings,
            Transform target, System.Random random)
        {
            player = target;
            trail = player != null ? player.position : Vector3.zero;
            bendRadius = settings.grassBendRadius;
            material = new Material(settings.grassMaterial) { name = "Cave grass (runtime)", hideFlags = HideFlags.DontSave };
            material.SetFloat(DistanceId, settings.grassDrawDistance);
            drawDistanceSquared = settings.grassDrawDistance * settings.grassDrawDistance;
            foreach (var patch in patches)
            {
                Vector3 origin = new Vector3(patch.Key.x * ChunkSize, 0f, patch.Key.y * ChunkSize);
                var vertices = new List<Vector3>();
                var normals = new List<Vector3>();
                var roots = new List<Vector4>();
                var uv = new List<Vector2>();
                var indices = new List<int>();
                foreach (Vector3 root in patch.Value)
                {
                    float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    Vector3 side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector3 normal = Vector3.Cross(side, Vector3.up);
                    float height = settings.grassHeight * Mathf.Lerp(1f - settings.grassHeightVariation, 1f + settings.grassHeightVariation, (float)random.NextDouble());
                    float width = settings.grassWidth * 0.5f * Mathf.Lerp(1f - settings.grassWidthVariation, 1f + settings.grassWidthVariation, (float)random.NextDouble());
                    Vector3 lean = normal * height * Mathf.Lerp(-0.25f, 0.25f, (float)random.NextDouble());
                    int start = vertices.Count;
                    for (int level = 0; level < 4; level++)
                    {
                        // Keep the leaf broad up to a short, bevelled cap instead of a long needle tip.
                        // Cap depth follows width, so tall narrow blades still have a rounded silhouette.
                        float shoulder = 1f - Mathf.Min(0.2f, width * 0.75f / height);
                        float t = level == 0 ? 0f : level == 1 ? 0.55f : level == 2 ? shoulder : 1f;
                        float widthScale = level == 0 ? 1f : level == 1 ? 0.95f : level == 2 ? 0.85f : 0.35f;
                        for (int edge = 0; edge < 2; edge++)
                        {
                            vertices.Add(root - origin + Vector3.up * (height * t) + lean * t * t + side * ((edge * 2 - 1) * width * widthScale));
                            normals.Add(normal);
                            roots.Add(new Vector4(root.x - origin.x, root.y, root.z - origin.z, height));
                            // Preserve the original six random draws per blade when adding the cap vertices.
                            uv.Add(new Vector2(level < 3 ? (float)random.NextDouble() : 0f, t));
                        }
                    }
                    for (int level = 0; level < 3; level++)
                    {
                        int a = start + level * 2;
                        indices.Add(a); indices.Add(a + 2); indices.Add(a + 1);
                        indices.Add(a + 1); indices.Add(a + 2); indices.Add(a + 3);
                    }
                    BladeCount++;
                }
                var mesh = new Mesh { name = "Grass chunk", hideFlags = HideFlags.DontSave };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetUVs(1, roots);
                mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
                Bounds bounds = mesh.bounds;
                bounds.Expand(settings.grassHeight * (1f + settings.grassHeightVariation) * 1.7f + 0.1f); // Include shader displacement in culling bounds.
                mesh.bounds = bounds;
                mesh.UploadMeshData(true);
                meshes.Add(mesh);
                var go = new GameObject("Grass chunk " + patch.Key);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = origin;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                chunks.Add(renderer);
            }
            UpdateBend(0f);
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += CullForCamera;
            RenderPipelineManager.endCameraRendering += RestoreAfterCamera;
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= CullForCamera;
            RenderPipelineManager.endCameraRendering -= RestoreAfterCamera;
            foreach (var chunk in chunks) if (chunk != null) chunk.enabled = false;
        }
        private void LateUpdate() => UpdateBend(Time.deltaTime);
        private void UpdateBend(float deltaTime)
        {
            if (material == null) return;
            bool active = player != null && player.gameObject.activeInHierarchy;
            Vector3 position = active ? player.position : Vector3.zero;
            if ((trail - position).sqrMagnitude > 25f) trail = position; // Teleports must not flatten intervening rooms.
            trail = Vector3.Lerp(trail, position, 1f - Mathf.Exp(-deltaTime / 0.35f));
            material.SetVector(PlayerId, new Vector4(position.x, position.y, position.z, active ? bendRadius : 0f));
            material.SetVector(TrailId, new Vector4(trail.x, trail.y, trail.z, 0f));
        }
        private void CullForCamera(ScriptableRenderContext context, Camera camera)
        {
            if (material == null) return;
            // The gameplay camera can sit farther away than the entire grass draw radius.
            Vector3 position = camera.cameraType == CameraType.Game && player != null
                ? player.position : camera.transform.position;
            material.SetVector(ViewOriginId, new Vector4(position.x, position.y, position.z, 1f));
            foreach (var chunk in chunks)
                if (chunk != null) chunk.enabled = chunk.bounds.SqrDistance(position) < drawDistanceSquared;
        }
        private void RestoreAfterCamera(ScriptableRenderContext context, Camera camera)
        {
            // A UI/Scene camera must not leave a persistent enabled state for another view or Inspector.
            foreach (var chunk in chunks) if (chunk != null) chunk.enabled = true;
        }
        private void OnDestroy() => ReleaseGeneratedResources();

        public void ReleaseGeneratedResources()
        {
            if (resourcesReleased) return;
            resourcesReleased = true;
            // Recover references from children too: nonserialized lists reset after an editor domain reload.
            var owned = new HashSet<Object>();
            foreach (var filter in GetComponentsInChildren<MeshFilter>(true)) owned.Add(filter.sharedMesh);
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true)) owned.Add(renderer.sharedMaterial);
            foreach (Mesh mesh in meshes) owned.Add(mesh);
            owned.Add(material);
            foreach (Object resource in owned) Release(resource);
            meshes.Clear();
            chunks.Clear();
            material = null;
        }
        private static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
