using UnityEditor;
using UnityEngine;

namespace CardsUnity.Controllers.Editor
{
    [CustomEditor(typeof(RhythmConductor))]
    public sealed class RhythmConductorEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (!Application.isPlaying) return;
            var conductor = (RhythmConductor)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live rhythm", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Tempo", conductor.Bpm.ToString("0") + " BPM (tier " + conductor.TierIndex
                + ", measured " + conductor.MeasuredBpm.ToString("0.0") + ")");
            EditorGUILayout.LabelField("Next beat", conductor.NextBeatIndex + "  (bar beat " + (conductor.NextBeatIndex % 4 + 1) + ")");
            Rect bar = EditorGUILayout.GetControlRect(false, 16f);
            EditorGUI.ProgressBar(bar, conductor.BeatPhase, "Phase " + conductor.BeatPhase.ToString("0.00"));
        }
    }
}
