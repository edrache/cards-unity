using System.Collections.Generic;
using CardsUnity.Data;

namespace CardsUnity.Runtime
{
    public sealed class CharacterRigValidationReport
    {
        private readonly List<string> issues = new();
        private readonly List<CharacterRigBone> missingRequiredBones = new();

        public bool IsValid => issues.Count == 0;
        public IReadOnlyList<string> Issues => issues;
        public IReadOnlyList<CharacterRigBone> MissingRequiredBones => missingRequiredBones;

        public void AddIssue(string issue)
        {
            if (string.IsNullOrWhiteSpace(issue))
            {
                return;
            }

            issues.Add(issue);
        }

        public void AddMissingRequiredBone(CharacterRigBone bone, string issue)
        {
            if (!missingRequiredBones.Contains(bone))
            {
                missingRequiredBones.Add(bone);
            }

            AddIssue(issue);
        }
    }
}
