using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.Controllers
{
    public sealed partial class ProceduralCave
    {
        private void SpawnPitTraps()
        {
            CavePitSettings tuning = Settings.pits;
            if (tuning.openRoomPercentage <= 0f && tuning.crackedRoomPercentage <= 0f) return;
            var boundaries = new List<Vector2[]>();
            for (int variant = 0; variant < 2; variant++)
            {
                float percentage = variant == 0 ? tuning.openRoomPercentage : tuning.crackedRoomPercentage;
                int count = Mathf.Clamp(Mathf.RoundToInt(rooms.Count * percentage / 100f), 0, rooms.Count);
                // Neither variant consumes any existing layout, content or rock random stream.
                var random = new System.Random(unchecked(Settings.seed * 397 ^ (variant == 0 ? 672804213 : 715827883)));
                var order = new List<int>();
                for (int i = 0; i < rooms.Count; i++) order.Add(i);
                for (int i = order.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (order[i], order[j]) = (order[j], order[i]);
                }
                for (int i = 0; i < count; i++)
                {
                    float width = Mathf.Lerp(tuning.minimumSize, tuning.maximumSize, (float)random.NextDouble());
                    float length = Mathf.Lerp(tuning.minimumSize, tuning.maximumSize, (float)random.NextDouble());
                    float radius = Mathf.Max(width, length) * 0.5f;
                    var rule = new CaveRoomContentRule { footprintRadius = radius + 0.3f, wallClearance = 0.6f, routeClearance = 1f };
                    if (!TryFindRoomContentPosition(rule, order[i], random, out Vector2 centre)) continue;
                    if (Vector2.Distance(centre, entrance) < radius + 3f || Vector2.Distance(centre, exit) < radius + 3f) continue;
                    var boundary = new Vector2[10];
                    float yaw = (float)random.NextDouble() * Mathf.PI * 2f;
                    for (int j = 0; j < boundary.Length; j++)
                    {
                        // Samples remain on an ellipse, guaranteeing a convex clockwise rim for exact subtraction.
                        float angle = -(j + ((float)random.NextDouble() - 0.5f) * tuning.irregularity * 0.7f)
                            * Mathf.PI * 2f / boundary.Length;
                        Vector2 p = new Vector2(Mathf.Cos(angle) * width, Mathf.Sin(angle) * length) * 0.5f;
                        boundary[j] = centre + new Vector2(p.x * Mathf.Cos(yaw) - p.y * Mathf.Sin(yaw),
                            p.x * Mathf.Sin(yaw) + p.y * Mathf.Cos(yaw));
                    }
                    float duration = tuning.collapseSeconds[random.Next(tuning.collapseSeconds.Length)];
                    var instance = new GameObject($"{(variant == 0 ? "Open" : "Cracked")} pit (Room {order[i] + 1:00})");
                    instance.transform.SetParent(generated.transform, false);
                    instance.AddComponent<CavePitTrap>().Initialize(this, boundary, player, variant == 1,
                        duration, Settings.floorMaterial, Settings.wallMaterial, tuning.depth);
                    boundaries.Add(boundary);
                    occupiedContentFootprints.Add(new Vector3(centre.x, centre.y, radius + 0.3f));
                }
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
