using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.Controllers
{
    public sealed partial class ProceduralCave
    {
        private void SpawnGrass()
        {
            foreach (var rule in Settings.roomContentRules)
                if (rule != null && rule.enabled && rule.contentType == CaveRoomContentType.Grass)
                    SpawnGrass(rule);
        }

        private void SpawnGrass(CaveRoomContentRule rule)
        {
            CaveGrassSettings grass = rule.grass;
            int count = Mathf.Clamp(Mathf.FloorToInt(rooms.Count * rule.roomPercentage / 100f + 0.5f), 0, rooms.Count);
            if (count == 0 || grass.grassMaterial == null || grass.grassBladesPerSquareMetre <= 0f || grass.grassMaximumBlades == 0) return;
            var random = new System.Random(unchecked(Settings.seed * 397 ^ rule.seedSalt));
            var order = new int[rooms.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            // Budget the enclosing squares, so every selected room gets coverage even at the cap.
            float area = 0f;
            for (int i = 0; i < count; i++) area += Mathf.Pow(radii[order[i]] * 2.5f, 2f);
            float density = Mathf.Min(grass.grassBladesPerSquareMetre, grass.grassMaximumBlades / Mathf.Max(area, 1f));
            float spacing = 1f / Mathf.Sqrt(density);
            var patches = new Dictionary<Vector2Int, List<Vector3>>();
            var occupied = new HashSet<Vector2Int>();
            int blades = 0;
            for (int i = 0; i < count; i++)
            {
                int room = order[i];
                Vector2 min = rooms[room] - Vector2.one * radii[room] * 1.25f;
                Vector2 max = rooms[room] + Vector2.one * radii[room] * 1.25f;
                for (int x = Mathf.CeilToInt(min.x / spacing); x * spacing < max.x; x++)
                    for (int z = Mathf.CeilToInt(min.y / spacing); z * spacing < max.y; z++)
                    {
                        var cell = new Vector2Int(x, z);
                        if (occupied.Contains(cell)) continue;
                        Vector2 p = new Vector2(x + (float)random.NextDouble(), z + (float)random.NextDouble()) * spacing;
                        if (!IsInsideRoomLocal(p, room, 0.35f) || Field(p) < 0.4f) continue;
                        if (OverlapsFootprints(p, 0.12f, rockFootprints) || OverlapsFootprints(p, 0.15f, occupiedContentFootprints)
                            || OverlapsPointFootprints(p, 0.55f, treasurePositions)) continue;
                        if (blades >= grass.grassMaximumBlades) break;
                        occupied.Add(cell);
                        var key = new Vector2Int(Mathf.FloorToInt(p.x / CaveGrass.ChunkSize), Mathf.FloorToInt(p.y / CaveGrass.ChunkSize));
                        if (!patches.TryGetValue(key, out var roots)) patches.Add(key, roots = new List<Vector3>());
                        roots.Add(new Vector3(p.x, FloorHeight(p) - 0.025f, p.y));
                        blades++;
                    }
            }
            var root = new GameObject(string.IsNullOrWhiteSpace(rule.label) ? "Tall grass" : rule.label.Trim());
            root.transform.SetParent(generated.transform, false);
            root.AddComponent<CaveGrass>().Initialize(patches, grass, player, random);
        }
    }
}
