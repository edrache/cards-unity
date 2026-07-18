using UnityEngine;

namespace CardsUnity.Config
{
    [CreateAssetMenu(fileName = "ActiveRagdollTuning", menuName = "Cards Unity/Active Ragdoll Tuning")]
    public sealed class ActiveRagdollTuning : ScriptableObject
    {
        [Min(0f)]
        public float jointSpring = 500f;

        [Min(0f)]
        public float jointDamper = 50f;

        [Min(0f)]
        public float maxJointForce = 500f;

        [Range(0f, 1f)]
        public float poseBlend = 1f;

        [Min(0f)]
        public float bodyTorqueSpring = 40f;

        [Min(0f)]
        public float bodyTorqueDamper = 4f;

        [Min(0f)]
        public float maxBodyTorque = 80f;

        [Min(0f)]
        public float rootUprightSpring = 120f;

        [Min(0f)]
        public float rootUprightDamper = 12f;

        [Min(0f)]
        public float maxRootUprightTorque = 160f;

        [Min(0f)]
        public float rootPositionSpring = 120f;

        [Min(0f)]
        public float rootPositionDamper = 18f;

        [Min(0f)]
        public float maxRootPositionForce = 250f;

        [Min(0f)]
        public float rootHeightSpring = 220f;

        [Min(0f)]
        public float rootHeightDamper = 24f;

        [Min(0f)]
        public float maxRootHeightForce = 350f;

        [Min(0f)]
        public float footSupportSpring = 100f;

        [Min(0f)]
        public float footSupportDamper = 12f;

        [Min(0f)]
        public float maxFootSupportForce = 160f;

        [Min(0f)]
        public float footProbeHeight = 0.5f;

        [Min(0.1f)]
        public float footProbeDistance = 2f;

        [Min(0f)]
        public float groundedFootOffset = 0.03f;

        public LayerMask supportGroundMask = Physics.DefaultRaycastLayers;

        public bool disableAnimatorWhileActive = true;
        public bool capturePoseOnEnable = true;
        public bool alignPhysicalRigOnEnable = true;

        public bool startActive = true;
    }
}
