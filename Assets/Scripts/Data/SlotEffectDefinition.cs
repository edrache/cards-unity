using UnityEngine;

namespace CardsUnity
{
    [CreateAssetMenu(menuName = "CardsUnity/Slot Effect Definition", fileName = "NewSlotEffect")]
    public class SlotEffectDefinition : ScriptableObject
    {
        public SlotEffectContext context;
        public SlotEffectTrigger trigger;
        public SlotEffectAction action;
        public int actionValue;
        public CardType targetCardType;
        public ClockTarget clockTarget;
        [TextArea] public string description;
    }
}
