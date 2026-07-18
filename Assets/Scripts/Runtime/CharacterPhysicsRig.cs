using System.Collections.Generic;
using System.Text;
using CardsUnity.Data;
using UnityEngine;

namespace CardsUnity.Runtime
{
    public sealed class CharacterPhysicsRig : MonoBehaviour
    {
        [SerializeField] private bool autoPopulateOnReset = true;
        [SerializeField] private Transform worldRoot;
        [SerializeField] private Transform physicalRoot;
        [SerializeField] private Rigidbody rootBody;
        [SerializeField] private Animator animator;
        [SerializeField] private List<CharacterBodySegment> segments = new();

        public Transform WorldRoot => worldRoot != null ? worldRoot : transform;
        public Transform PhysicalRoot => physicalRoot;
        public Rigidbody RootBody => rootBody;
        public Animator Animator => animator;
        public IReadOnlyList<CharacterBodySegment> Segments => segments;

        public bool HasRig => rootBody != null && physicalRoot != null && segments.Count > 0;

        private void Reset()
        {
            if (!autoPopulateOnReset)
            {
                return;
            }

            AutoAssignSceneReferences();
            RebuildSegmentsFromAnimator();
        }

        private void OnValidate()
        {
            AutoAssignSceneReferences();
        }

        public bool TryGetSegment(CharacterRigBone bone, out CharacterBodySegment segment)
        {
            foreach (var candidate in segments)
            {
                if (candidate != null && candidate.bone == bone)
                {
                    segment = candidate;
                    return true;
                }
            }

            segment = null;
            return false;
        }

        public void GetBodies(List<Rigidbody> results)
        {
            results.Clear();

            foreach (var segment in segments)
            {
                if (segment?.body != null)
                {
                    results.Add(segment.body);
                }
            }
        }

        [ContextMenu("Auto-Assign Scene References")]
        public void AutoAssignSceneReferences()
        {
            if (worldRoot == null)
            {
                worldRoot = transform;
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (animator == null)
            {
                animator = GetComponentInParent<Animator>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (animator == null || !animator.isHuman)
            {
                return;
            }

            if (physicalRoot == null)
            {
                physicalRoot = ResolveBoneTransform(CharacterRigBone.Hips);
            }

            if (rootBody == null && physicalRoot != null)
            {
                rootBody = physicalRoot.GetComponent<Rigidbody>();
            }
        }

        [ContextMenu("Rebuild Segments From Animator")]
        public void RebuildSegmentsFromAnimator()
        {
            AutoAssignSceneReferences();

            if (animator == null || !animator.isHuman)
            {
                return;
            }

            var rebuiltSegments = new List<CharacterBodySegment>();
            var coreBones = CharacterRigBoneUtility.GetCoreBones();

            foreach (var bone in coreBones)
            {
                var boneTransform = ResolveBoneTransform(bone);
                if (boneTransform == null)
                {
                    continue;
                }

                var segment = FindExistingSegment(bone) ?? new CharacterBodySegment();
                segment.bone = bone;
                segment.transform = boneTransform;
                segment.body = boneTransform.GetComponent<Rigidbody>();
                segment.primaryCollider = boneTransform.GetComponent<Collider>();
                segment.joint = boneTransform.GetComponent<ConfigurableJoint>();
                rebuiltSegments.Add(segment);
            }

            segments = rebuiltSegments;

            if (TryGetSegment(CharacterRigBone.Hips, out var hipsSegment) && hipsSegment?.transform != null)
            {
                physicalRoot = hipsSegment.transform;
                rootBody = hipsSegment.body != null ? hipsSegment.body : physicalRoot.GetComponent<Rigidbody>();
            }
            else if (rootBody == null && physicalRoot != null)
            {
                rootBody = physicalRoot.GetComponent<Rigidbody>();
            }
        }

        public bool IsHumanoidReady()
        {
            return animator != null && animator.isHuman;
        }

        public CharacterRigValidationReport ValidateRig()
        {
            return CharacterRigValidationUtility.Validate(
                worldRoot,
                physicalRoot,
                rootBody,
                animator != null,
                animator != null && animator.isHuman,
                segments);
        }

        [ContextMenu("Log Validation Report")]
        public void LogValidationReport()
        {
            var report = ValidateRig();
            if (report.IsValid)
            {
                Debug.Log($"{name}: rig validation passed with {segments.Count} configured segments.", this);
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine($"{name}: rig validation found issues:");
            foreach (var issue in report.Issues)
            {
                builder.Append("- ").AppendLine(issue);
            }

            Debug.LogWarning(builder.ToString(), this);
        }

        public int CountConfiguredSegments()
        {
            var count = 0;
            foreach (var segment in segments)
            {
                if (segment != null && segment.IsConfigured)
                {
                    count++;
                }
            }

            return count;
        }

        public void CollectValidationIssues(List<string> issues)
        {
            if (issues == null)
            {
                return;
            }

            issues.Clear();
            var report = ValidateRig();
            foreach (var issue in report.Issues)
            {
                issues.Add(issue);
            }
        }

        private CharacterBodySegment FindExistingSegment(CharacterRigBone bone)
        {
            foreach (var segment in segments)
            {
                if (segment != null && segment.bone == bone)
                {
                    return segment;
                }
            }

            return null;
        }

        private Transform ResolveBoneTransform(CharacterRigBone bone)
        {
            if (animator == null || !animator.isHuman)
            {
                return null;
            }

            var candidateBones = CharacterRigBoneUtility.GetCandidateHumanBodyBones(bone);
            for (var i = 0; i < candidateBones.Count; i++)
            {
                var candidate = candidateBones[i];
                if (candidate == HumanBodyBones.LastBone)
                {
                    continue;
                }

                var boneTransform = animator.GetBoneTransform(candidate);
                if (boneTransform != null)
                {
                    return boneTransform;
                }
            }

            return null;
        }
    }
}
