using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CardsUnity.Controllers.Editor
{
    [CustomEditor(typeof(ProceduralCave))]
    public sealed class ProceduralCaveEditor : UnityEditor.Editor
    {
        private bool showLegacyFallback;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox(
                "The optional profile owns reusable generation tuning. Without one, the original component fields remain the exact fallback. " +
                "Profile changes are read at the start of Rebuild Cave. The Player binding always remains scene-specific.",
                MessageType.Info);

            SerializedProperty profileProperty = serializedObject.FindProperty("generationProfile");
            EditorGUILayout.PropertyField(profileProperty);
            serializedObject.ApplyModifiedProperties();

            var cave = (ProceduralCave)target;
            CaveGenerationProfile profile = cave.GenerationProfile;
            if (profile != null)
            {
                DrawInlineProfile(profile);
                showLegacyFallback = EditorGUILayout.Foldout(showLegacyFallback, "Legacy Fallback (inactive)", true);
                if (showLegacyFallback)
                {
                    EditorGUILayout.HelpBox(
                        "These values are retained on the scene component and become active if the profile is cleared. " +
                        "Capture Legacy Into Profile copies them without deleting the profile's room content rules.",
                        MessageType.None);
                    serializedObject.Update();
                    DrawLegacyProperties(serializedObject);
                    serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                serializedObject.Update();
                DrawLegacyProperties(serializedObject);
                serializedObject.ApplyModifiedProperties();
            }

            serializedObject.Update();
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("player"));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(targets.Length != 1))
                {
                    if (GUILayout.Button("Create Profile From Legacy...")) CreateProfileFromLegacy(cave);
                }
                using (new EditorGUI.DisabledScope(profile == null || targets.Length != 1))
                {
                    if (GUILayout.Button("Capture Legacy Into Profile")) CaptureLegacyIntoProfile(cave, profile);
                }
            }

            if (GUILayout.Button("Rebuild Cave"))
            {
                foreach (Object inspectedTarget in targets)
                {
                    var inspectedCave = (ProceduralCave)inspectedTarget;
                    inspectedCave.Rebuild();
                    if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(inspectedCave.gameObject.scene);
                }
            }
        }

        public static CaveGenerationProfile CreateProfileAssetFromLegacy(
            ProceduralCave cave, string assetPath, bool assignToCave = true, bool rebuildImmediately = true)
        {
            if (cave == null) throw new System.ArgumentNullException(nameof(cave));
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/"))
                throw new System.ArgumentException("The profile asset path must start with 'Assets/'.", nameof(assetPath));

            var profile = ScriptableObject.CreateInstance<CaveGenerationProfile>();
            profile.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            cave.CopyLegacySettingsTo(profile, false);
            AssetDatabase.CreateAsset(profile, assetPath);
            Undo.RegisterCreatedObjectUndo(profile, "Create Cave Generation Profile");
            if (assignToCave) AssignProfile(cave, profile, rebuildImmediately);
            AssetDatabase.SaveAssets();
            return profile;
        }

        public static void AssignProfile(
            ProceduralCave cave, CaveGenerationProfile profile, bool rebuildImmediately = true)
        {
            if (cave == null) throw new System.ArgumentNullException(nameof(cave));
            Undo.RecordObject(cave, "Assign Cave Generation Profile");
            cave.AssignGenerationProfile(profile, rebuildImmediately);
            EditorUtility.SetDirty(cave);
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(cave.gameObject.scene);
        }

        private static void DrawLegacyProperties(SerializedObject caveObject)
        {
            DrawPropertiesExcluding(caveObject, "m_Script", "generationProfile", "player");
        }

        private static void DrawInlineProfile(CaveGenerationProfile profile)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Profile Settings", EditorStyles.boldLabel);
            var profileObject = new SerializedObject(profile);
            profileObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(profileObject.FindProperty("settings"), true);
            if (EditorGUI.EndChangeCheck())
            {
                profileObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(profile);
            }
            else profileObject.ApplyModifiedProperties();

            DrawSeedSaltWarnings(profile.Settings.roomContentRules);
            EditorGUILayout.HelpBox(
                "Room content rules use a circular XZ footprint and bounded placement attempts. Set Route Clearance above zero for blocking props or traps. " +
                "Moving actors and ambient content can use zero. Prefab collider size and navigation behaviour are not inferred.",
                MessageType.None);
        }

        private static void DrawSeedSaltWarnings(List<CaveRoomContentRule> rules)
        {
            if (rules == null || rules.Count < 2) return;
            var seen = new HashSet<int>();
            foreach (CaveRoomContentRule rule in rules)
            {
                if (rule == null || !rule.enabled) continue;
                if (!seen.Add(rule.seedSalt))
                {
                    EditorGUILayout.HelpBox(
                        $"Enabled room content rules share Seed Salt {rule.seedSalt}. Give each rule a unique salt for independent layouts.",
                        MessageType.Warning);
                    return;
                }
            }
        }

        private static void CreateProfileFromLegacy(ProceduralCave cave)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Cave Generation Profile", "Cave Generation Profile", "asset",
                "Choose where to save the reusable cave generation profile.");
            if (string.IsNullOrEmpty(path)) return;
            CaveGenerationProfile profile = CreateProfileAssetFromLegacy(cave, path);
            Selection.activeObject = profile;
        }

        private static void CaptureLegacyIntoProfile(ProceduralCave cave, CaveGenerationProfile profile)
        {
            Undo.RecordObject(profile, "Capture Legacy Cave Settings");
            cave.CopyLegacySettingsTo(profile);
            EditorUtility.SetDirty(profile);
            cave.Rebuild();
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(cave.gameObject.scene);
        }
    }

    [CustomEditor(typeof(CaveGenerationProfile))]
    public sealed class CaveGenerationProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox(
                "All caves assigned to this asset read these values on their next rebuild. Room content rules have independent random streams keyed by Seed Salt.",
                MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("settings"), true);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
