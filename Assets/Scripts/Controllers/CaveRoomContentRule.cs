using System;
using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>One independent, seeded population rule for arbitrary room content.</summary>
    [Serializable]
    public sealed class CaveRoomContentRule
    {
        public string label = "Room Content";
        public bool enabled = true;
        [Tooltip("Stable salt for this rule's random stream. Keep it unique when rules must vary independently.")]
        public int seedSalt = 1;
        public GameObject[] prefabs;
        [Range(0f, 100f)] public float roomPercentage = 25f;
        [Tooltip("Instances requested in each selected room. Placement can yield fewer when no safe footprint remains.")]
        [Range(0, 64)] public int minimumPerSelectedRoom = 1;
        [Range(0, 64)] public int maximumPerSelectedRoom = 1;
        [Tooltip("Planar radius reserved around each instance. Author this from the prefab's gameplay footprint.")]
        [Min(0f)] public float footprintRadius = 0.5f;
        [Tooltip("Required free cave field beyond the footprint. Larger values keep content farther from walls.")]
        [Min(0f)] public float wallClearance = 0.3f;
        [Tooltip("Additional distance from corridor centre lines. Use zero for mobile or non-blocking content.")]
        [Min(0f)] public float routeClearance;
        [Range(-10f, 10f)] public float surfaceOffset;
        [Tooltip("Chance to attach content to a room wall instead of the floor. Prefab +Z points into the room; origin is the attachment point. Failed wall attempts fall back to floor placement.")]
        [Range(0f, 100f)] public float wallPlacementPercentage;
        [Min(0f)] public float minimumWallHeight = 0.25f;
        [Min(0f)] public float maximumWallHeight = 0.8f;
        [Tooltip("How far the attachment origin is embedded behind the wall surface. Wall placement ignores Wall Clearance and Surface Offset.")]
        [Range(0f, 0.5f)] public float wallEmbedDepth = 0.12f;
        public bool alignToFloor = true;
        public bool randomYaw = true;

        public CaveRoomContentRule Clone()
        {
            var clone = (CaveRoomContentRule)MemberwiseClone();
            clone.prefabs = prefabs != null ? (GameObject[])prefabs.Clone() : null;
            return clone;
        }

        public void Validate()
        {
            roomPercentage = Mathf.Clamp(Finite(roomPercentage, 25f), 0f, 100f);
            minimumPerSelectedRoom = Mathf.Clamp(minimumPerSelectedRoom, 0, 64);
            maximumPerSelectedRoom = Mathf.Clamp(maximumPerSelectedRoom, minimumPerSelectedRoom, 64);
            footprintRadius = Mathf.Max(0f, Finite(footprintRadius, 0.5f));
            wallClearance = Mathf.Max(0f, Finite(wallClearance, 0.3f));
            routeClearance = Mathf.Max(0f, Finite(routeClearance, 0f));
            wallPlacementPercentage = Mathf.Clamp(Finite(wallPlacementPercentage, 0f), 0f, 100f);
            minimumWallHeight = Mathf.Max(0f, Finite(minimumWallHeight, 0.25f));
            maximumWallHeight = Mathf.Max(minimumWallHeight, Finite(maximumWallHeight, 0.8f));
            wallEmbedDepth = Mathf.Clamp(Finite(wallEmbedDepth, 0.12f), 0f, 0.5f);
            surfaceOffset = Mathf.Clamp(Finite(surfaceOffset, 0f), -10f, 10f);
        }

        private static float Finite(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }

    /// <summary>Optional callback implemented by components that need cave-specific initialization.</summary>
    public interface ICaveSpawnParticipant
    {
        void OnCaveSpawned(CaveSpawnContext context);
    }

    public readonly struct CaveSpawnContext
    {
        public readonly ProceduralCave Cave;
        public readonly CaveRoomContentRule Rule;
        public readonly Transform Player;
        public readonly int RoomIndex;
        public readonly int InstanceIndex;
        public readonly int Seed;

        public CaveSpawnContext(ProceduralCave cave, CaveRoomContentRule rule, Transform player,
            int roomIndex, int instanceIndex, int seed)
        {
            Cave = cave;
            Rule = rule;
            Player = player;
            RoomIndex = roomIndex;
            InstanceIndex = instanceIndex;
            Seed = seed;
        }
    }
}
