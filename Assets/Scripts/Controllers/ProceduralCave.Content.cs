using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.Controllers
{
    public sealed partial class ProceduralCave
    {
        private readonly List<Vector3> occupiedContentFootprints = new List<Vector3>();

        private void SpawnRoomContent()
        {
            List<CaveRoomContentRule> rules = Settings.roomContentRules;
            if (rules == null || rules.Count == 0) return;

            GameObject population = null;
            for (int ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
            {
                CaveRoomContentRule rule = rules[ruleIndex];
                if (rule == null || !rule.enabled || rule.roomPercentage <= 0f) continue;

                var prefabs = new List<GameObject>();
                if (rule.prefabs != null)
                    foreach (GameObject prefab in rule.prefabs)
                        if (prefab != null && !prefabs.Contains(prefab)) prefabs.Add(prefab);
                if (prefabs.Count == 0) continue;

                int selectedRoomCount = Mathf.Clamp(
                    Mathf.FloorToInt(rooms.Count * rule.roomPercentage / 100f + 0.5f), 0, rooms.Count);
                if (selectedRoomCount == 0) continue;

                int baseSeed = CombineContentSeed(Settings.seed, rule.seedSalt);
                var roomRandom = new System.Random(unchecked(baseSeed ^ 73856093));
                var placementRandom = new System.Random(unchecked(baseSeed ^ 19349663));
                var prefabRandom = new System.Random(unchecked(baseSeed ^ 83492791));
                var participantRandom = new System.Random(unchecked(baseSeed ^ 15485863));
                var order = new int[rooms.Count];
                for (int i = 0; i < order.Length; i++) order[i] = i;
                for (int i = order.Length - 1; i > 0; i--)
                {
                    int swapIndex = roomRandom.Next(i + 1);
                    (order[i], order[swapIndex]) = (order[swapIndex], order[i]);
                }

                GameObject ruleRoot = null;
                int instanceIndex = 0;
                for (int selectedIndex = 0; selectedIndex < selectedRoomCount; selectedIndex++)
                {
                    int room = order[selectedIndex];
                    int count = rule.minimumPerSelectedRoom;
                    if (rule.maximumPerSelectedRoom > rule.minimumPerSelectedRoom)
                        count += placementRandom.Next(rule.maximumPerSelectedRoom - rule.minimumPerSelectedRoom + 1);
                    for (int i = 0; i < count; i++)
                    {
                        if (!TryFindRoomContentPosition(rule, room, placementRandom, out Vector2 point)) continue;
                        if (population == null)
                        {
                            population = new GameObject("Generated Room Content") { hideFlags = HideFlags.DontSave };
                            population.transform.SetParent(generated.transform, false);
                        }
                        if (ruleRoot == null)
                        {
                            string rootName = string.IsNullOrWhiteSpace(rule.label) ? $"Rule {ruleIndex + 1:00}" : rule.label.Trim();
                            ruleRoot = new GameObject(rootName) { hideFlags = HideFlags.DontSave };
                            ruleRoot.transform.SetParent(population.transform, false);
                            // Keep callbacks ahead of OnEnable so authored behaviours see their cave context first.
                            ruleRoot.SetActive(false);
                        }

                        GameObject prefab = prefabs[prefabRandom.Next(prefabs.Count)];
                        GameObject instance = Instantiate(prefab, ruleRoot.transform);
                        instance.name = $"{prefab.name} (Room {room + 1:00}, {i + 1})";
                        instance.hideFlags = HideFlags.DontSave;
                        Vector3 normal = rule.alignToFloor ? FloorNormal(point) : Vector3.up;
                        Quaternion surfaceRotation = Quaternion.FromToRotation(Vector3.up, normal);
                        Quaternion yaw = rule.randomYaw
                            ? Quaternion.Euler(0f, (float)placementRandom.NextDouble() * 360f, 0f)
                            : Quaternion.identity;
                        instance.transform.localPosition = Surface(point, rule.surfaceOffset);
                        instance.transform.localRotation = surfaceRotation * yaw * prefab.transform.localRotation;

                        int instanceSeed = participantRandom.Next();
                        var context = new CaveSpawnContext(this, rule, player, room, instanceIndex, instanceSeed);
                        if (!NotifySpawnParticipants(instance, context))
                        {
                            instance.SetActive(false);
                            Dispose(instance);
                            continue;
                        }

                        occupiedContentFootprints.Add(new Vector3(point.x, point.y, rule.footprintRadius));
                        instanceIndex++;
                    }
                }
                if (ruleRoot != null) ruleRoot.SetActive(true);
            }
        }

        private bool TryFindRoomContentPosition(CaveRoomContentRule rule, int room, System.Random random, out Vector2 point)
        {
            float sampleRadius = Mathf.Max(0f, radii[room] - rule.footprintRadius - rule.wallClearance);
            for (int attempt = 0; attempt < 128; attempt++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Sqrt((float)random.NextDouble()) * sampleRadius;
                Vector2 candidate = rooms[room] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (!IsRoomContentPositionAvailable(candidate, room, rule)) continue;
                point = candidate;
                return true;
            }
            point = default;
            return false;
        }

        private bool IsRoomContentPositionAvailable(Vector2 point, int room, CaveRoomContentRule rule)
        {
            float requiredField = rule.wallClearance;
            if (!IsInsideRoomLocal(point, room, rule.footprintRadius + rule.wallClearance)) return false;
            if (Field(point) < requiredField) return false;
            const int perimeterSamples = 12;
            for (int i = 0; i < perimeterSamples; i++)
            {
                float angle = i * Mathf.PI * 2f / perimeterSamples;
                Vector2 edge = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * rule.footprintRadius;
                if (Field(edge) < requiredField) return false;
            }

            if (rule.routeClearance > 0f)
            {
                float requiredRouteDistance = rule.footprintRadius + rule.routeClearance;
                foreach (Vector2[] corridor in corridors)
                    for (int i = 1; i < corridor.Length; i++)
                    {
                        Vector2 edge = corridor[i] - corridor[i - 1];
                        float t = Mathf.Clamp01(Vector2.Dot(point - corridor[i - 1], edge)
                            / Mathf.Max(0.0001f, edge.sqrMagnitude));
                        if (Vector2.Distance(point, corridor[i - 1] + edge * t) < requiredRouteDistance) return false;
                    }
            }

            if (OverlapsFootprints(point, rule.footprintRadius, rockFootprints)) return false;
            if (OverlapsPointFootprints(point, rule.footprintRadius + 0.75f, treasurePositions)) return false;
            return !OverlapsFootprints(point, rule.footprintRadius, occupiedContentFootprints);
        }

        private bool IsInsideRoomLocal(Vector2 point, int room, float inset)
        {
            Vector2 delta = point - rooms[room];
            float angle = Mathf.Atan2(delta.y, delta.x);
            float lobes = Mathf.Sin(angle * 3f + noiseOffset + room) * 0.13f
                + Mathf.Sin(angle * 5f - noiseOffset) * 0.07f;
            return delta.magnitude + inset < radii[room] * (1f + lobes * Settings.irregularity);
        }

        private static bool OverlapsFootprints(Vector2 point, float radius, List<Vector3> footprints)
        {
            foreach (Vector3 footprint in footprints)
                if (Vector2.Distance(point, new Vector2(footprint.x, footprint.y)) < radius + footprint.z) return true;
            return false;
        }

        private static bool OverlapsPointFootprints(Vector2 point, float radius, List<Vector2> footprints)
        {
            foreach (Vector2 footprint in footprints)
                if (Vector2.Distance(point, footprint) < radius) return true;
            return false;
        }

        private Vector3 FloorNormal(Vector2 point)
        {
            const float e = 0.1f;
            return new Vector3(
                -(FloorHeight(point + Vector2.right * e) - FloorHeight(point - Vector2.right * e)) / (2f * e),
                1f,
                -(FloorHeight(point + Vector2.up * e) - FloorHeight(point - Vector2.up * e)) / (2f * e)).normalized;
        }

        private static int CombineContentSeed(int caveSeed, int ruleSalt)
        {
            unchecked
            {
                uint value = (uint)caveSeed;
                value ^= (uint)ruleSalt + 0x9e3779b9u + (value << 6) + (value >> 2);
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                return (int)value;
            }
        }

        private static bool NotifySpawnParticipants(GameObject instance, CaveSpawnContext context)
        {
            foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!(behaviour is ICaveSpawnParticipant participant)) continue;
                try
                {
                    participant.OnCaveSpawned(context);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"Cave content callback failed for '{instance.name}' on '{behaviour.GetType().Name}'. " +
                        "The generated instance was removed.\n" + exception,
                        behaviour);
                    return false;
                }
            }
            return true;
        }
    }
}
