using System.Collections.Generic;
using CardsUnity.Data;
using UnityEngine;

namespace CardsUnity.Runtime
{
    public static class CharacterRigValidationUtility
    {
        public static CharacterRigValidationReport Validate(
            Transform worldRoot,
            Transform physicalRoot,
            Rigidbody rootBody,
            bool hasAnimator,
            bool isHumanoidAnimator,
            IReadOnlyList<CharacterBodySegment> segments)
        {
            var report = new CharacterRigValidationReport();

            if (worldRoot == null)
            {
                report.AddIssue("World root is missing.");
            }

            if (!hasAnimator)
            {
                report.AddIssue("Animator reference is missing.");
                return report;
            }

            if (!isHumanoidAnimator)
            {
                report.AddIssue("Animator avatar is not configured as Humanoid.");
                return report;
            }

            if (physicalRoot == null)
            {
                report.AddIssue("Physical root is missing. Expected the humanoid hips transform.");
            }

            if (rootBody == null)
            {
                report.AddIssue("Root body is missing. Expected a Rigidbody on the hips segment.");
            }

            if (segments == null || segments.Count == 0)
            {
                report.AddIssue("No body segments are configured.");
                return report;
            }

            foreach (var requiredBone in CharacterRigBoneUtility.GetRequiredBones())
            {
                var segment = FindSegment(requiredBone, segments);
                if (segment == null)
                {
                    report.AddMissingRequiredBone(requiredBone, $"Missing required segment entry for {requiredBone}.");
                    continue;
                }

                if (segment.transform == null)
                {
                    report.AddMissingRequiredBone(requiredBone, $"{requiredBone} is missing a transform reference.");
                }

                if (segment.body == null)
                {
                    report.AddIssue($"{requiredBone} is missing a Rigidbody reference.");
                }

                if (segment.primaryCollider == null)
                {
                    report.AddIssue($"{requiredBone} is missing a Collider reference.");
                }

                if (requiredBone != CharacterRigBone.Hips && segment.joint == null)
                {
                    report.AddIssue($"{requiredBone} is missing a ConfigurableJoint reference.");
                }
            }

            return report;
        }

        private static CharacterBodySegment FindSegment(CharacterRigBone bone, IReadOnlyList<CharacterBodySegment> segments)
        {
            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                if (segment != null && segment.bone == bone)
                {
                    return segment;
                }
            }

            return null;
        }
    }
}
