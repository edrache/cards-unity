using System;
using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Read-only ray accelerator for visible terrain that differs from its gameplay collider.</summary>
    internal sealed class CrawlSurfaceMesh
    {
        private struct Triangle
        {
            public Vector3 a, b, c;
            public Bounds bounds;
        }
        private sealed class Node
        {
            public Bounds bounds;
            public Node left, right;
            public int start, count;
        }
        private readonly Triangle[] triangles;
        private readonly Node root;
        public Mesh Mesh { get; }
        public MeshCollider Collider { get; }

        public CrawlSurfaceMesh(MeshCollider collider, Mesh mesh)
        {
            Collider = collider;
            Mesh = mesh;
            var vertices = mesh.vertices;
            var indices = mesh.triangles;
            triangles = new Triangle[indices.Length / 3];
            for (int i = 0; i < triangles.Length; i++)
            {
                var t = new Triangle { a = vertices[indices[i * 3]], b = vertices[indices[i * 3 + 1]], c = vertices[indices[i * 3 + 2]] };
                t.bounds = new Bounds(t.a, Vector3.zero);
                t.bounds.Encapsulate(t.b); t.bounds.Encapsulate(t.c);
                t.bounds.Expand(0.0001f);
                triangles[i] = t;
            }
            if (triangles.Length > 0) root = Build(0, triangles.Length);
        }

        private Node Build(int start, int count)
        {
            var node = new Node { start = start, count = count, bounds = triangles[start].bounds };
            for (int i = start + 1; i < start + count; i++) node.bounds.Encapsulate(triangles[i].bounds);
            if (count <= 8) return node;
            Vector3 extent = node.bounds.size;
            int axis = extent.x > extent.y ? (extent.x > extent.z ? 0 : 2) : (extent.y > extent.z ? 1 : 2);
            Array.Sort(triangles, start, count, System.Collections.Generic.Comparer<Triangle>.Create(
                (a, b) => a.bounds.center[axis].CompareTo(b.bounds.center[axis])));
            int half = count / 2;
            node.left = Build(start, half); node.right = Build(start + half, count - half);
            return node;
        }

        public bool Raycast(Vector3 origin, Vector3 direction, float length, out RaycastHit hit)
        {
            hit = default;
            if (root == null || Collider == null || !Collider.enabled || !Collider.gameObject.activeInHierarchy) return false;
            var tr = Collider.transform;
            Vector3 localDirection = tr.InverseTransformVector(direction);
            float scale = localDirection.magnitude;
            if (scale < 0.00001f) return false;
            var ray = new Ray(tr.InverseTransformPoint(origin), localDirection / scale);
            float nearest = length * scale;
            Vector3 normal = Vector3.zero;
            bool found = Intersect(root, ray, ref nearest, ref normal);
            if (!found) return false;
            hit.distance = nearest / scale;
            hit.point = origin + direction * hit.distance;
            hit.normal = tr.worldToLocalMatrix.transpose.MultiplyVector(normal).normalized;
            return true;
        }

        private bool Intersect(Node node, Ray ray, ref float nearest, ref Vector3 normal)
        {
            if (!node.bounds.IntersectRay(ray, out float entry) || entry > nearest) return false;
            if (node.left != null)
            {
                bool left = Intersect(node.left, ray, ref nearest, ref normal);
                return Intersect(node.right, ray, ref nearest, ref normal) || left;
            }
            bool found = false;
            for (int i = node.start; i < node.start + node.count; i++)
            {
                var t = triangles[i];
                Vector3 e1 = t.b - t.a, e2 = t.c - t.a;
                Vector3 p = Vector3.Cross(ray.direction, e2);
                float determinant = Vector3.Dot(e1, p);
                if (Mathf.Abs(determinant) < 0.0000001f) continue;
                float inv = 1f / determinant;
                Vector3 offset = ray.origin - t.a;
                float u = Vector3.Dot(offset, p) * inv;
                if (u < 0f || u > 1f) continue;
                Vector3 q = Vector3.Cross(offset, e1);
                float v = Vector3.Dot(ray.direction, q) * inv;
                if (v < 0f || u + v > 1f) continue;
                float distance = Vector3.Dot(e2, q) * inv;
                if (distance < 0.000001f || distance > nearest) continue;
                nearest = distance;
                normal = Vector3.Cross(e1, e2).normalized;
                if (Vector3.Dot(normal, ray.direction) > 0f) normal = -normal;
                found = true;
            }
            return found;
        }
    }
}
