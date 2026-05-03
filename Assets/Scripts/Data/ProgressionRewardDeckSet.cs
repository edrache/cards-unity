using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity
{
    [CreateAssetMenu(menuName = "CardsUnity/Progression Reward Deck Set", fileName = "ProgressionRewardDeckSet")]
    public class ProgressionRewardDeckSet : ScriptableObject
    {
        public List<CardDefinition> forceCards = new();
        public List<CardDefinition> witCards = new();
        public List<CardDefinition> presenceCards = new();
        public List<CardDefinition> fullTieCards = new();

        public CardDefinition GetRewardCard(StoryEffectResolutionKey key, System.Random rng)
        {
            if (rng == null)
                throw new ArgumentNullException(nameof(rng));

            var rewardPool = key switch
            {
                StoryEffectResolutionKey.Force => forceCards,
                StoryEffectResolutionKey.Wit => witCards,
                StoryEffectResolutionKey.Presence => presenceCards,
                StoryEffectResolutionKey.FullTie => fullTieCards,
                _ => null
            };

            if (rewardPool == null || rewardPool.Count == 0)
                return null;

            return rewardPool[rng.Next(rewardPool.Count)];
        }
    }
}
