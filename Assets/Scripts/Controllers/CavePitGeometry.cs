using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CardsUnity.Controllers
{
    /// <summary>Subtracts convex clockwise XZ polygons from a floor while interpolating its existing heights.</summary>
    public static class CavePitGeometry
    {
        public static Mesh CutFloor(Mesh source, IReadOnlyList<Vector2[]> holes)
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            var uv = new List<Vector2>();
            Vector3[] original = source.vertices;
            int[] triangles = source.triangles;
            var bounds = new Rect[holes.Count];
            for (int h = 0; h < holes.Count; h++)
            {
                Vector2 min = holes[h][0], max = min;
                foreach (Vector2 p in holes[h]) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
                bounds[h] = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = original[triangles[t]], b = original[triangles[t + 1]], c = original[triangles[t + 2]];
                Rect triangleBounds = Rect.MinMaxRect(Mathf.Min(a.x, b.x, c.x), Mathf.Min(a.z, b.z, c.z),
                    Mathf.Max(a.x, b.x, c.x), Mathf.Max(a.z, b.z, c.z));
                bool intersects = false;
                for (int h = 0; h < bounds.Length; h++)
                    if (triangleBounds.Overlaps(bounds[h])) { intersects = true; break; }
                if (!intersects)
                {
                    Add(a, vertices, indices, uv); Add(b, vertices, indices, uv); Add(c, vertices, indices, uv);
                    continue;
                }
                var pieces = new List<List<Vector3>> { new List<Vector3> {
                    original[triangles[t]], original[triangles[t + 1]], original[triangles[t + 2]] } };
                foreach (Vector2[] hole in holes)
                {
                    var next = new List<List<Vector3>>();
                    foreach (List<Vector3> piece in pieces) Subtract(piece, hole, next);
                    pieces = next;
                    if (pieces.Count == 0) break;
                }
                foreach (List<Vector3> piece in pieces)
                    for (int i = 1; i + 1 < piece.Count; i++)
                    {
                        if (Vector3.Cross(piece[i] - piece[0], piece[i + 1] - piece[0]).sqrMagnitude < 1e-12f) continue;
                        Add(piece[0], vertices, indices, uv);
                        Add(piece[i], vertices, indices, uv);
                        Add(piece[i + 1], vertices, indices, uv);
                    }
            }
            var mesh = new Mesh { name = "Cave floor with pits", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.SetUVs(0, uv);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static void Add(Vector3 p, List<Vector3> vertices, List<int> indices, List<Vector2> uv)
        {
            indices.Add(vertices.Count); vertices.Add(p);
            uv.Add(new Vector2(p.x + p.y, p.z + p.y) * 0.25f);
        }

        private static void Subtract(List<Vector3> polygon, Vector2[] hole, List<List<Vector3>> result)
        {
            Vector2 min = hole[0], max = hole[0];
            foreach (Vector2 p in hole) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            Vector2 pmin = new Vector2(polygon[0].x, polygon[0].z), pmax = pmin;
            foreach (Vector3 p in polygon) { pmin = Vector2.Min(pmin, new Vector2(p.x, p.z)); pmax = Vector2.Max(pmax, new Vector2(p.x, p.z)); }
            if (pmax.x < min.x || pmin.x > max.x || pmax.y < min.y || pmin.y > max.y)
            { result.Add(polygon); return; }
            // At each edge, retain the outside piece and continue clipping only the inside piece.
            // These pieces are disjoint; what survives all edges is the hole and is discarded.
            for (int edge = 0; edge < hole.Length && polygon.Count >= 3; edge++)
            {
                Vector2 a = hole[edge], b = hole[(edge + 1) % hole.Length];
                var inside = new List<Vector3>();
                var outside = new List<Vector3>();
                for (int i = 0; i < polygon.Count; i++)
                {
                    Vector3 p = polygon[i], q = polygon[(i + 1) % polygon.Count];
                    float dp = Side(a, b, p), dq = Side(a, b, q);
                    if (dp <= 0f) inside.Add(p); else outside.Add(p);
                    if ((dp < 0f && dq > 0f) || (dp > 0f && dq < 0f))
                    {
                        Vector3 intersection = Vector3.LerpUnclamped(p, q, dp / (dp - dq));
                        inside.Add(intersection); outside.Add(intersection);
                    }
                    else if (dp == 0f) outside.Add(p);
                }
                if (outside.Count >= 3) result.Add(outside);
                polygon = inside;
            }
        }

        private static float Side(Vector2 a, Vector2 b, Vector3 p) => (b.x - a.x) * (p.z - a.y) - (b.y - a.y) * (p.x - a.x);
    }
}
