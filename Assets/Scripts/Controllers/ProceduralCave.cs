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
        [SerializeField, Range(0f, 1f)] private float corridorWinding = 0.8f;
        [Tooltip("Side tunnels branching from the middle of existing passages, including dead ends.")]
        [SerializeField, Range(0f, 1f)] private float branchDensity = 0.5f;
        [SerializeField, Range(8f, 20f)] private float entranceLength = 12f;
        [Header("Elevation")]
        [Tooltip("Continuous ramps without overlapping floors. Disable to restore the flat cave.")]
        [SerializeField] private bool enableElevation;
        [Tooltip("Maximum height difference between the lowest and highest parts of the height field.")]
        [SerializeField, Range(1f, 12f)] private float elevationRange = 6f;
        [SerializeField, Range(5f, 20f)] private float maximumSlope = 15f;
        [Tooltip("Visible wall height in elevated caves. Collision walls retain their full height.")]
        [SerializeField, Range(0.5f, 1.5f)] private float cutawayWallHeight = 1.2f;
        [Header("Rock")]
        [SerializeField, Range(0f, 1f)] private float irregularity = 0.65f;
        [SerializeField, Range(2f, 5f)] private float wallHeight = 3.2f;
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material wallMaterial;
        [Header("Player")]
        [Tooltip("Moved to the entrance tunnel after rebuilding, including when Play Mode starts.")]
        [SerializeField] private Transform player;

        private readonly List<Vector2> rooms = new List<Vector2>();
        private readonly List<float> radii = new List<float>();
        private readonly List<Vector2[]> corridors = new List<Vector2[]>();
        private readonly List<Rect> corridorBounds = new List<Rect>();
        private Vector2 entrance;
        private Vector2 entranceForward;
        private GameObject generated;
        private Mesh floorMesh, wallMesh, wallCollisionMesh;
        private bool rebuildPending;
        private float noiseOffset;
        public int RoomCount => roomCount;
        public IReadOnlyList<Vector2> RoomCenters => rooms;
        public IReadOnlyList<Vector2[]> Corridors => corridors;
        public IReadOnlyList<float> RoomRadii => radii;
        public Vector3 SpawnPosition => transform.TransformPoint(Surface(entrance, 0.1f));

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
            corridorWinding = Mathf.Clamp01(corridorWinding);
            branchDensity = Mathf.Clamp01(branchDensity);
            entranceLength = Mathf.Clamp(entranceLength, 8f, 20f);
            elevationRange = Mathf.Clamp(elevationRange, 1f, 12f);
            maximumSlope = Mathf.Clamp(maximumSlope, 5f, 20f);
            cutawayWallHeight = Mathf.Clamp(cutawayWallHeight, 0.5f, 1.5f);
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
            Dispose(generated); Dispose(floorMesh); Dispose(wallMesh); Dispose(wallCollisionMesh);
            generated = null; floorMesh = null; wallMesh = null; wallCollisionMesh = null;
        }

        [ContextMenu("Rebuild Cave")]
        public void Rebuild()
        {
            rebuildPending = false;
            Clear(); rooms.Clear(); radii.Clear(); corridors.Clear(); corridorBounds.Clear();
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
            var floor = new MeshData(); var walls = new MeshData(); var collision = new MeshData();
            for (int x = 0; x < nx; x++)
                for (int z = 0; z < nz; z++)
                {
                    Vector2 a = min + new Vector2(x, z) * cell;
                    Vector2 b = a + Vector2.up * cell;
                    Vector2 c = a + Vector2.one * cell;
                    Vector2 d = a + Vector2.right * cell;
                    Clip(a, b, c, values[x,z], values[x,z+1], values[x+1,z+1], floor, walls, collision);
                    Clip(a, c, d, values[x,z], values[x+1,z+1], values[x+1,z], floor, walls, collision);
                }
            generated = new GameObject("Generated Cave (preview)");
            generated.hideFlags = HideFlags.DontSave;
            generated.transform.SetParent(transform, false);
            floorMesh = floor.Build("Cave floor"); wallMesh = walls.Build("Cave walls");
            CreateSurface("Level floor", floorMesh, floorMaterial);
            wallCollisionMesh = collision.Build("Cave wall collision");
            CreateSurface("Rock walls", wallMesh, wallMaterial, wallCollisionMesh);
            if (player == null)
            {
                foreach (var candidate in FindObjectsByType<ProceduralCharacter>(FindObjectsSortMode.None))
                    if (candidate.gameObject.scene == gameObject.scene) { player = candidate.transform; break; }
            }
            if (player != null)
            {
                var controller = player.GetComponent<CharacterController>();
                bool enabledBefore = controller != null && controller.enabled;
                if (enabledBefore) controller.enabled = false;
                player.position = SpawnPosition;
                player.rotation = Quaternion.LookRotation(transform.TransformDirection(V(entranceForward, 0f)));
                if (enabledBefore) controller.enabled = true;
            }
        }

        private void GenerateLayout(System.Random random)
        {
            // Sunflower packing avoids rows and right-angle junctions.
            float rotation = (float)random.NextDouble() * Mathf.PI * 2f;
            float spacing = roomRadius * 4.3f;
            var edges = new List<Vector2Int>();
            for (int i = 0; i < roomCount; i++)
            {
                float angle = i * 2.399963f + rotation;
                float distance = spacing * 0.62f * Mathf.Sqrt(i);
                rooms.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance);
                // Stratified sizes guarantee a visible spread even in small layouts.
                float size = roomCount == 1 ? 0.5f : (i + (float)random.NextDouble()) / roomCount;
                radii.Add(Mathf.Max(2.5f, roomRadius * (1f + (size * 2f - 1f) * 0.45f * roomSizeVariation)));

            }
            for (int i = radii.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                float radius = radii[i]; radii[i] = radii[j]; radii[j] = radius;
            }
            for (int i = 0; i < roomCount; i++)
                for (int j = i + 1; j < roomCount; j++) edges.Add(new Vector2Int(i, j));
            edges.Sort((a, b) => (rooms[a.x] - rooms[a.y]).sqrMagnitude.CompareTo((rooms[b.x] - rooms[b.y]).sqrMagnitude));
            // Randomized spanning tree guarantees reachability; spare edges add real cycles.
            var groups = new int[roomCount];
            for (int i = 0; i < roomCount; i++) groups[i] = i;
            var spare = new List<Vector2Int>();
            foreach (var edge in edges)
            {
                int from = groups[edge.x], to = groups[edge.y];
                if (from == to) { if (Vector2.Distance(rooms[edge.x], rooms[edge.y]) < spacing * 1.15f) spare.Add(edge); continue; }
                AddCorridor(edge, random, spacing);
                for (int i = 0; i < groups.Length; i++) if (groups[i] == to) groups[i] = from;
            }
            for (int i = spare.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                Vector2Int swap = spare[i]; spare[i] = spare[j]; spare[j] = swap;
            }
            int loops = Mathf.RoundToInt(Mathf.Min(spare.Count, roomCount / 2f) * extraConnections);
            if (extraConnections > 0f && spare.Count > 0) loops = Mathf.Max(1, loops);
            for (int i = 0; i < loops; i++) AddCorridor(spare[i], random, spacing);
            int mainCount = corridors.Count;
            int branches = Mathf.RoundToInt(roomCount * branchDensity);
            for (int i = 0; i < branches && mainCount > 0; i++)
            {
                var source = corridors[random.Next(mainCount)];
                int index = random.Next(source.Length / 3, source.Length * 2 / 3);
                Vector2 start = source[index];
                Vector2 side = Vector2.Perpendicular((source[index + 1] - source[index - 1]).normalized);
                if (random.Next(2) == 0) side = -side;
                Vector2 end = start + side * Mathf.Lerp(7f, 12f, (float)random.NextDouble());
                AddTunnel(start, end, random);
            }
            // Start beyond the southernmost chamber, so the entrance cannot cut across the maze.
            int first = 0;
            for (int i = 1; i < roomCount; i++)
                if (rooms[i].y - radii[i] < rooms[first].y - radii[first]) first = i;
            Vector2 mouth = rooms[first] - Vector2.up * (radii[first] * 1.2f + entranceLength);
            AddTunnel(mouth, rooms[first], random);
            var entryPath = corridors[corridors.Count - 1];
            entrance = entryPath[2];
            entranceForward = (entryPath[3] - entryPath[2]).normalized;
        }

        private void AddCorridor(Vector2Int edge, System.Random random, float spacing)
        {
            AddTunnel(rooms[edge.x], rooms[edge.y], random);
        }

        private void AddTunnel(Vector2 a, Vector2 b, System.Random random)
        {
            Vector2 normal = Vector2.Perpendicular((b - a).normalized);
            float length = Vector2.Distance(a, b);
            float sign = random.Next(2) == 0 ? -1f : 1f;
            float amplitude = Mathf.Min(length * 0.24f, 5f) * corridorWinding;
            float asymmetry = Mathf.Lerp(-0.4f, 0.4f, (float)random.NextDouble());
            int steps = Mathf.Max(12, Mathf.CeilToInt(length / 0.9f));
            var points = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float wave = Mathf.Sin(t * Mathf.PI * 2f) + asymmetry * Mathf.Sin(t * Mathf.PI * 3f);
                points[i] = Vector2.Lerp(a, b, t) + normal * (wave * amplitude * sign);
            }
            points[0] = a; points[steps] = b;
            corridors.Add(points);
            Vector2 min = a, max = a;
            foreach (var point in points) { min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            float padding = corridorWidth * 0.5f + 3f;
            corridorBounds.Add(Rect.MinMaxRect(min.x - padding, min.y - padding, max.x + padding, max.y + padding));
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
            for (int c = 0; c < corridors.Count; c++)
            {
            if (!corridorBounds[c].Contains(p)) continue;
            var corridor = corridors[c];
            for (int i = 1; i < corridor.Length; i++)
            {
                Vector2 a = corridor[i - 1], ab = corridor[i] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                value = Mathf.Max(value, corridorWidth * 0.5f - Vector2.Distance(p, a + t * ab));
            }
            }
            // Perturb only the boundary: minimum corridor clearance remains above 1.8 metres.
            return value + (Mathf.PerlinNoise(p.x * 0.42f + noiseOffset, p.y * 0.42f + noiseOffset) - 0.5f) * irregularity * 0.7f;
        }
        private void CreateSurface(string label, Mesh mesh, Material material, Mesh colliderMesh = null)
        {
            var go = new GameObject(label) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(generated.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.AddComponent<MeshCollider>().sharedMesh = colliderMesh != null ? colliderMesh : mesh;
        }
        private void Clip(Vector2 a, Vector2 b, Vector2 c, float fa, float fb, float fc, MeshData floor, MeshData walls, MeshData collision)
        {
            if (fa < 0f && fb < 0f && fc < 0f) return;
            var polygon = new List<Vector2>(4);
            var boundary = new List<Vector2>(2);
            Edge(a, b, fa, fb, polygon, boundary);
            Edge(b, c, fb, fc, polygon, boundary);
            Edge(c, a, fc, fa, polygon, boundary);
            for (int i = 1; i + 1 < polygon.Count; i++) floor.Triangle(Surface(polygon[0], 0f), Surface(polygon[i], 0f), Surface(polygon[i+1], 0f));
            if (boundary.Count != 2) return;
            Vector2 p = boundary[0], q = boundary[1];
            Vector2 outward = Vector2.Perpendicular(q - p).normalized;
            Vector2 midpoint = (p + q) * 0.5f;
            if (Field(midpoint + outward * 0.1f) > Field(midpoint - outward * 0.1f)) outward = -outward;
            // Shared noise at boundary vertices keeps neighbouring rock panels watertight.
            // Collision follows the carved boundary; decorative outer lips must not block nearby tunnels.
            collision.Quad(Surface(p, 0f), Surface(q, 0f), Surface(q, wallHeight + 1f), Surface(p, wallHeight + 1f), -V(outward, 0f));
            Vector3 p0 = Surface(p, 0f), q0 = Surface(q, 0f);
            Vector3 p1 = p0 + V(RockOffset(p) * 0.35f, VisibleHeight(p) * 0.48f), q1 = q0 + V(RockOffset(q) * 0.35f, VisibleHeight(q) * 0.48f);
            Vector3 p2 = p0 + V(RockOffset(p), VisibleHeight(p)), q2 = q0 + V(RockOffset(q), VisibleHeight(q));
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
        /// <summary>One height per XZ point keeps all intersections seamless and prevents stacked floors.</summary>
        public float FloorHeight(Vector2 point)
        {
            if (!enableElevation) return 0f;
            float amplitude = elevationRange * 0.5f;
            // Analytic gradient stays below the requested slope, with margin for triangulation.
            float frequency = Mathf.Tan(maximumSlope * Mathf.Deg2Rad) * 0.85f / amplitude;
            return amplitude * 0.5f * (Mathf.Sin(point.x * frequency + noiseOffset)
                + Mathf.Sin(point.y * frequency * 0.83f + noiseOffset * 0.71f));
        }
        private Vector3 Surface(Vector2 p, float offset) => V(p, FloorHeight(p) + offset);
        private float VisibleHeight(Vector2 p) => enableElevation ? Mathf.Min(Height(p), cutawayWallHeight) : Height(p);
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
