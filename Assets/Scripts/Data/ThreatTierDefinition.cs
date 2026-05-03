using UnityEngine;

namespace CardsUnity
{
    [CreateAssetMenu(menuName = "CardsUnity/Threat Tier Definition", fileName = "NewThreatTier")]
    public class ThreatTierDefinition : ScriptableObject
    {
        [Min(1)]
        public int maxValue = 5;

        [Tooltip("Slot spawned on the board when this tier is depleted. Leave null for the final tier (game over instead).")]
        public SlotDefinition penaltySlot;

        [Tooltip("Status attached to the player when this tier is depleted. Can combine with penaltySlot.")]
        public StatusDefinition penaltyStatus;
    }
}
