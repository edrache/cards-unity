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
    }
}
