namespace CardsUnity
{
    public class SlotState
    {
        public SlotDefinition Definition { get; }
        public CardInstance OpponentCard { get; set; }
        public CardInstance PlayerCard { get; set; }

        public bool HasOpponentCard => OpponentCard != null;
        public bool HasPlayerCard => PlayerCard != null;

        public SlotState() { }

        public SlotState(SlotDefinition definition)
        {
            Definition = definition;
        }
    }
}
