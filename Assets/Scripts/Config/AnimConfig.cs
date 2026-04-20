using UnityEngine;

namespace CardsUnity.Config
{
    [CreateAssetMenu(fileName = "AnimConfig", menuName = "Cards Unity/Config/Animation Config")]
    public sealed class AnimConfig : ScriptableObject
    {
        [Header("Drag")]
        [SerializeField, Min(0f)]
        private float liftScale = 1.15f;

        [SerializeField, Min(0f)]
        private float tiltMax = 40f;

        [SerializeField, Min(0f)]
        private float snapDuration = 0.05f;

        [SerializeField, Min(0f)]
        private float liftDuration = 0.55f;

        [SerializeField, Min(0f)]
        private float slotSnapRadius = 100f;

        [Header("Combat Resolution")]
        [SerializeField, Min(0f)]
        private float resolveFlashDuration = 0.45f;

        [SerializeField, Min(0f)]
        private float resolvePauseDuration = 0.22f;

        [SerializeField, Min(0f)]
        private float resolveTiltAngle = 30f;

        [SerializeField, Min(0f)]
        private float cleanupDuration = 0.55f;

        public float LiftScale => liftScale;
        public float TiltMax => tiltMax;
        public float SnapDuration => snapDuration;
        public float LiftDuration => liftDuration;
        public float SlotSnapRadius => slotSnapRadius;
        public float ResolveFlashDuration => resolveFlashDuration;
        public float ResolvePauseDuration => resolvePauseDuration;
        public float ResolveTiltAngle => resolveTiltAngle;
        public float CleanupDuration => cleanupDuration;
    }
}
