using UnityEditor;
using UnityEngine;

namespace CardsUnity.Controllers.Editor
{
    public abstract class BalanceProfileEditor : UnityEditor.Editor
    {
        private GameObject destination;
        private string message;

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("This asset is shared by every component that references it. Tune in Edit Mode; restart Play Mode for initial health, stress and cached enemy state. Capture reads local fallback fields, not a previously assigned profile.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                DrawDefaultInspector();
            EditorGUILayout.Space();
            destination = (GameObject)EditorGUILayout.ObjectField("Target root", destination, typeof(GameObject), true);
            using (new EditorGUI.DisabledScope(destination == null || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Capture Local Fallback"))
                {
                    bool success = BalanceProfileActions.Capture((ScriptableObject)target, destination, out message);
                    if (success) AssetDatabase.SaveAssets();
                }
                if (GUILayout.Button("Assign Profile"))
                    BalanceProfileActions.Assign((ScriptableObject)target, destination, out message);
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
        }
    }

    [CustomEditor(typeof(PlayerGameplayBalanceProfile))]
    public sealed class PlayerGameplayBalanceProfileEditor : BalanceProfileEditor { }
    [CustomEditor(typeof(TorchBalanceProfile))]
    public sealed class TorchBalanceProfileEditor : BalanceProfileEditor { }
    [CustomEditor(typeof(CentipedeBalanceProfile))]
    public sealed class CentipedeBalanceProfileEditor : BalanceProfileEditor { }

    public static class BalanceProfileActions
    {
        public static bool Capture(ScriptableObject profile, GameObject root, out string message)
        {
            if (root == null) { message = "Choose a target root."; return false; }
            switch (profile)
            {
                case PlayerGameplayBalanceProfile player: return player.CaptureFrom(root, out message);
                case TorchBalanceProfile torch: return torch.CaptureFrom(root.GetComponent<HandheldTorch>(), out message);
                case CentipedeBalanceProfile centipede: return centipede.CaptureFrom(root.GetComponent<ProceduralCentipede>(), out message);
                default: message = "Unsupported balance profile."; return false;
            }
        }

        public static bool Assign(ScriptableObject profile, GameObject root, out string message)
        {
            if (root == null) { message = "Choose a target root."; return false; }
            bool success;
            switch (profile)
            {
                case PlayerGameplayBalanceProfile player: success = player.ApplyTo(root, out message); break;
                case TorchBalanceProfile torch: success = torch.ApplyTo(root.GetComponent<HandheldTorch>(), out message); break;
                case CentipedeBalanceProfile centipede: success = centipede.ApplyTo(root.GetComponent<ProceduralCentipede>(), out message); break;
                default: message = "Unsupported balance profile."; return false;
            }
            if (success)
            {
                foreach (var component in root.GetComponents<MonoBehaviour>())
                    if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                if (!EditorUtility.IsPersistent(root))
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
                else
                {
                    if (PrefabUtility.IsPartOfPrefabAsset(root))
                        PrefabUtility.SavePrefabAsset(root.transform.root.gameObject);
                    AssetDatabase.SaveAssets();
                }
                if (string.IsNullOrEmpty(message)) message = "Profile assigned. Save the scene or prefab to retain the assignment.";
            }
            return success;
        }

        [MenuItem("CONTEXT/ProceduralCharacter/Create Balance Profile From Local Fallback")]
        [MenuItem("CONTEXT/CharacterHealth/Create Balance Profile From Local Fallback")]
        [MenuItem("CONTEXT/CharacterStress/Create Balance Profile From Local Fallback")]
        [MenuItem("CONTEXT/CharacterKnockdown/Create Balance Profile From Local Fallback")]
        [MenuItem("CONTEXT/TorchAttack/Create Balance Profile From Local Fallback")]
        [MenuItem("CONTEXT/TorchInteraction/Create Balance Profile From Local Fallback")]
        [MenuItem("CONTEXT/CharacterDeath/Create Balance Profile From Local Fallback")]
        [MenuItem("CONTEXT/HandheldTorch/Create Balance Profile From Local Fallback")]
        [MenuItem("CONTEXT/ProceduralCentipede/Create Balance Profile From Local Fallback")]
        private static void CreateProfile(MenuCommand command)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var component = command.context as Component;
            if (component == null) return;
            ScriptableObject profile = component is HandheldTorch ? ScriptableObject.CreateInstance<TorchBalanceProfile>()
                : component is ProceduralCentipede ? ScriptableObject.CreateInstance<CentipedeBalanceProfile>()
                : ScriptableObject.CreateInstance<PlayerGameplayBalanceProfile>();
            if (!Capture(profile, component.gameObject, out string message))
            {
                UnityEngine.Object.DestroyImmediate(profile);
                Debug.LogWarning(message, component);
                return;
            }
            string path = EditorUtility.SaveFilePanelInProject("Create Balance Profile",
                component.gameObject.name + " Balance", "asset", "Save the captured local tuning.");
            if (string.IsNullOrEmpty(path)) { UnityEngine.Object.DestroyImmediate(profile); return; }
            AssetDatabase.CreateAsset(profile, path);
            Undo.RegisterCreatedObjectUndo(profile, "Create Balance Profile");
            Assign(profile, component.gameObject, out message);
            AssetDatabase.SaveAssets();
            Selection.activeObject = profile;
        }
    }
}
