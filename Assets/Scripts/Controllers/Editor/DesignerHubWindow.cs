using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardsUnity.Controllers.Editor
{
    /// <summary>Entry point for authored tuning assets, source prefabs and the offline designer guide.</summary>
    public sealed class DesignerHubWindow : EditorWindow
    {
        private string search = string.Empty;
        private Vector2 scroll;
        private UnityEngine.Object[] profiles = Array.Empty<UnityEngine.Object>();
        private UnityEngine.Object[] prefabs = Array.Empty<UnityEngine.Object>();

        [MenuItem("Tools/Cards Unity/Designer Hub")]
        public static void Open() => GetWindow<DesignerHubWindow>("Cave Designer");

        private void OnEnable()
        {
            RefreshAssets();
            EditorApplication.projectChanged += RefreshAssets;
        }

        private void OnDisable() => EditorApplication.projectChanged -= RefreshAssets;

        private void RefreshAssets()
        {
            profiles = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ScriptableObject>)
                .Where(asset => asset != null && asset.GetType().Namespace != null
                    && asset.GetType().Namespace.StartsWith("CardsUnity", StringComparison.Ordinal))
                .OrderBy(asset => asset.name).Cast<UnityEngine.Object>().ToArray();
            prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(asset => asset != null).OrderBy(asset => asset.name)
                .Cast<UnityEngine.Object>().ToArray();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Cave Designer", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Active scene", SceneManager.GetActiveScene().name);
            EditorGUILayout.HelpBox("Tune shared profiles in Edit Mode. Select a source prefab to change its rig, visuals or references. Generated cave children are disposable. See the guide for settings that require rebuilding or restarting Play Mode.", MessageType.Info);
            if (GUILayout.Button("Open Designer Guide")) OpenGuide();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select Cave"))
                    Selection.activeObject = FindObjectsByType<ProceduralCave>(FindObjectsSortMode.None).FirstOrDefault();
                if (GUILayout.Button("Select Player"))
                    Selection.activeObject = FindObjectsByType<ProceduralCharacter>(FindObjectsSortMode.None).FirstOrDefault();
                if (GUILayout.Button("Refresh")) RefreshAssets();
            }
            search = EditorGUILayout.TextField("Search assets", search);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawAssets("Tuning and content profiles", profiles);
            DrawAssets("Source prefabs", prefabs);
            EditorGUILayout.EndScrollView();
        }

        private void DrawAssets(string title, UnityEngine.Object[] assets)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach (var asset in assets)
            {
                if (!asset) continue;
                string path = AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrEmpty(search)
                    && path.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                    && asset.GetType().Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(asset, typeof(UnityEngine.Object), false);
                    if (GUILayout.Button("Select", GUILayout.Width(55)))
                    {
                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                    }
                }
                EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
            }
        }

        [MenuItem("CONTEXT/Treasure/Create Definition From Current Values")]
        private static void CreateTreasureDefinition(MenuCommand command)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var treasure = command.context as Treasure;
            if (treasure == null) return;
            string path = EditorUtility.SaveFilePanelInProject("Create Treasure Definition",
                treasure.name, "asset", "Choose where to save this item's shared definition.");
            if (string.IsNullOrEmpty(path)) return;
            var definition = CreateInstance<TreasureDefinition>();
            definition.CaptureFrom(treasure);
            AssetDatabase.CreateAsset(definition, path);
            Undo.RegisterCreatedObjectUndo(definition, "Create Treasure Definition");
            Undo.RecordObject(treasure, "Assign Treasure Definition");
            treasure.AssignDefinition(definition);
            EditorUtility.SetDirty(treasure);
            if (PrefabUtility.IsPartOfPrefabInstance(treasure))
                PrefabUtility.RecordPrefabInstancePropertyModifications(treasure);
            AssetDatabase.SaveAssets();
            Selection.activeObject = definition;
        }

        [MenuItem("Tools/Cards Unity/Open Designer Guide")]
        public static void OpenGuide()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/designer-guide/index.html"));
            if (File.Exists(path)) Application.OpenURL(new Uri(path).AbsoluteUri);
            else Debug.LogWarning("Designer guide is missing: " + path);
        }
    }
}
