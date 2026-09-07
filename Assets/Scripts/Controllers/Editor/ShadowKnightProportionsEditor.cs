using UnityEditor;
using UnityEngine;

namespace CardsUnity.Controllers.Editor
{
    [CustomEditor(typeof(ShadowKnightProportions)), CanEditMultipleObjects]
    public sealed class ShadowKnightProportionsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Dimensions update the rig, leg IK, collider and torch grip. Helmet is a separate nested prefab. Edit outside Play Mode to keep changes.", MessageType.Info);
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            if (!EditorGUI.EndChangeCheck()) return;

            foreach (var item in targets)
                Undo.RegisterFullObjectHierarchyUndo(((ShadowKnightProportions)item).gameObject, "Change knight proportions");
            serializedObject.ApplyModifiedProperties();
            foreach (var item in targets)
            {
                var proportions = (ShadowKnightProportions)item;
                proportions.ApplyProportions();
                foreach (var component in proportions.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    EditorUtility.SetDirty(component);
                    if (PrefabUtility.IsPartOfPrefabInstance(component))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                    if (component is Transform)
                    {
                        EditorUtility.SetDirty(component.gameObject);
                        if (PrefabUtility.IsPartOfPrefabInstance(component.gameObject))
                            PrefabUtility.RecordPrefabInstancePropertyModifications(component.gameObject);
                    }
                }
            }
        }
    }
}
