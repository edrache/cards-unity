using UnityEngine;

namespace CardsUnity
{
    public enum SlotEffectValueSource
    {
        FixedValue,
        CardOnSlotValue,
    }

    public enum SlotEffectSlotCardTarget
    {
        OpponentCard,
        PlayerCard,
    }

    public enum SlotEffectTargetCardType
    {
        Force,
        Presence,
        Wit,
        OpponentCard,
        PlayerCard,
    }

    [CreateAssetMenu(menuName = "CardsUnity/Slot Effect Definition", fileName = "NewSlotEffect")]
    public class SlotEffectDefinition : ScriptableObject
    {
        public SlotEffectContext context;
        public SlotEffectTrigger trigger;
        public SlotEffectAction action;
        public int actionValue;
        public SlotEffectValueSource valueSource = SlotEffectValueSource.FixedValue;
        public SlotEffectSlotCardTarget slotCardTarget = SlotEffectSlotCardTarget.OpponentCard;
        public SlotEffectTargetCardType targetCardType = SlotEffectTargetCardType.Force;
        public ClockTarget clockTarget;
        [TextArea] public string description;
    }
}
