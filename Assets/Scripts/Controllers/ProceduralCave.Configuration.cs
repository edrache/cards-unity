using System.Collections.Generic;

namespace CardsUnity.Controllers
{
    public sealed partial class ProceduralCave
    {
        private CaveGenerationSettings activeSettings;

        /// <summary>The assigned reusable profile, or null while legacy component fields drive generation.</summary>
        public CaveGenerationProfile GenerationProfile => generationProfile;

        private CaveGenerationSettings Settings
        {
            get
            {
                if (activeSettings == null) RefreshActiveSettings();
                return activeSettings;
            }
        }

        private void RefreshActiveSettings()
        {
            activeSettings = generationProfile != null
                ? generationProfile.Settings.Clone()
                : CaptureLegacySettings();
            activeSettings.Validate();
        }

        /// <summary>Copies the original serialized component fields without reading an assigned profile.</summary>
        public CaveGenerationSettings CaptureLegacySettings()
        {
            return new CaveGenerationSettings
            {
                roomCount = roomCount,
                seed = seed,
                roomRadius = roomRadius,
                roomSizeVariation = roomSizeVariation,
                extraConnections = extraConnections,
                corridorWidth = corridorWidth,
                corridorWinding = corridorWinding,
                branchDensity = branchDensity,
                entranceLength = entranceLength,
                rockPrefabs = rockPrefabs != null ? (UnityEngine.GameObject[])rockPrefabs.Clone() : null,
                rockDensity = rockDensity,
                minimumRockSize = minimumRockSize,
                maximumRockSize = maximumRockSize,
                rockfallCorridorPercentage = rockfallCorridorPercentage,
                minimumRockfallLength = minimumRockfallLength,
                maximumRockfallLength = maximumRockfallLength,
                rockfallCeilingHeight = rockfallCeilingHeight,
                rockfallWarningSeconds = rockfallWarningSeconds,
                rockfallCollapseSeconds = rockfallCollapseSeconds,
                rockfallIdlePebblesPerSecond = rockfallIdlePebblesPerSecond,
                rockfallActivePebblesPerSecond = rockfallActivePebblesPerSecond,
                rockfallPebbleLifetime = rockfallPebbleLifetime,
                minimumRockfallPebbleSize = minimumRockfallPebbleSize,
                maximumRockfallPebbleSize = maximumRockfallPebbleSize,
                minimumRockfallBoulderRadius = minimumRockfallBoulderRadius,
                maximumRockfallBoulderRadius = maximumRockfallBoulderRadius,
                rockfallMaterial = rockfallMaterial,
                centipedePrefab = centipedePrefab,
                centipedeRoomPercentage = centipedeRoomPercentage,
                treasurePrefab = treasurePrefab,
                treasureVariants = treasureVariants != null ? (Treasure[])treasureVariants.Clone() : null,
                treasureRoomPercentage = treasureRoomPercentage,
                coinPrefab = coinPrefab,
                coinsPerRoom = coinsPerRoom,
                bodyPrefab = bodyPrefab,
                bodyRoomPercentage = bodyRoomPercentage,
                enableElevation = enableElevation,
                elevationRange = elevationRange,
                maximumSlope = maximumSlope,
                cutawayWallHeight = cutawayWallHeight,
                irregularity = irregularity,
                wallHeight = wallHeight,
                floorMaterial = floorMaterial,
                wallMaterial = wallMaterial,
                roomContentRules = new List<CaveRoomContentRule>()
            };
        }

        /// <summary>Captures legacy component fields into a profile, preserving its generic content rules by default.</summary>
        public void CopyLegacySettingsTo(CaveGenerationProfile profile, bool preserveRoomContentRules = true)
        {
            if (profile == null) throw new System.ArgumentNullException(nameof(profile));
            CaveGenerationSettings captured = CaptureLegacySettings();
            if (preserveRoomContentRules && profile.Settings.roomContentRules != null)
            {
                captured.roomContentRules = new List<CaveRoomContentRule>();
                foreach (var rule in profile.Settings.roomContentRules)
                    captured.roomContentRules.Add(rule != null ? rule.Clone() : null);
            }
            profile.ReplaceSettings(captured);
        }

        /// <summary>Assigns a profile and either rebuilds immediately or queues the normal next-frame rebuild.</summary>
        public void AssignGenerationProfile(CaveGenerationProfile profile, bool rebuildImmediately = true)
        {
            generationProfile = profile;
            if (rebuildImmediately) Rebuild();
            else rebuildPending = true;
        }
    }
}
