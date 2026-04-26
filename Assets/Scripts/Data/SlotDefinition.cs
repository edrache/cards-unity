using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity
{
    [CreateAssetMenu(menuName = "CardsUnity/Slot Definition", fileName = "NewSlot")]
    public class SlotDefinition : ScriptableObject
    {
        public bool hasOpponentCardSpot = true;
        public bool hasPlayerCardSpot = true;
        public List<SlotEffectDefinition> effects = new List<SlotEffectDefinition>();
    }
}
