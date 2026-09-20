using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>An irregular cave-local pit, optionally hidden beneath a one-shot collapsing floor.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class CavePitTrap : MonoBehaviour
    {
        private const float MinimumCollapseSeconds = 0.05f;
        private const float CoverReleaseFraction = 0.25f;
        private const float TriggerHeightTolerance = 1.35f;
        private const float LethalDrop = 0.8f;
        private const float CrackWidth = 0.025f;

        private sealed class CoverShard
        {
            public Transform transform;
            public Vector3 origin;
            public Quaternion rotation;
            public Vector3 rotationAxis;
            public float delay;
            public float turnAngle;
        }

        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private readonly List<Material> generatedMaterials = new List<Material>();
        private readonly List<CoverShard> coverShards = new List<CoverShard>();

        private ProceduralCave cave;
        private Transform player;
        private Vector2[] boundary;
        private MeshCollider coverCollider;
        private GameObject generatedRoot;
        private float collapseSeconds;
        private float collapseTime;
        private float depth;
        private float victimFallSpeed;
        private bool configured;
        private bool movingVictim;

        public bool IsOpen { get; private set; }
        public bool HasTriggered { get; private set; }
        public float CollapseSeconds => collapseSeconds;

        /// <param name="owner">Cave providing the local floor height field.</param>
        /// <param name="polygon">Clockwise convex polygon in cave-local XZ coordinates.</param>
        public void Initialize(ProceduralCave owner, Vector2[] polygon, Transform target, bool covered,
            float collapseDuration, Material floorMaterial, Material wallMaterial, float pitDepth)
        {
            ReleaseGeneratedResources();

            cave = owner;
            player = target;
            boundary = SanitizeBoundary(polygon);
            collapseSeconds = Mathf.Max(MinimumCollapseSeconds, collapseDuration);
            depth = Mathf.Max(1f, pitDepth);
            collapseTime = 0f;
            victimFallSpeed = 0f;
            movingVictim = false;
            HasTriggered = false;
            IsOpen = !covered;
            configured = cave != null && boundary.Length >= 3 && Mathf.Abs(SignedArea(boundary)) > 0.001f;
            if (!configured) return;

            // The generator supplies clockwise boundaries, but accepting either orientation makes the
            // component safe to use directly from focused checks and future content rules.
            if (SignedArea(boundary) > 0f) System.Array.Reverse(boundary);

            generatedRoot = new GameObject("Pit Geometry") { hideFlags = HideFlags.DontSave };
            generatedRoot.transform.SetParent(transform, false);

            BuildShaft(wallMaterial, floorMaterial);
            if (covered) BuildCover(floorMaterial);
        }

        private void Update()
        {
            if (Application.isPlaying) Tick(Time.deltaTime);
        }

        /// <summary>Advances the cover and lethal fall and may be called directly by focused checks.</summary>
        public void Tick(float dt)
        {
            if (!configured || dt <= 0f) return;
            dt = Mathf.Max(0f, dt);

            if (!HasTriggered && !IsOpen && PlayerIsOnCover())
            {
                HasTriggered = true;
                collapseTime = 0f;
            }

            if (HasTriggered)
            {
                collapseTime = Mathf.Min(collapseSeconds, collapseTime + dt);
                if (!IsOpen && collapseTime >= collapseSeconds * CoverReleaseFraction)
                {
                    if (coverCollider != null) coverCollider.enabled = false;
                    IsOpen = true;
                }
                AnimateCover();
            }

            if (movingVictim)
            {
                MoveVictimToBottom(dt);
                return;
            }

            if (!IsOpen || player == null) return;
            Vector3 local = CaveLocalPosition(player.position);
            Vector2 point = new Vector2(local.x, local.z);
            if (!ContainsPoint(point) || local.y > cave.FloorHeight(point) - LethalDrop) return;

            CharacterHealth health = player.GetComponent<CharacterHealth>();
            if (health != null && health.TryKill())
            {
                movingVictim = true;
                victimFallSpeed = 0f;
                MoveVictimToBottom(dt);
            }
        }

        private bool PlayerIsOnCover()
        {
            if (player == null) return false;
            CaveExit caveExit = player.GetComponent<CaveExit>();
            CharacterHealth health = player.GetComponent<CharacterHealth>();
            if ((caveExit != null && caveExit.IsCompleted) || (health != null && health.IsDead)) return false;

            Vector3 local = CaveLocalPosition(player.position);
            Vector2 point = new Vector2(local.x, local.z);
            return ContainsPoint(point)
                && Mathf.Abs(local.y - cave.FloorHeight(point)) <= TriggerHeightTolerance;
        }

        private void MoveVictimToBottom(float dt)
        {
            if (player == null)
            {
                movingVictim = false;
                return;
            }

            Vector3 local = CaveLocalPosition(player.position);
            Vector2 point = new Vector2(local.x, local.z);
            float bottom = cave.FloorHeight(point) - depth + 0.04f;
            if (local.y <= bottom + 0.001f)
            {
                movingVictim = false;
                return;
            }

            victimFallSpeed = Mathf.Min(victimFallSpeed + Mathf.Abs(Physics.gravity.y) * dt, 18f);
            float fall = Mathf.Min(local.y - bottom, victimFallSpeed * dt);
            Vector3 worldMotion = cave.transform.TransformVector(Vector3.down * fall);
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null && controller.enabled && player.gameObject.activeInHierarchy)
                controller.Move(worldMotion);
            else
                player.position += worldMotion;
        }

        private void BuildShaft(Material wallMaterial, Material floorMaterial)
        {
            Vector2 centre = PolygonCentre();
            var wallVertices = new List<Vector3>(boundary.Length * 4);
            var wallTriangles = new List<int>(boundary.Length * 6);
            for (int i = 0; i < boundary.Length; i++)
            {
                Vector2 a = boundary[i];
                Vector2 b = boundary[(i + 1) % boundary.Length];
                Vector3 topA = Surface(a, 0f);
                Vector3 topB = Surface(b, 0f);
                Vector3 bottomA = Surface(a, -depth);
                Vector3 bottomB = Surface(b, -depth);
                int first = wallVertices.Count;
                wallVertices.Add(topA);
                wallVertices.Add(topB);
                wallVertices.Add(bottomB);
                wallVertices.Add(bottomA);

                // Clockwise input places the interior to the right of each edge. These triangles face
                // that interior so both rendering and one-sided collision work from inside the shaft.
                wallTriangles.Add(first);
                wallTriangles.Add(first + 1);
                wallTriangles.Add(first + 2);
                wallTriangles.Add(first);
                wallTriangles.Add(first + 2);
                wallTriangles.Add(first + 3);
            }
            Mesh walls = CreateMesh("Pit Shaft", wallVertices, wallTriangles);
            CreateSurfaceObject("Shaft Walls", walls, wallMaterial, true);

            var bottomVertices = new List<Vector3>(boundary.Length + 1) { Surface(centre, -depth) };
            for (int i = 0; i < boundary.Length; i++) bottomVertices.Add(Surface(boundary[i], -depth));
            var bottomTriangles = new List<int>(boundary.Length * 3);
            for (int i = 0; i < boundary.Length; i++)
            {
                bottomTriangles.Add(0);
                bottomTriangles.Add(i + 1);
                bottomTriangles.Add((i + 1) % boundary.Length + 1);
            }
            Mesh bottom = CreateMesh("Pit Bottom", bottomVertices, bottomTriangles);
            Material darkBottom = CreateDarkMaterial(wallMaterial != null ? wallMaterial : floorMaterial);
            CreateSurfaceObject("Dark Bottom", bottom, darkBottom, true);
        }

        private void BuildCover(Material floorMaterial)
        {
            Vector2 centre = PolygonCentre();
            Vector3 centreSurface = Surface(centre, 0.012f);
            var colliderVertices = new List<Vector3>(boundary.Length + 1) { centreSurface };
            for (int i = 0; i < boundary.Length; i++) colliderVertices.Add(Surface(boundary[i], 0.012f));
            var colliderTriangles = new List<int>(boundary.Length * 3);

            for (int i = 0; i < boundary.Length; i++)
            {
                int next = (i + 1) % boundary.Length;
                colliderTriangles.Add(0);
                colliderTriangles.Add(i + 1);
                colliderTriangles.Add(next + 1);
            }

            Vector2[] seeds = CreateCoverSeeds(centre);
            for (int i = 0; i < seeds.Length; i++)
            {
                List<Vector2> cell = BuildVoronoiCell(seeds, i);
                if (cell.Count < 3) continue;

                Vector2 shardCentre2 = Vector2.zero;
                for (int v = 0; v < cell.Count; v++) shardCentre2 += cell[v];
                shardCentre2 /= cell.Count;

                var worldLocalVertices = new List<Vector3>(cell.Count);
                for (int v = 0; v < cell.Count; v++)
                {
                    Vector2 inset = InsetPoint(cell[v], shardCentre2, CrackWidth);
                    worldLocalVertices.Add(Surface(inset, 0.018f));
                }
                Vector3 shardCentre3 = Vector3.zero;
                for (int v = 0; v < worldLocalVertices.Count; v++) shardCentre3 += worldLocalVertices[v];
                shardCentre3 /= worldLocalVertices.Count;

                var shardVertices = new List<Vector3>(worldLocalVertices.Count);
                for (int v = 0; v < worldLocalVertices.Count; v++)
                    shardVertices.Add(worldLocalVertices[v] - shardCentre3);
                var shardTriangles = new List<int>((shardVertices.Count - 2) * 3);
                for (int v = 1; v + 1 < shardVertices.Count; v++)
                {
                    shardTriangles.Add(0);
                    shardTriangles.Add(v);
                    shardTriangles.Add(v + 1);
                }
                Mesh shardMesh = CreateMesh("Pit Cover Shard " + (i + 1), shardVertices, shardTriangles, shardCentre3);

                GameObject shardObject = CreateSurfaceObject("Cover Shard " + (i + 1), shardMesh, floorMaterial, false);
                shardObject.transform.localPosition = shardCentre3;
                coverShards.Add(new CoverShard
                {
                    transform = shardObject.transform,
                    origin = shardCentre3,
                    rotation = Quaternion.identity,
                    rotationAxis = new Vector3(0.55f + i * 0.17f, 0.2f, -0.7f + i * 0.11f).normalized,
                    delay = collapseSeconds * (0.025f + 0.15f * (i % 3) / 2f),
                    turnAngle = 75f + i % 4 * 19f
                });
            }

            Mesh supportMesh = CreateMesh("Pit Cover Support", colliderVertices, colliderTriangles);
            var support = new GameObject("Cover Support") { hideFlags = HideFlags.DontSave };
            support.transform.SetParent(generatedRoot.transform, false);
            coverCollider = support.AddComponent<MeshCollider>();
            coverCollider.sharedMesh = supportMesh;
        }

        private void AnimateCover()
        {
            for (int i = 0; i < coverShards.Count; i++)
            {
                CoverShard shard = coverShards[i];
                if (shard.transform == null) continue;
                float duration = Mathf.Max(0.001f, collapseSeconds - shard.delay);
                float t = Mathf.Clamp01((collapseTime - shard.delay) / duration);
                float eased = t * t;
                shard.transform.localPosition = shard.origin + Vector3.down * ((depth + 0.35f) * eased);
                shard.transform.localRotation = shard.rotation
                    * Quaternion.AngleAxis(shard.turnAngle * eased, shard.rotationAxis);
            }
        }

        private GameObject CreateSurfaceObject(string label, Mesh mesh, Material material, bool addCollider)
        {
            var surface = new GameObject(label) { hideFlags = HideFlags.DontSave };
            surface.transform.SetParent(generatedRoot.transform, false);
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            surface.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (addCollider) surface.AddComponent<MeshCollider>().sharedMesh = mesh;
            return surface;
        }

        private Mesh CreateMesh(string label, List<Vector3> vertices, List<int> triangles,
            Vector3 uvPositionOffset = default)
        {
            var mesh = new Mesh { name = label, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            var uv = new List<Vector2>(vertices.Count);
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 point = vertices[i] + uvPositionOffset;
                uv.Add(new Vector2(point.x + point.y, point.z + point.y) * 0.25f);
            }
            mesh.SetUVs(0, uv);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            generatedMeshes.Add(mesh);
            return mesh;
        }

        private Material CreateDarkMaterial(Material source)
        {
            Material material = null;
            if (source != null)
            {
                material = new Material(source);
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader != null) material = new Material(shader);
            }
            if (material == null) return null;

            material.name = "Pit Bottom (runtime)";
            material.hideFlags = HideFlags.DontSave;
            Color dark = new Color(0.018f, 0.014f, 0.012f, 1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", dark);
            if (material.HasProperty("_Color")) material.SetColor("_Color", dark);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
            generatedMaterials.Add(material);
            return material;
        }

        private Vector3 Surface(Vector2 point, float verticalOffset)
        {
            return new Vector3(point.x, cave.FloorHeight(point) + verticalOffset, point.y);
        }

        private Vector3 CaveLocalPosition(Vector3 worldPosition)
        {
            return cave.transform.InverseTransformPoint(worldPosition);
        }

        private Vector2 PolygonCentre()
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < boundary.Length; i++) sum += boundary[i];
            return sum / boundary.Length;
        }

        private bool ContainsPoint(Vector2 point)
        {
            // A convex clockwise polygon contains the point when it stays on the right of every edge.
            for (int i = 0; i < boundary.Length; i++)
            {
                Vector2 a = boundary[i];
                Vector2 b = boundary[(i + 1) % boundary.Length];
                Vector2 edge = b - a;
                float cross = edge.x * (point.y - a.y) - edge.y * (point.x - a.x);
                if (cross > 0.001f) return false;
            }
            return true;
        }

        private static Vector2 InsetPoint(Vector2 point, Vector2 centre, float distance)
        {
            float length = Vector2.Distance(point, centre);
            return length > 0.0001f ? Vector2.Lerp(point, centre, Mathf.Min(0.45f, distance / length)) : point;
        }

        private Vector2[] CreateCoverSeeds(Vector2 centre)
        {
            const int seedCount = 8;
            var seeds = new Vector2[seedCount];
            // A slightly off-centre cell prevents every crack from converging on one obvious hub.
            seeds[0] = centre * 0.88f + boundary[1 % boundary.Length] * 0.07f
                + boundary[(boundary.Length * 3 / 5) % boundary.Length] * 0.05f;
            for (int i = 1; i < seedCount; i++)
            {
                float around = (i - 1) * boundary.Length / (float)(seedCount - 1);
                int a = Mathf.FloorToInt(around) % boundary.Length;
                int b = (a + 1) % boundary.Length;
                float blend = around - Mathf.Floor(around);
                Vector2 rim = Vector2.Lerp(boundary[a], boundary[b], blend);
                float radial = 0.48f + (i % 3) * 0.055f;
                seeds[i] = Vector2.Lerp(centre, rim, radial);
            }
            return seeds;
        }

        private List<Vector2> BuildVoronoiCell(Vector2[] seeds, int index)
        {
            var cell = new List<Vector2>(boundary);
            for (int other = 0; other < seeds.Length && cell.Count >= 3; other++)
            {
                if (other == index) continue;
                Vector2 direction = seeds[other] - seeds[index];
                if (direction.sqrMagnitude < 0.000001f) continue;
                Vector2 midpoint = (seeds[index] + seeds[other]) * 0.5f;
                cell = ClipToBisector(cell, midpoint, direction);
            }
            return cell;
        }

        private static List<Vector2> ClipToBisector(List<Vector2> polygon, Vector2 midpoint, Vector2 direction)
        {
            var clipped = new List<Vector2>(polygon.Count + 1);
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 current = polygon[i];
                Vector2 next = polygon[(i + 1) % polygon.Count];
                float currentSide = Vector2.Dot(current - midpoint, direction);
                float nextSide = Vector2.Dot(next - midpoint, direction);
                bool currentInside = currentSide <= 0f;
                bool nextInside = nextSide <= 0f;
                if (currentInside) clipped.Add(current);
                if (currentInside == nextInside) continue;
                float t = currentSide / (currentSide - nextSide);
                clipped.Add(Vector2.LerpUnclamped(current, next, t));
            }
            return clipped;
        }

        private static Vector2[] SanitizeBoundary(Vector2[] polygon)
        {
            if (polygon == null || polygon.Length < 3) return System.Array.Empty<Vector2>();
            var clean = new List<Vector2>(polygon.Length);
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 point = polygon[i];
                if (clean.Count == 0 || (point - clean[clean.Count - 1]).sqrMagnitude > 0.000001f)
                    clean.Add(point);
            }
            if (clean.Count > 2 && (clean[0] - clean[clean.Count - 1]).sqrMagnitude <= 0.000001f)
                clean.RemoveAt(clean.Count - 1);
            return clean.ToArray();
        }

        private static float SignedArea(Vector2[] polygon)
        {
            float twiceArea = 0f;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Length];
                twiceArea += a.x * b.y - b.x * a.y;
            }
            return twiceArea * 0.5f;
        }

        private void ReleaseGeneratedResources()
        {
            coverCollider = null;
            coverShards.Clear();
            if (generatedRoot != null) DestroyOwnedObject(generatedRoot);
            generatedRoot = null;
            for (int i = 0; i < generatedMeshes.Count; i++) DestroyOwnedObject(generatedMeshes[i]);
            generatedMeshes.Clear();
            for (int i = 0; i < generatedMaterials.Count; i++) DestroyOwnedObject(generatedMaterials[i]);
            generatedMaterials.Clear();
        }

        private static void DestroyOwnedObject(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        private void OnDestroy() => ReleaseGeneratedResources();
    }
}
