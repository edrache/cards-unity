#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CardsUnity.Controllers
{
    public sealed partial class ProceduralCave
    {
        [InitializeOnLoadMethod]
        private static void RegisterGeneratedSelectionGuard()
        {
            EditorApplication.playModeStateChanged -= RedirectBeforePlayModeChange;
            EditorApplication.playModeStateChanged += RedirectBeforePlayModeChange;
            AssemblyReloadEvents.beforeAssemblyReload -= RedirectBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += RedirectBeforeAssemblyReload;
        }

        private static void RedirectBeforePlayModeChange(PlayModeStateChange state)
        {
            // OnDisable is too late: Unity has already captured the Inspector for domain reload.
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
                RedirectBeforeAssemblyReload();
        }

        private static void RedirectBeforeAssemblyReload()
        {
            foreach (var cave in FindObjectsByType<ProceduralCave>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                cave.RedirectGeneratedSelection();
        }

        private void RedirectGeneratedSelection()
        {
            if (generated == null) return;
            bool selected = false;
            foreach (Object target in Selection.objects)
                if (IsGeneratedInspectorTarget(target)) { selected = true; break; }

            // Release cached component editors before disposing their meshes and GameObjects.
            var tracker = ActiveEditorTracker.sharedTracker;
            bool inspected = false;
            foreach (var editor in tracker.activeEditors)
            {
                if (editor == null) continue;
                foreach (Object target in editor.targets)
                    if (IsGeneratedInspectorTarget(target)) { inspected = true; break; }
                if (inspected) break;
            }
            if (!selected && !inspected) return;
            if (inspected && tracker.isLocked) tracker.isLocked = false;
            Selection.activeGameObject = gameObject;
            tracker.ForceRebuild();
        }

        private bool IsGeneratedInspectorTarget(Object target)
        {
            if (target == null) return false;
            Transform candidate = target is GameObject go ? go.transform
                : target is Component component ? component.transform : null;
            return candidate != null && candidate.IsChildOf(generated.transform);
        }
    }
}
#endif
