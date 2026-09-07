using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CardsUnity.Controllers
{
    /// <summary>Connected cave carved from a continuous field, with a level collision floor.</summary>
    [ExecuteAlways]
    public sealed class ProceduralCave : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField, Range(1, 24)] private int roomCount = 6;
        [SerializeField] private int seed = 173;
        [SerializeField, Range(4f, 8f)] private float roomRadius = 5.5f;
        [Tooltip("Radius variation around Room Radius. Zero gives equal base sizes; one ranges from 55% to 145%.")]
        [SerializeField, Range(0f, 1f)] private float roomSizeVariation = 0.85f;
        [Tooltip("Fraction of spare neighbouring connections opened as loops. Zero makes a branching tree.")]
        [SerializeField, Range(0f, 1f)] private float extraConnections = 0.4f;
        [SerializeField, Range(2.5f, 5f)] private float corridorWidth = 3.5f;
        [Header("Rock")]
        [SerializeField, Range(0f, 1f)] private float irregularity = 0.65f;
        [SerializeField, Range(2f, 5f)] private float wallHeight = 3.2f;
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material wallMaterial;
        [Header("Player")]
        [Tooltip("Moved to the first room after rebuilding, including when Play Mode starts.")]
        [SerializeField] private Transform player;

        private readonly List<Vector2> rooms = new List<Vector2>();
        private readonly List<float> radii = new List<float>();
        private readonly List<Vector2[]> corridors = new List<Vector2[]>();
        private GameObject generated;
        private Mesh floorMesh, wallMesh;
        private bool rebuildPending;
        private float noiseOffset;
        public int RoomCount => roomCount;
        public IReadOnlyList<Vector2> RoomCenters => rooms;
        public IReadOnlyList<Vector2[]> Corridors => corridors;
        public IReadOnlyList<float> RoomRadii => radii;
        public Vector3 SpawnPosition => transform.TransformPoint(Vector3.up * 0.1f);

        private void OnEnable() { rebuildPending = true; }
        private void OnValidate()
        {
            roomCount = Mathf.Clamp(roomCount, 1, 24);
            roomRadius = Mathf.Clamp(roomRadius, 4f, 8f);
            roomSizeVariation = Mathf.Clamp01(roomSizeVariation);
            extraConnections = Mathf.Clamp01(extraConnections);
            corridorWidth = Mathf.Clamp(corridorWidth, 2.5f, 5f);
            irregularity = Mathf.Clamp01(irregularity);
            wallHeight = Mathf.Clamp(wallHeight, 2f, 5f);
            rebuildPending = true;
        }
        private void Update() { if (rebuildPending) Rebuild(); }
        private void OnDisable() { Clear(); }
        private static void Dispose(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        private void Clear()
        {
            if (generated != null) generated.SetActive(false);
            Dispose(generated); Dispose(floorMesh); Dispose(wallMesh);
            generated = null; floorMesh = null; wallMesh = null;
        }

        [ContextMenu("Rebuild Cave")]
        public void Rebuild()
        {
            rebuildPending = false;
            Clear(); rooms.Clear(); radii.Clear(); corridors.Clear();
            var random = new System.Random(seed);
            noiseOffset = (float)random.NextDouble() * 1000f;
            GenerateLayout(random);
            const float cell = 0.8f;
            // Include every room and curved passage, rather than assuming a one-dimensional chain.
            Vector2 min = rooms[0], max = rooms[0];
            for (int i = 0; i < rooms.Count; i++)
            {
                Vector2 extent = Vector2.one * (radii[i] * 1.2f + 3f);
                min = Vector2.Min(min, rooms[i] - extent);
                max = Vector2.Max(max, rooms[i] + extent);
            }
            foreach (var corridor in corridors)
                foreach (var point in corridor)
                {
                    Vector2 extent = Vector2.one * (corridorWidth * 0.5f + 3f);
                    min = Vector2.Min(min, point - extent);
                    max = Vector2.Max(max, point + extent);
                }
            int nx = Mathf.CeilToInt((max.x - min.x) / cell);
            int nz = Mathf.CeilToInt((max.y - min.y) / cell);
            var values = new float[nx + 1, nz + 1];
            for (int x = 0; x <= nx; x++)
                for (int z = 0; z <= nz; z++) values[x, z] = Field(min + new Vector2(x, z) * cell);
            var floor = new MeshData(); var walls = new MeshData();
            for (int x = 0; x < nx; x++)
                for (int z = 0; z < nz; z++)
                {
                    Vector2 a = min + new Vector2(x, z) * cell;
                    Vector2 b = a + Vector2.up * cell;
                    Vector2 c = a + Vector2.one * cell;
                    Vector2 d = a + Vector2.right * cell;
                    Clip(a, b, c, values[x,z], values[x,z+1], values[x+1,z+1], floor, walls);
                    Clip(a, c, d, values[x,z], values[x+1,z+1], values[x+1,z], floor, walls);
                }
            generated = new GameObject("Generated Cave (preview)");
            generated.hideFlags = HideFlags.DontSave;
            generated.transform.SetParent(transform, false);
            floorMesh = floor.Build("Cave floor"); wallMesh = walls.Build("Cave walls");
            CreateSurface("Level floor", floorMesh, floorMaterial);
            CreateSurface("Rock walls", wallMesh, wallMaterial);
            if (player != null)
            {
                var controller = player.GetComponent<CharacterController>();
                bool enabledBefore = controller != null && controller.enabled;
                if (enabledBefore) controller.enabled = false;
                player.position = SpawnPosition;
                if (enabledBefore) controller.enabled = true;
            }
        }

        private void GenerateLayout(System.Random random)
        {
            int columns = Mathf.CeilToInt(Mathf.Sqrt(roomCount));
            float spacing = roomRadius * 4.3f;
            var edges = new List<Vector2Int>();
            for (int i = 0; i < roomCount; i++)
            {
                int x = i % columns, z = i / columns;
                Vector2 jitter = i == 0 ? Vector2.zero : new Vector2(
                    (float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f) * spacing * 0.16f;
                rooms.Add(new Vector2(x, z) * spacing + jitter);
                // Stratified sizes guarantee a visible spread even in small layouts.
                float size = roomCount == 1 ? 0.5f : (i + (float)random.NextDouble()) / roomCount;
                radii.Add(Mathf.Max(2.5f, roomRadius * (1f + (size * 2f - 1f) * 0.45f * roomSizeVariation)));
                if (x > 0) edges.Add(new Vector2Int(i - 1, i));
                if (z > 0) edges.Add(new Vector2Int(i - columns, i));
            }
            for (int i = radii.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                float radius = radii[i]; radii[i] = radii[j]; radii[j] = radius;
            }
            for (int i = edges.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                Vector2Int edge = edges[i]; edges[i] = edges[j]; edges[j] = edge;
            }
            // Randomized spanning tree guarantees reachability; spare edges add real cycles.
            var groups = new int[roomCount];
            for (int i = 0; i < roomCount; i++) groups[i] = i;
            var spare = new List<Vector2Int>();
            foreach (var edge in edges)
            {
                int from = groups[edge.x], to = groups[edge.y];
                if (from == to) { spare.Add(edge); continue; }
                AddCorridor(edge, random, spacing);
                for (int i = 0; i < groups.Length; i++) if (groups[i] == to) groups[i] = from;
            }
            int loops = Mathf.RoundToInt(spare.Count * extraConnections);
            if (extraConnections > 0f && spare.Count > 0) loops = Mathf.Max(1, loops);
            for (int i = 0; i < loops; i++) AddCorridor(spare[i], random, spacing);
        }

        private void AddCorridor(Vector2Int edge, System.Random random, float spacing)
        {
            Vector2 a = rooms[edge.x], b = rooms[edge.y];
            Vector2 normal = Vector2.Perpendicular((b - a).normalized);
            float bend = ((float)random.NextDouble() - 0.5f) * spacing * 0.3f;
            var points = new Vector2[9];
            for (int i = 0; i < points.Length; i++)
            {
                float t = i / 8f;
                points[i] = Vector2.Lerp(a, b, t) + normal * (Mathf.Sin(t * Mathf.PI) * bend);
            }
            points[0] = a;
            points[points.Length - 1] = b;
            corridors.Add(points);
        }

        private float Field(Vector2 p)
        {
            float value = float.NegativeInfinity;
            for (int i = 0; i < rooms.Count; i++)
            {
                Vector2 delta = p - rooms[i];
                float angle = Mathf.Atan2(delta.y, delta.x);
                float lobes = Mathf.Sin(angle * 3f + noiseOffset + i) * 0.13f
                    + Mathf.Sin(angle * 5f - noiseOffset) * 0.07f;
                value = Mathf.Max(value, radii[i] * (1f + lobes * irregularity) - delta.magnitude);
            }
            foreach (var corridor in corridors)
            for (int i = 1; i < corridor.Length; i++)
            {
                Vector2 a = corridor[i - 1], ab = corridor[i] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                value = Mathf.Max(value, corridorWidth * 0.5f - Vector2.Distance(p, a + t * ab));
            }
            // Perturb only the boundary: minimum corridor clearance remains above 1.8 metres.
            return value + (Mathf.PerlinNoise(p.x * 0.42f + noiseOffset, p.y * 0.42f + noiseOffset) - 0.5f) * irregularity * 0.7f;
        }
        private void CreateSurface(string label, Mesh mesh, Material material)
        {
            var go = new GameObject(label) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(generated.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        private void Clip(Vector2 a, Vector2 b, Vector2 c, float fa, float fb, float fc, MeshData floor, MeshData walls)
        {
            if (fa < 0f && fb < 0f && fc < 0f) return;
            var polygon = new List<Vector2>(4);
            var boundary = new List<Vector2>(2);
            Edge(a, b, fa, fb, polygon, boundary);
            Edge(b, c, fb, fc, polygon, boundary);
            Edge(c, a, fc, fa, polygon, boundary);
            for (int i = 1; i + 1 < polygon.Count; i++) floor.Triangle(V(polygon[0], 0f), V(polygon[i], 0f), V(polygon[i+1], 0f));
            if (boundary.Count != 2) return;
            Vector2 p = boundary[0], q = boundary[1];
            Vector2 outward = Vector2.Perpendicular(q - p).normalized;
            Vector2 midpoint = (p + q) * 0.5f;
            if (Field(midpoint + outward * 0.1f) > Field(midpoint - outward * 0.1f)) outward = -outward;
            // Shared noise at boundary vertices keeps neighbouring rock panels watertight.
            Vector3 p0 = V(p, 0f), q0 = V(q, 0f);
            Vector3 p1 = V(p + RockOffset(p) * 0.35f, Height(p) * 0.48f), q1 = V(q + RockOffset(q) * 0.35f, Height(q) * 0.48f);
            Vector3 p2 = V(p + RockOffset(p), Height(p)), q2 = V(q + RockOffset(q), Height(q));
            walls.Quad(p0, q0, q1, p1, -V(outward, 0f));
            walls.Quad(p1, q1, q2, p2, -V(outward, 0f));
            Vector3 plip = V(RockOffset(p).normalized * 0.9f, 0f);
            Vector3 qlip = V(RockOffset(q).normalized * 0.9f, 0f);
            walls.Quad(p2, q2, q2 + qlip, p2 + plip, Vector3.up);
            walls.Quad(p0 + plip, q0 + qlip, q2 + qlip, p2 + plip, V(outward, 0f));
        }
        private Vector2 RockOffset(Vector2 p)
        {
            const float e = 0.08f;
            Vector2 gradient = new Vector2(Field(p + Vector2.right * e) - Field(p - Vector2.right * e),
                Field(p + Vector2.up * e) - Field(p - Vector2.up * e));
            return -gradient.normalized * (0.35f + Mathf.PerlinNoise(p.x + noiseOffset, p.y) * irregularity);
        }
        private float Height(Vector2 p) => wallHeight + (Mathf.PerlinNoise(p.x * 0.6f + noiseOffset, p.y * 0.6f) - 0.5f) * 1.3f;
        private static Vector3 V(Vector2 p, float y) => new Vector3(p.x, y, p.y);
        private static void Edge(Vector2 a, Vector2 b, float fa, float fb, List<Vector2> polygon, List<Vector2> boundary)
        {
            if (fa >= 0f) polygon.Add(a);
            if ((fa >= 0f) == (fb >= 0f)) return;
            Vector2 point = Vector2.Lerp(a, b, fa / (fa - fb));
            polygon.Add(point); boundary.Add(point);
        }
        private sealed class MeshData
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();
            private readonly List<Vector2> uv = new List<Vector2>();
            public void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                int n = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                uv.Add(new Vector2(a.x + a.y, a.z + a.y) * 0.25f);
                uv.Add(new Vector2(b.x + b.y, b.z + b.y) * 0.25f);
                uv.Add(new Vector2(c.x + c.y, c.z + c.y) * 0.25f);
                triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                if (Vector3.Dot(Vector3.Cross(b-a, c-a), normal) >= 0f) { Triangle(a,b,c); Triangle(a,c,d); }
                else { Triangle(a,c,b); Triangle(a,d,c); }
            }
            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uv);
                mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
            }
        }
    }
}
