using UnityEngine;

namespace CardsUnity.Config
{
    [CreateAssetMenu(fileName = "StepTuning", menuName = "Cards Unity/Step Tuning")]
    public sealed class StepTuning : ScriptableObject
    {
        [Min(0f)]
        public float triggerDistance = 0.35f;

        [Min(0f)]
        public float maxStepDistance = 0.75f;

        [Min(0f)]
        public float stepHeight = 0.15f;

        [Min(0.01f)]
        public float stepDuration = 0.35f;

        public LayerMask groundMask = Physics.DefaultRaycastLayers;
    }
}
