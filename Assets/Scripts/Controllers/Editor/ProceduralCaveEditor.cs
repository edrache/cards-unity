using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CardsUnity.Controllers.Editor
{
    [CustomEditor(typeof(ProceduralCave))]
    public sealed class ProceduralCaveEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Connected chambers with a level floor. The roof is cut away for the overhead camera. Changes rebuild the cave and move the player to the entrance. Keep the root transform at unit scale.", MessageType.Info);
            DrawDefaultInspector();
            if (GUILayout.Button("Rebuild Cave"))
            {
                var cave = (ProceduralCave)target;
                cave.Rebuild();
                if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(cave.gameObject.scene);
            }
        }
    }
}
