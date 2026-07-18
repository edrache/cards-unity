using UnityEngine;

namespace CardsUnity.Config
{
    [CreateAssetMenu(fileName = "BalanceTuning", menuName = "Cards Unity/Balance Tuning")]
    public sealed class BalanceTuning : ScriptableObject
    {
        [Min(0f)]
        public float recoverDistance = 0.15f;

        [Min(0f)]
        public float stepDistance = 0.35f;

        [Min(0f)]
        public float fallingDistance = 0.6f;

        [Range(-1f, 1f)]
        public float stableVelocityThreshold = 0.1f;
    }
}
