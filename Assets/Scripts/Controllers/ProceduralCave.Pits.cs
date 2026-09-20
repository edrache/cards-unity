using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.Controllers
{
    public sealed partial class ProceduralCave
    {
        internal Material PitFloorMaterial => Settings.floorMaterial;
        internal Material PitWallMaterial => Settings.wallMaterial;

        // Population belongs to prefab Room Content Rules. Only floor integration remains in the generator.
        private void ApplyPitCutouts()
        {
            var boundaries = new List<Vector2[]>();
            foreach (var pit in generated.GetComponentsInChildren<CavePitTrap>())
            {
                if (!pit.IsConfigured || pit.Boundary == null) continue;
                var polygon = new Vector2[pit.Boundary.Count];
                for (int i = 0; i < polygon.Length; i++) polygon[i] = pit.Boundary[i];
                boundaries.Add(polygon);
            }
            if (boundaries.Count == 0) return;
            Mesh replacement = CavePitGeometry.CutFloor(floorMesh, boundaries);
            Transform surface = generated.transform.Find("Level floor");
            surface.GetComponent<MeshFilter>().sharedMesh = replacement;
            surface.GetComponent<MeshCollider>().sharedMesh = replacement;
            Dispose(floorMesh);
            floorMesh = replacement;
        }
    }
}
