using System;

namespace CardsUnity
{
    [Serializable]
    public class SlotClockConfig
    {
        public int maxValue = 0;
        public SlotClockCompletionEffect completionEffect = SlotClockCompletionEffect.RemoveSlot;
    }
}
