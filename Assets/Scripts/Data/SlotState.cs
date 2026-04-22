namespace CardsUnity
{
    public class SlotState
    {
        public CardInstance OpponentCard { get; set; }
        public CardInstance PlayerCard { get; set; }

        public bool HasOpponentCard => OpponentCard != null;
        public bool HasPlayerCard => PlayerCard != null;
    }
}
