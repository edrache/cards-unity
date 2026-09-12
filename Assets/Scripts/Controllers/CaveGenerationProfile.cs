using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Reusable, authorable snapshot of every cave generation setting except the scene player binding.</summary>
    [CreateAssetMenu(fileName = "Cave Generation Profile", menuName = "Cards Unity/Cave Generation Profile")]
    public sealed class CaveGenerationProfile : ScriptableObject
    {
        [SerializeField] private CaveGenerationSettings settings = new CaveGenerationSettings();

        public CaveGenerationSettings Settings
        {
            get
            {
                if (settings == null) settings = new CaveGenerationSettings();
                return settings;
            }
        }

        public void ReplaceSettings(CaveGenerationSettings value)
        {
            settings = value != null ? value.Clone() : new CaveGenerationSettings();
            settings.Validate();
        }

        private void OnValidate() => Settings.Validate();
    }

    /// <summary>Typed data used by one complete cave rebuild.</summary>
    [Serializable]
    public sealed class CaveGenerationSettings
    {
        [Header("Layout")]
        [Range(1, 24)] public int roomCount = 6;
        public int seed = 173;
        [Range(4f, 8f)] public float roomRadius = 5.5f;
        [Tooltip("Radius variation around Room Radius. Zero gives equal base sizes; one ranges from 55% to 145%.")]
        [Range(0f, 1f)] public float roomSizeVariation = 0.85f;
        [Tooltip("Fraction of spare neighbouring connections opened as loops. Zero makes a branching tree.")]
        [Range(0f, 1f)] public float extraConnections = 0.4f;
        [Range(2.5f, 5f)] public float corridorWidth = 3.5f;
        [Range(0f, 1f)] public float corridorWinding = 0.8f;
        [Tooltip("Side tunnels branching from the middle of existing passages, including dead ends.")]
        [Range(0f, 1f)] public float branchDensity = 0.5f;
        [Range(8f, 20f)] public float entranceLength = 12f;

        [Header("Scattered rocks")]
        public GameObject[] rockPrefabs;
        [Tooltip("Zero disables rocks; one fills available space while keeping routes clear.")]
        [Range(0f, 1f)] public float rockDensity = 0.45f;
        [Range(1f, 4f)] public float minimumRockSize = 1.2f;
        [Range(1f, 4f)] public float maximumRockSize = 2.6f;

        [Header("Corridor rockfall traps")]
        [Tooltip("Percentage of eligible corridors with one rockfall stretch. The entrance and rooms stay clear. Zero disables traps.")]
        [Range(0f, 100f)] public float rockfallCorridorPercentage = 25f;
        [Min(1f)] public float minimumRockfallLength = 2f;
        [Min(1f)] public float maximumRockfallLength = 6f;
        [Tooltip("Height above the corridor floor from which debris falls, independent of the cutaway walls.")]
        [Min(2f)] public float rockfallCeilingHeight = 3.5f;
        [Tooltip("Time after entering the trap before heavy rocks begin to fall. Leaving does not cancel it.")]
        [Min(0.1f)] public float rockfallWarningSeconds = 2.5f;
        [Tooltip("Time over which the heavy rocks are released, once the warning ends.")]
        [Min(0.1f)] public float rockfallCollapseSeconds = 1f;
        [Min(0f)] public float rockfallIdlePebblesPerSecond = 4f;
        [Min(0f)] public float rockfallActivePebblesPerSecond = 55f;
        [Min(0.1f)] public float rockfallPebbleLifetime = 2f;
        [Tooltip("Particle size in world units, before shrinking near the end of its lifetime.")]
        [Min(0.001f)] public float minimumRockfallPebbleSize = 0.025f;
        [Min(0.001f)] public float maximumRockfallPebbleSize = 0.085f;
        [Tooltip("Boulder radius in meters. Both bounds are reduced when necessary to fit the corridor.")]
        [Min(0.1f)] public float minimumRockfallBoulderRadius = 0.55f;
        [Min(0.1f)] public float maximumRockfallBoulderRadius = 0.67f;
        [Tooltip("Optional material with a PickupOutline pass. Unassigned uses a generated rock-colored Dither Accent material.")]
        public Material rockfallMaterial;

        [Header("Centipedes")]
        public ProceduralCentipede centipedePrefab;
        [Tooltip("Percentage of rooms receiving one centipede. Rounded to the nearest whole room; zero disables spawning. Selection is repeatable for Seed.")]
        [Range(0f, 100f)] public float centipedeRoomPercentage = 25f;

        [Header("Treasures")]
        public Treasure treasurePrefab;
        public Treasure[] treasureVariants;
        [Range(0f, 100f)] public float treasureRoomPercentage = 50f;

        [Header("Coins")]
        public Treasure coinPrefab;
        [Tooltip("Coins scattered independently of large treasures. Zero disables coins; crowded rooms may receive fewer.")]
        [Range(0, 8)] public int coinsPerRoom = 2;

        [Header("Bodies")]
        public CaveBody bodyPrefab;
        [Tooltip("Percentage of rooms receiving one body and burning torch. Selection and proportions repeat for Seed.")]
        [Range(0f, 100f)] public float bodyRoomPercentage = 25f;

        [Header("Elevation")]
        [Tooltip("Continuous ramps without overlapping floors. Disable to restore the flat cave.")]
        public bool enableElevation;
        [Tooltip("Maximum height difference between the lowest and highest parts of the height field.")]
        [Range(1f, 12f)] public float elevationRange = 6f;
        [Range(5f, 20f)] public float maximumSlope = 15f;
        [Tooltip("Visible wall height in elevated caves. Collision walls retain their full height.")]
        [Range(0.5f, 1.5f)] public float cutawayWallHeight = 1.2f;

        [Header("Rock")]
        [Range(0f, 1f)] public float irregularity = 0.65f;
        [Range(2f, 20f)] public float wallHeight = 3.2f;
        public Material floorMaterial;
        public Material wallMaterial;

        [Header("Extensible room content")]
        [Tooltip("Independent, seeded rules evaluated after all built-in cave populations have been placed.")]
        public List<CaveRoomContentRule> roomContentRules = new List<CaveRoomContentRule>();

        public CaveGenerationSettings Clone()
        {
            var clone = (CaveGenerationSettings)MemberwiseClone();
            clone.rockPrefabs = rockPrefabs != null ? (GameObject[])rockPrefabs.Clone() : null;
            clone.treasureVariants = treasureVariants != null ? (Treasure[])treasureVariants.Clone() : null;
            clone.roomContentRules = new List<CaveRoomContentRule>();
            if (roomContentRules != null)
                foreach (var rule in roomContentRules)
                    clone.roomContentRules.Add(rule != null ? rule.Clone() : null);
            return clone;
        }

        public void Validate()
        {
            roomCount = Mathf.Clamp(roomCount, 1, 24);
            roomRadius = Mathf.Clamp(Finite(roomRadius, 5.5f), 4f, 8f);
            roomSizeVariation = Mathf.Clamp01(Finite(roomSizeVariation, 0.85f));
            extraConnections = Mathf.Clamp01(Finite(extraConnections, 0.4f));
            corridorWidth = Mathf.Clamp(Finite(corridorWidth, 3.5f), 2.5f, 5f);
            corridorWinding = Mathf.Clamp01(Finite(corridorWinding, 0.8f));
            branchDensity = Mathf.Clamp01(Finite(branchDensity, 0.5f));
            entranceLength = Mathf.Clamp(Finite(entranceLength, 12f), 8f, 20f);
            rockDensity = Mathf.Clamp01(Finite(rockDensity, 0.45f));
            minimumRockSize = Mathf.Clamp(Finite(minimumRockSize, 1.2f), 1f, 4f);
            maximumRockSize = Mathf.Clamp(Finite(maximumRockSize, 2.6f), minimumRockSize, 4f);
            rockfallCorridorPercentage = Mathf.Clamp(Finite(rockfallCorridorPercentage, 25f), 0f, 100f);
            minimumRockfallLength = Mathf.Max(1f, Finite(minimumRockfallLength, 2f));
            maximumRockfallLength = Mathf.Max(minimumRockfallLength, Finite(maximumRockfallLength, 6f));
            rockfallCeilingHeight = Mathf.Max(2f, Finite(rockfallCeilingHeight, 3.5f));
            rockfallWarningSeconds = Mathf.Max(0.1f, Finite(rockfallWarningSeconds, 2.5f));
            rockfallCollapseSeconds = Mathf.Max(0.1f, Finite(rockfallCollapseSeconds, 1f));
            rockfallIdlePebblesPerSecond = Mathf.Max(0f, Finite(rockfallIdlePebblesPerSecond, 4f));
            rockfallActivePebblesPerSecond = Mathf.Max(rockfallIdlePebblesPerSecond, Finite(rockfallActivePebblesPerSecond, 55f));
            rockfallPebbleLifetime = Mathf.Max(0.1f, Finite(rockfallPebbleLifetime, 2f));
            minimumRockfallPebbleSize = Mathf.Max(0.001f, Finite(minimumRockfallPebbleSize, 0.025f));
            maximumRockfallPebbleSize = Mathf.Max(minimumRockfallPebbleSize, Finite(maximumRockfallPebbleSize, 0.085f));
            minimumRockfallBoulderRadius = Mathf.Max(0.1f, Finite(minimumRockfallBoulderRadius, 0.55f));
            maximumRockfallBoulderRadius = Mathf.Max(minimumRockfallBoulderRadius, Finite(maximumRockfallBoulderRadius, 0.67f));
            centipedeRoomPercentage = Mathf.Clamp(Finite(centipedeRoomPercentage, 25f), 0f, 100f);
            treasureRoomPercentage = Mathf.Clamp(Finite(treasureRoomPercentage, 50f), 0f, 100f);
            coinsPerRoom = Mathf.Clamp(coinsPerRoom, 0, 8);
            bodyRoomPercentage = Mathf.Clamp(Finite(bodyRoomPercentage, 25f), 0f, 100f);
            elevationRange = Mathf.Clamp(Finite(elevationRange, 6f), 1f, 12f);
            maximumSlope = Mathf.Clamp(Finite(maximumSlope, 15f), 5f, 20f);
            cutawayWallHeight = Mathf.Clamp(Finite(cutawayWallHeight, 1.2f), 0.5f, 1.5f);
            irregularity = Mathf.Clamp01(Finite(irregularity, 0.65f));
            wallHeight = Mathf.Clamp(Finite(wallHeight, 3.2f), 2f, 20f);
            if (roomContentRules == null) roomContentRules = new List<CaveRoomContentRule>();
            foreach (var rule in roomContentRules) rule?.Validate();
        }

        private static float Finite(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }
}
