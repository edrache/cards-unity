using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity
{
    [CreateAssetMenu(menuName = "CardsUnity/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        public int draftValue = 3;
        public int boardSlotCount = 3;
        public List<CardDefinition> startingDeck = new List<CardDefinition>();
        public List<CardDefinition> opponentDeck = new List<CardDefinition>();
        public List<SlotDefinition> slotDefinitions = new List<SlotDefinition>();

        [Header("Threat System")]
        public List<ThreatTierDefinition> forceThreatTiers = new List<ThreatTierDefinition>();
        public List<ThreatTierDefinition> witThreatTiers = new List<ThreatTierDefinition>();
        public List<ThreatTierDefinition> presenceThreatTiers = new List<ThreatTierDefinition>();

        [Header("Progression")]
        [Min(1)] public int progressionXpCap = 10;
        public ProgressionRewardDeckSet progressionRewardDeckSet;

        public List<ThreatTierDefinition> GetThreatTiers(CardType type) => type switch
        {
            CardType.Force => forceThreatTiers,
            CardType.Wit => witThreatTiers,
            CardType.Presence => presenceThreatTiers,
            _ => new List<ThreatTierDefinition>()
        };
    }
}
