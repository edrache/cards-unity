using UnityEditor;
using CardsUnity.Rendering;

namespace CardsUnity.Controllers.Editor
{
    [CustomEditor(typeof(CharacterDepthOfField)), CanEditMultipleObjects]
    public sealed class CharacterDepthOfFieldEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Draw("target", "volume", "mode", "blurStrength");
            var mode = serializedObject.FindProperty("mode");
            if (mode.hasMultipleDifferentValues)
            {
                EditorGUILayout.HelpBox("Select a common mode to edit its focus shape.", MessageType.Info);
            }
            else
            {
                switch ((CharacterDepthOfField.FocusMode)mode.enumValueIndex)
                {
                    case CharacterDepthOfField.FocusMode.Circle:
                        Draw("centerOffset", "sharpRadius", "circleTransition");
                        break;
                    case CharacterDepthOfField.FocusMode.TiltShift:
                        Draw("centerOffset", "tiltAngle", "upperSharpWidth", "lowerSharpWidth",
                            "upperTransition", "lowerTransition");
                        break;
                    default:
                        Draw("sharpMargin", "blurRange");
                        break;
                }
            }
            EditorGUILayout.HelpBox("Circle and Tilt Shift use fractions of viewport height (0.1 = 10%). " +
                "Preview in Game view. The assigned Volume needs a Depth Of Field override and the camera needs Post Processing. " +
                "Disable this component to return to the Volume's settings.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
        }

        private void Draw(params string[] names)
        {
            foreach (string name in names)
                EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
        }
    }
}
