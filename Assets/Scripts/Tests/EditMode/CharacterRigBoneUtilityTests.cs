using CardsUnity.Data;
using CardsUnity.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests.EditMode
{
    public sealed class CharacterRigBoneUtilityTests
    {
        [Test]
        public void GetCoreBones_ContainsHipsAndFeet()
        {
            var bones = CharacterRigBoneUtility.GetCoreBones();

            Assert.That(bones, Does.Contain(CharacterRigBone.Hips));
            Assert.That(bones, Does.Contain(CharacterRigBone.LeftFoot));
            Assert.That(bones, Does.Contain(CharacterRigBone.RightFoot));
        }

        [Test]
        public void ToHumanBodyBone_MapsUpperChestAndHips()
        {
            Assert.That(
                CharacterRigBoneUtility.ToHumanBodyBone(CharacterRigBone.Hips),
                Is.EqualTo(HumanBodyBones.Hips));

            Assert.That(
                CharacterRigBoneUtility.ToHumanBodyBone(CharacterRigBone.Spine2),
                Is.EqualTo(HumanBodyBones.UpperChest));
        }

        [Test]
        public void GetRequiredBones_ContainsHumanoidCoreSegments()
        {
            var bones = CharacterRigBoneUtility.GetRequiredBones();

            Assert.That(bones, Does.Contain(CharacterRigBone.Hips));
            Assert.That(bones, Does.Contain(CharacterRigBone.LeftArm));
            Assert.That(bones, Does.Contain(CharacterRigBone.RightLeg));
        }

        [Test]
        public void GetCandidateHumanBodyBones_ReturnsFallbackOrder_ForSpine2()
        {
            var candidates = CharacterRigBoneUtility.GetCandidateHumanBodyBones(CharacterRigBone.Spine2);

            Assert.That(candidates.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(candidates[0], Is.EqualTo(HumanBodyBones.UpperChest));
            Assert.That(candidates[1], Is.EqualTo(HumanBodyBones.Chest));
            Assert.That(candidates[2], Is.EqualTo(HumanBodyBones.Spine));
        }
    }
}
