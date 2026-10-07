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
            if (!conductor.IsReady) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live rhythm", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Mode", conductor.Mode + (conductor.IsMoving ? " (moving)" : " (standing)"));
            EditorGUILayout.LabelField("Tempo", conductor.Bpm.ToString("0") + " BPM x " + conductor.StepsPerBeat
                + " = " + conductor.StepsPerSecond.ToString("0.00") + " steps/s"
                + (conductor.HasPendingTempo ? "  (change on next beat)" : ""));
            EditorGUILayout.LabelField("Next beat", conductor.NextBeatIndex + "  (bar beat " + (conductor.NextBeatIndex % 4 + 1) + ")");
            EditorGUILayout.LabelField("Step position", conductor.StepPosition.ToString("0.00")
                + "  (" + (((long)System.Math.Floor(conductor.StepPosition) & 1L) == 0L ? "left" : "right") + " foot)");
            Rect bar = EditorGUILayout.GetControlRect(false, 16f);
            EditorGUI.ProgressBar(bar, conductor.BeatPhase, "Beat phase " + conductor.BeatPhase.ToString("0.00"));
        }
    }
}
