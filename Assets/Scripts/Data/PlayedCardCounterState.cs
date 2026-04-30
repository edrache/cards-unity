namespace CardsUnity
{
    public class PlayedCardCounterState
    {
        public int Pressure { get; private set; }
        public int Appeal { get; private set; }
        public int Positioning { get; private set; }

        public void Add(CardType type, int amount)
        {
            if (amount == 0)
                return;

            switch (type)
            {
                case CardType.Pressure:
                    Pressure += amount;
                    break;
                case CardType.Appeal:
                    Appeal += amount;
                    break;
                case CardType.Positioning:
                    Positioning += amount;
                    break;
            }
        }

        public int Get(CardType type)
        {
            return type switch
            {
                CardType.Pressure => Pressure,
                CardType.Appeal => Appeal,
                CardType.Positioning => Positioning,
                _ => 0
            };
        }

        public void Reset()
        {
            Pressure = 0;
            Appeal = 0;
            Positioning = 0;
        }
    }
}
