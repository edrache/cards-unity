using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CardsUnity.Controllers.Editor
{
    [CustomPropertyDrawer(typeof(CaveRoomContentRule))]
    public sealed class CaveRoomContentRuleDrawer : PropertyDrawer
    {
        private static IEnumerable<SerializedProperty> VisibleFields(SerializedProperty property)
        {
            bool water = property.FindPropertyRelative("contentType").enumValueIndex == (int)CaveRoomContentType.Water;
            bool grass = property.FindPropertyRelative("contentType").enumValueIndex == (int)CaveRoomContentType.Grass;
            foreach (string name in new[] { "label", "enabled", "contentType", "seedSalt", "roomPercentage" })
                yield return property.FindPropertyRelative(name);
            if (grass)
            {
                yield return property.FindPropertyRelative("grass");
                yield break;
            }
            if (water) yield return property.FindPropertyRelative("water");
            foreach (string name in new[] { "prefabs", "minimumPerSelectedRoom", "maximumPerSelectedRoom",
                "footprintRadius", "wallClearance", "routeClearance", "surfaceOffset", "wallPlacementPercentage",
                "minimumWallHeight", "maximumWallHeight", "wallEmbedDepth", "alignToFloor", "randomYaw" })
            {
                if (water && (name == "prefabs" || name == "wallPlacementPercentage" || name == "minimumWallHeight"
                    || name == "maximumWallHeight" || name == "wallEmbedDepth" || name == "alignToFloor")) continue;
                yield return property.FindPropertyRelative(name);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property.isExpanded)
                foreach (var field in VisibleFields(property))
                    height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(field, true);
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            string title = property.FindPropertyRelative("label").stringValue;
            Rect row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded,
                string.IsNullOrWhiteSpace(title) ? label : new GUIContent(title), true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                foreach (var field in VisibleFields(property))
                {
                    row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
                    row.height = EditorGUI.GetPropertyHeight(field, true);
                    EditorGUI.PropertyField(row, field, field.name == "grass" ? new GUIContent("Grass Settings") : null, true);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }
}
