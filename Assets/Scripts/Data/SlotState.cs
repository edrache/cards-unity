namespace CardsUnity
{
    public class SlotState
    {
        public SlotDefinition Definition { get; private set; }
        public CardInstance OpponentCard { get; set; }
        public CardInstance PlayerCard { get; set; }
        public bool IsActive { get; private set; } = true;
        public SlotClockState ClockState { get; private set; }

        public bool HasOpponentCard => OpponentCard != null;
        public bool HasPlayerCard => PlayerCard != null;

        public SlotState() { }

        public SlotState(SlotDefinition definition)
        {
            Definition = definition;
            ClockState = BuildClockState(definition);
        }

        public SlotState(SlotDefinition definition, bool isActive)
        {
            Definition = definition;
            IsActive = isActive;
            ClockState = BuildClockState(definition);
        }

        public void SetDefinition(SlotDefinition definition)
        {
            Definition = definition;
            ClockState = BuildClockState(definition);
        }

        public void SetActive(bool isActive)
        {
            IsActive = isActive;
        }

        private static SlotClockState BuildClockState(SlotDefinition definition)
        {
            if (definition?.clock == null || definition.clock.maxValue <= 0)
                return null;

            return new SlotClockState(definition.clock.maxValue);
        }
    }
}
