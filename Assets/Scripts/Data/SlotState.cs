namespace CardsUnity
{
    public class SlotState
    {
        public SlotDefinition Definition { get; private set; }
        public CardInstance OpponentCard { get; set; }
        public CardInstance PlayerCard { get; set; }
        public bool IsActive { get; private set; } = true;

        public bool HasOpponentCard => OpponentCard != null;
        public bool HasPlayerCard => PlayerCard != null;

        public SlotState() { }

        public SlotState(SlotDefinition definition)
        {
            Definition = definition;
        }

        public SlotState(SlotDefinition definition, bool isActive)
        {
            Definition = definition;
            IsActive = isActive;
        }

        public void SetDefinition(SlotDefinition definition)
        {
            Definition = definition;
        }

        public void SetActive(bool isActive)
        {
            IsActive = isActive;
        }
    }
}
