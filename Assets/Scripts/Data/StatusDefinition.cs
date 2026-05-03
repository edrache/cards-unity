using UnityEngine;

namespace CardsUnity
{
    public enum StatusDurationType
    {
        Permanent,
        Turns,
    }

    [CreateAssetMenu(menuName = "CardsUnity/Status Definition", fileName = "NewStatus")]
    public class StatusDefinition : ScriptableObject
    {
        public string statusName;
        public Sprite icon;
        [TextArea] public string description;

        [Header("Duration")]
        public StatusDurationType durationType = StatusDurationType.Turns;
        [Min(1)] public int durationTurns = 3;

        [Header("Per-Turn Effect")]
        public SlotEffectAction action;
        public SlotEffectTargetCardType targetCardType = SlotEffectTargetCardType.Force;
        [Min(0)] public int actionValue;
    }
}
