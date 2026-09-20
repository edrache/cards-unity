using System;
using UnityEditor;
using UnityEngine;

namespace CardsUnity.Controllers.Editor
{
    /// <summary>Focused geometry and snapshot regression checks, without scene changes.</summary>
    public static class CavePitChecks
    {
        [MenuItem("Tools/Cards Unity/Run Pit Checks")]
        public static void Run()
        {
            var source = new Mesh();
            Mesh cut = null;
            try
            {
                source.vertices = new[] { new Vector3(0, 0, 0), new Vector3(0, 1, 10),
                    new Vector3(10, 3, 10), new Vector3(10, 2, 0) };
                source.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                var hole = new[] { new Vector2(3, 3), new Vector2(3, 7), new Vector2(7, 7), new Vector2(7, 3) };
                var second = new[] { new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1.5f), new Vector2(1.5f, 1.5f), new Vector2(1.5f, 0.5f) };
                cut = CavePitGeometry.CutFloor(source, new[] { hole, second });
                float area = 0f;
                var vertices = cut.vertices;
                var triangles = cut.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                    float signedArea = Vector3.Cross(b - a, c - a).y * 0.5f;
                    Require(signedArea > 0f, "Floor winding remains upward and nondegenerate");
                    area += signedArea;
                    Vector3 centre = (a + b + c) / 3f;
                    Require(!(centre.x > 3f && centre.x < 7f && centre.z > 3f && centre.z < 7f), "Main hole is empty");
                    Require(!(centre.x > 0.5f && centre.x < 1.5f && centre.z > 0.5f && centre.z < 1.5f), "Second hole is empty");
                }
                Require(Mathf.Abs(area - 83f) < 0.002f, "Exact remaining area, including holes crossing triangle edges");
                foreach (Vector3 p in vertices)
                    Require(Mathf.Abs(p.y - (0.2f * p.x + 0.1f * p.z)) < 0.0001f, "Sloping floor heights preserved");
                var settings = new CaveGenerationSettings();
                settings.pits.collapseSeconds = new[] { float.NaN, -1f, 2f };
                var clone = settings.Clone();
                clone.Validate();
                Require(float.IsNaN(settings.pits.collapseSeconds[0]), "Snapshot does not mutate source duration list");
                Require(clone.pits.collapseSeconds[0] == 1.2f && clone.pits.collapseSeconds[1] == 0.1f
                    && clone.pits.collapseSeconds[2] == 2f, "Duration validation preserves legal choices");
                clone.pits.collapseSeconds = Array.Empty<float>();
                clone.Validate();
                Require(clone.pits.collapseSeconds.Length == 1 && clone.pits.collapseSeconds[0] == 1.2f, "Empty duration fallback");
                Debug.Log("Pit checks passed: exact multi-hole area, empty interiors, upward winding, slope interpolation, snapshot isolation and duration validation.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                if (cut != null) UnityEngine.Object.DestroyImmediate(cut);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Pit check failed: " + message);
        }
    }
}
