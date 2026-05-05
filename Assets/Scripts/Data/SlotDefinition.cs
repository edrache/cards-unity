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
        [Tooltip("If true, runtime-spawned instances of this slot are parented under BoardView.passiveSlotContainer instead of the main board container.")]
        public bool spawnInPassiveContainer;
        [Tooltip("If true, cards played into this slot contribute to the global played-card counters.")]
        public bool contributesToGlobalPlayedCardCounts;
        [Tooltip("Controls whether this slot contributes the card's current value or a fixed value to the global played-card counters.")]
        public PlayedCardCountMode playedCardCountMode = PlayedCardCountMode.UseCardValue;
        [Tooltip("When using a fixed contribution mode, every card played into this slot adds this value to the matching global type counter.")]
        public int fixedPlayedCardCountValue = 1;
        [Tooltip("Optional clock attached to this slot. maxValue > 0 activates it. Use a ProgressSlotClock effect to advance it.")]
        public SlotClockConfig clock;
        public List<SlotEffectDefinition> effects = new List<SlotEffectDefinition>();
    }
}
