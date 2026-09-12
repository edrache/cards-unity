using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardsUnity.Controllers.Editor
{
    /// <summary>Small, repeatable authoring regression checks without project test assembly dependencies.</summary>
    public static class ConfigurationChecks
    {
        [MenuItem("Tools/Cards Unity/Run Configuration Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Configuration checks require Edit Mode.");
            var scene = EditorSceneManager.NewPreviewScene();
            var playerProfile = ScriptableObject.CreateInstance<PlayerGameplayBalanceProfile>();
            var definition = ScriptableObject.CreateInstance<TreasureDefinition>();
            var torchProfile = ScriptableObject.CreateInstance<TorchBalanceProfile>();
            int assertions = 0;
            try
            {
                var settings = new CaveGenerationSettings
                {
                    roomCount = 0,
                    minimumRockSize = 3f,
                    maximumRockSize = 1f,
                    minimumRockfallLength = 5f,
                    maximumRockfallLength = 1f
                };
                settings.roomContentRules.Add(new CaveRoomContentRule { prefabs = new GameObject[1] });
                settings.Validate();
                Check(settings.roomCount == 1 && settings.maximumRockSize == 3f
                    && settings.maximumRockfallLength == 5f, "Cave bound validation", ref assertions);
                var clone = settings.Clone();
                clone.roomContentRules[0].seedSalt = 99;
                Check(settings.roomContentRules[0].seedSalt != 99
                    && !ReferenceEquals(settings.roomContentRules[0].prefabs, clone.roomContentRules[0].prefabs),
                    "Cave snapshot deep copy", ref assertions);

                var root = new GameObject("Configuration checks");
                SceneManager.MoveGameObjectToScene(root, scene);
                var health = root.AddComponent<CharacterHealth>();
                SetInt(playerProfile, "health.slotCount", 4);
                SetFloat(playerProfile, "health.refillSecondsPerSlot", 2f);
                SetReference(health, "balanceProfile", playerProfile);
                health.Tick(0f);
                Check(health.SlotCount == 4 && health.ActiveSlotCount == 4,
                    "Health initializes from profile", ref assertions);
                Check(health.TryTakeDamage(), "Health accepts hit", ref assertions);
                health.Tick(1f);
                Check(Mathf.Approximately(health.GetSlotFill(3), 0.5f),
                    "Profile controls health regeneration", ref assertions);
                health.enabled = false;
                health.enabled = true;
                Check(Mathf.Approximately(health.GetSlotFill(3), 0.5f),
                    "Health toggle preserves partial segment", ref assertions);
                health.TryTakeDamage();
                Check(health.ActiveSlotCount == 2 && health.GetSlotFill(3) == 0f,
                    "A hit clears partial refill and one full segment", ref assertions);
                Check(new SerializedObject(health).FindProperty("slotCount").intValue == 3,
                    "Profile leaves legacy health fields intact", ref assertions);

                var incomplete = new GameObject("Incomplete player");
                SceneManager.MoveGameObjectToScene(incomplete, scene);
                string before = EditorJsonUtility.ToJson(playerProfile);
                Check(!playerProfile.CaptureFrom(incomplete, out string warning)
                    && !string.IsNullOrEmpty(warning) && before == EditorJsonUtility.ToJson(playerProfile),
                    "Incomplete capture is atomic", ref assertions);

                var complete = new GameObject("Complete player");
                SceneManager.MoveGameObjectToScene(complete, scene);
                var components = new MonoBehaviour[]
                {
                    complete.AddComponent<ProceduralCharacter>(), complete.AddComponent<CharacterHealth>(),
                    complete.AddComponent<CharacterStress>(), complete.AddComponent<CharacterKnockdown>(),
                    complete.AddComponent<TorchAttack>(), complete.AddComponent<TorchInteraction>(),
                    complete.AddComponent<CharacterDeath>()
                };
                SetFloat(components[0], "speed", 2.5f);
                Check(playerProfile.CaptureFrom(complete, out warning)
                    && Mathf.Approximately(playerProfile.Movement.WalkSpeed, 2.5f),
                    "Complete capture copies authored movement", ref assertions);
                Check(playerProfile.ApplyTo(complete, out warning) && string.IsNullOrEmpty(warning),
                    "Complete player assignment succeeds", ref assertions);
                bool allAssigned = true;
                foreach (var component in components)
                    allAssigned &= new SerializedObject(component).FindProperty("balanceProfile").objectReferenceValue == playerProfile;
                Check(allAssigned, "All seven player components share the assigned profile", ref assertions);
                Check(!BalanceProfileActions.Capture(playerProfile, null, out warning)
                    && !BalanceProfileActions.Assign(playerProfile, null, out warning),
                    "Editor actions reject a missing target", ref assertions);

                var torchObject = new GameObject("Test torch");
                SceneManager.MoveGameObjectToScene(torchObject, scene);
                var torch = torchObject.AddComponent<HandheldTorch>();
                SetFloat(torchProfile, "fuel.lifetime", 20f);
                SetFloat(torchProfile, "fuel.strikeLifetimeCost", 3f);
                SetReference(torch, "balanceProfile", torchProfile);
                torch.Tick(2f, 2f);
                Check(Mathf.Approximately(torch.RemainingLifetime, 18f), "Profile torch burn rate", ref assertions);
                torch.ConsumeStrikeFuel();
                Check(Mathf.Approximately(torch.RemainingLifetime, 15f), "Profile strike fuel cost", ref assertions);
                torch.enabled = false;
                torch.enabled = true;
                Check(Mathf.Approximately(torch.RemainingLifetime, 15f), "Torch toggle preserves fuel", ref assertions);
                Check(new SerializedObject(torchProfile).FindProperty("fuel.lifetime").floatValue == 20f,
                    "Burning does not mutate shared profile", ref assertions);

                var treasureObject = new GameObject("Renamed collectible");
                SceneManager.MoveGameObjectToScene(treasureObject, scene);
                var treasure = treasureObject.AddComponent<Treasure>();
                SetInt(definition, "value", 37);
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("displayName").stringValue = "Designer item";
                serialized.FindProperty("isCoin").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                treasure.AssignDefinition(definition);
                Check(treasure.Value == 37 && treasure.DisplayName == "Designer item" && treasure.IsCoin,
                    "Treasure identity is independent of object name", ref assertions);
                treasure.SetValue(42);
                Check(treasure.Value == 42 && definition.Value == 37,
                    "Per-instance treasure value preserves definition", ref assertions);
                Debug.Log("Configuration checks passed: " + assertions + " assertions. Edit Mode direct calls; no physical input test.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                UnityEngine.Object.DestroyImmediate(playerProfile);
                UnityEngine.Object.DestroyImmediate(torchProfile);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private static void Check(bool condition, string label, ref int assertions)
        {
            if (!condition) throw new InvalidOperationException("Configuration check failed: " + label);
            assertions++;
        }

        private static void SetFloat(UnityEngine.Object target, string property, float value)
        {
            var serialized = new SerializedObject(target);
            var field = serialized.FindProperty(property);
            if (field == null) throw new InvalidOperationException("Missing test field: " + property);
            field.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(UnityEngine.Object target, string property, int value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetReference(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
