using System.Collections.Generic;
using CardsUnity.Data;
using CardsUnity.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests.EditMode
{
    public sealed class CharacterRigValidationUtilityTests
    {
        [Test]
        public void Validate_ReturnsMissingRequiredBones_WhenCoreSegmentsAreMissing()
        {
            var root = new GameObject("RigRoot");
            var hips = new GameObject("Hips");
            var spine = new GameObject("Spine");
            hips.transform.SetParent(root.transform);
            spine.transform.SetParent(root.transform);
            var hipsBody = hips.AddComponent<Rigidbody>();

            var segments = new List<CharacterBodySegment>
            {
                new()
                {
                    bone = CharacterRigBone.Hips,
                    transform = hips.transform,
                    body = hipsBody,
                    primaryCollider = hips.AddComponent<BoxCollider>()
                },
                new()
                {
                    bone = CharacterRigBone.Spine,
                    transform = spine.transform
                }
            };

            var report = CharacterRigValidationUtility.Validate(
                root.transform,
                hips.transform,
                hipsBody,
                hasAnimator: true,
                isHumanoidAnimator: true,
                segments);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.MissingRequiredBones, Does.Contain(CharacterRigBone.LeftUpLeg));
            Assert.That(report.MissingRequiredBones, Does.Contain(CharacterRigBone.RightArm));
            Assert.That(report.Issues, Has.Some.Contains("Spine is missing a Rigidbody reference."));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void Validate_ReturnsValid_WhenRequiredSegmentsAreConfigured()
        {
            var root = new GameObject("RigRoot");
            var segments = new List<CharacterBodySegment>();
            Transform hipsTransform = null;
            Rigidbody hipsBody = null;

            foreach (var bone in CharacterRigBoneUtility.GetRequiredBones())
            {
                var segmentObject = new GameObject(bone.ToString());
                segmentObject.transform.SetParent(root.transform);

                var body = segmentObject.AddComponent<Rigidbody>();
                var collider = segmentObject.AddComponent<BoxCollider>();
                ConfigurableJoint joint = null;
                if (bone != CharacterRigBone.Hips)
                {
                    joint = segmentObject.AddComponent<ConfigurableJoint>();
                }

                if (bone == CharacterRigBone.Hips)
                {
                    hipsTransform = segmentObject.transform;
                    hipsBody = body;
                }

                segments.Add(new CharacterBodySegment
                {
                    bone = bone,
                    transform = segmentObject.transform,
                    body = body,
                    primaryCollider = collider,
                    joint = joint
                });
            }

            var report = CharacterRigValidationUtility.Validate(
                root.transform,
                hipsTransform,
                hipsBody,
                hasAnimator: true,
                isHumanoidAnimator: true,
                segments);

            Assert.That(report.IsValid, Is.True);
            Assert.That(report.Issues, Is.Empty);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void Validate_ReturnsRootBodyIssue_WhenHipsBodyIsMissing()
        {
            var root = new GameObject("RigRoot");
            var hips = new GameObject("Hips");
            hips.transform.SetParent(root.transform);

            var segments = new List<CharacterBodySegment>
            {
                new()
                {
                    bone = CharacterRigBone.Hips,
                    transform = hips.transform,
                    primaryCollider = hips.AddComponent<BoxCollider>()
                }
            };

            var report = CharacterRigValidationUtility.Validate(
                root.transform,
                hips.transform,
                rootBody: null,
                hasAnimator: true,
                isHumanoidAnimator: true,
                segments);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Issues, Has.Some.Contains("Root body is missing."));

            Object.DestroyImmediate(root);
        }
    }
}
