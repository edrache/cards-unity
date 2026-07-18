using UnityEngine;

namespace CardsUnity.Data
{
    public readonly struct StepRequest
    {
        public static StepRequest Invalid => default;

        public StepRequest(StepSide side, Vector3 targetPoint, float duration, float stepHeight)
        {
            Side = side;
            TargetPoint = targetPoint;
            Duration = Mathf.Max(0.01f, duration);
            StepHeight = Mathf.Max(0f, stepHeight);
        }

        public StepSide Side { get; }
        public Vector3 TargetPoint { get; }
        public float Duration { get; }
        public float StepHeight { get; }
        public bool IsValid => Side != StepSide.None;
    }
}
