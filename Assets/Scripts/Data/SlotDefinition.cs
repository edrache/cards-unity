using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity
{
    [CreateAssetMenu(menuName = "CardsUnity/Slot Definition", fileName = "NewSlot")]
    public class SlotDefinition : ScriptableObject
    {
        [Tooltip("If true, an opponent card can occupy this slot and participate in combat.")]
        public bool hasOpponentCardSpot = true;
        [Tooltip("If true, the player can drag a card onto this slot.")]
        public bool hasPlayerCardSpot = true;
        public List<SlotEffectDefinition> effects = new List<SlotEffectDefinition>();
    }
}
