using System.Collections.Generic;
using CardsUnity.Data;
using UnityEngine;

namespace CardsUnity.Runtime
{
    public static class CharacterRigBoneUtility
    {
        private static readonly CharacterRigBone[] CoreBones =
        {
            CharacterRigBone.Hips,
            CharacterRigBone.Spine,
            CharacterRigBone.Spine1,
            CharacterRigBone.Spine2,
            CharacterRigBone.Neck,
            CharacterRigBone.Head,
            CharacterRigBone.LeftShoulder,
            CharacterRigBone.LeftArm,
            CharacterRigBone.LeftForeArm,
            CharacterRigBone.RightShoulder,
            CharacterRigBone.RightArm,
            CharacterRigBone.RightForeArm,
            CharacterRigBone.LeftUpLeg,
            CharacterRigBone.LeftLeg,
            CharacterRigBone.LeftFoot,
            CharacterRigBone.RightUpLeg,
            CharacterRigBone.RightLeg,
            CharacterRigBone.RightFoot
        };

        private static readonly CharacterRigBone[] RequiredBones =
        {
            CharacterRigBone.Hips,
            CharacterRigBone.Spine,
            CharacterRigBone.LeftUpLeg,
            CharacterRigBone.LeftLeg,
            CharacterRigBone.RightUpLeg,
            CharacterRigBone.RightLeg,
            CharacterRigBone.LeftArm,
            CharacterRigBone.LeftForeArm,
            CharacterRigBone.RightArm,
            CharacterRigBone.RightForeArm
        };

        private static readonly HumanBodyBones[] EmptyHumanBoneCandidates = { };
        private static readonly HumanBodyBones[] HipsCandidates = { HumanBodyBones.Hips };
        private static readonly HumanBodyBones[] SpineCandidates = { HumanBodyBones.Spine };
        private static readonly HumanBodyBones[] ChestCandidates = { HumanBodyBones.Chest, HumanBodyBones.Spine };
        private static readonly HumanBodyBones[] UpperChestCandidates = { HumanBodyBones.UpperChest, HumanBodyBones.Chest, HumanBodyBones.Spine };
        private static readonly HumanBodyBones[] NeckCandidates = { HumanBodyBones.Neck, HumanBodyBones.Head };
        private static readonly HumanBodyBones[] HeadCandidates = { HumanBodyBones.Head };
        private static readonly HumanBodyBones[] LeftShoulderCandidates = { HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm };
        private static readonly HumanBodyBones[] LeftUpperArmCandidates = { HumanBodyBones.LeftUpperArm };
        private static readonly HumanBodyBones[] LeftLowerArmCandidates = { HumanBodyBones.LeftLowerArm };
        private static readonly HumanBodyBones[] RightShoulderCandidates = { HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm };
        private static readonly HumanBodyBones[] RightUpperArmCandidates = { HumanBodyBones.RightUpperArm };
        private static readonly HumanBodyBones[] RightLowerArmCandidates = { HumanBodyBones.RightLowerArm };
        private static readonly HumanBodyBones[] LeftUpperLegCandidates = { HumanBodyBones.LeftUpperLeg };
        private static readonly HumanBodyBones[] LeftLowerLegCandidates = { HumanBodyBones.LeftLowerLeg };
        private static readonly HumanBodyBones[] LeftFootCandidates = { HumanBodyBones.LeftFoot };
        private static readonly HumanBodyBones[] RightUpperLegCandidates = { HumanBodyBones.RightUpperLeg };
        private static readonly HumanBodyBones[] RightLowerLegCandidates = { HumanBodyBones.RightLowerLeg };
        private static readonly HumanBodyBones[] RightFootCandidates = { HumanBodyBones.RightFoot };

        public static IReadOnlyList<CharacterRigBone> GetCoreBones()
        {
            return CoreBones;
        }

        public static IReadOnlyList<CharacterRigBone> GetRequiredBones()
        {
            return RequiredBones;
        }

        public static bool TryGetHumanBodyBone(CharacterRigBone bone, out HumanBodyBones humanBone)
        {
            humanBone = bone switch
            {
                CharacterRigBone.Hips => HumanBodyBones.Hips,
                CharacterRigBone.Spine => HumanBodyBones.Spine,
                CharacterRigBone.Spine1 => HumanBodyBones.Chest,
                CharacterRigBone.Spine2 => HumanBodyBones.UpperChest,
                CharacterRigBone.Neck => HumanBodyBones.Neck,
                CharacterRigBone.Head => HumanBodyBones.Head,
                CharacterRigBone.LeftShoulder => HumanBodyBones.LeftShoulder,
                CharacterRigBone.LeftArm => HumanBodyBones.LeftUpperArm,
                CharacterRigBone.LeftForeArm => HumanBodyBones.LeftLowerArm,
                CharacterRigBone.RightShoulder => HumanBodyBones.RightShoulder,
                CharacterRigBone.RightArm => HumanBodyBones.RightUpperArm,
                CharacterRigBone.RightForeArm => HumanBodyBones.RightLowerArm,
                CharacterRigBone.LeftUpLeg => HumanBodyBones.LeftUpperLeg,
                CharacterRigBone.LeftLeg => HumanBodyBones.LeftLowerLeg,
                CharacterRigBone.LeftFoot => HumanBodyBones.LeftFoot,
                CharacterRigBone.RightUpLeg => HumanBodyBones.RightUpperLeg,
                CharacterRigBone.RightLeg => HumanBodyBones.RightLowerLeg,
                CharacterRigBone.RightFoot => HumanBodyBones.RightFoot,
                _ => HumanBodyBones.LastBone
            };

            return humanBone != HumanBodyBones.LastBone;
        }

        public static HumanBodyBones ToHumanBodyBone(CharacterRigBone bone)
        {
            return TryGetHumanBodyBone(bone, out var humanBone) ? humanBone : HumanBodyBones.LastBone;
        }

        public static IReadOnlyList<HumanBodyBones> GetCandidateHumanBodyBones(CharacterRigBone bone)
        {
            return bone switch
            {
                CharacterRigBone.Hips => HipsCandidates,
                CharacterRigBone.Spine => SpineCandidates,
                CharacterRigBone.Spine1 => ChestCandidates,
                CharacterRigBone.Spine2 => UpperChestCandidates,
                CharacterRigBone.Neck => NeckCandidates,
                CharacterRigBone.Head => HeadCandidates,
                CharacterRigBone.LeftShoulder => LeftShoulderCandidates,
                CharacterRigBone.LeftArm => LeftUpperArmCandidates,
                CharacterRigBone.LeftForeArm => LeftLowerArmCandidates,
                CharacterRigBone.RightShoulder => RightShoulderCandidates,
                CharacterRigBone.RightArm => RightUpperArmCandidates,
                CharacterRigBone.RightForeArm => RightLowerArmCandidates,
                CharacterRigBone.LeftUpLeg => LeftUpperLegCandidates,
                CharacterRigBone.LeftLeg => LeftLowerLegCandidates,
                CharacterRigBone.LeftFoot => LeftFootCandidates,
                CharacterRigBone.RightUpLeg => RightUpperLegCandidates,
                CharacterRigBone.RightLeg => RightLowerLegCandidates,
                CharacterRigBone.RightFoot => RightFootCandidates,
                _ => EmptyHumanBoneCandidates
            };
        }

        public static bool IsRequiredBone(CharacterRigBone bone)
        {
            foreach (var requiredBone in RequiredBones)
            {
                if (requiredBone == bone)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
