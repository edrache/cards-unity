using UnityEngine;

namespace CardsUnity.Runtime
{
    public static class ConfigurableJointExtensions
    {
        public static void SetTargetRotationLocal(
            this ConfigurableJoint joint,
            Quaternion targetLocalRotation,
            Quaternion startLocalRotation)
        {
            var right = joint.axis;
            var forward = Vector3.Cross(joint.axis, joint.secondaryAxis).normalized;
            var up = Vector3.Cross(forward, right).normalized;
            var worldToJointSpace = Quaternion.LookRotation(forward, up);

            var resultRotation = Quaternion.Inverse(worldToJointSpace);
            resultRotation *= Quaternion.Inverse(targetLocalRotation) * startLocalRotation;
            resultRotation *= worldToJointSpace;

            joint.targetRotation = resultRotation;
        }
    }
}
