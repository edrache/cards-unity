namespace CardsUnity
{
    public class PlayedCardCounterState
    {
        public int Force { get; private set; }
        public int Presence { get; private set; }
        public int Wit { get; private set; }

        public void Add(CardType type, int amount)
        {
            if (amount == 0)
                return;

            switch (type)
            {
                case CardType.Force:
                    Force += amount;
                    break;
                case CardType.Presence:
                    Presence += amount;
                    break;
                case CardType.Wit:
                    Wit += amount;
                    break;
            }
        }

        public int Get(CardType type)
        {
            return type switch
            {
                CardType.Force => Force,
                CardType.Presence => Presence,
                CardType.Wit => Wit,
                _ => 0
            };
        }

        public void Reset()
        {
            Force = 0;
            Presence = 0;
            Wit = 0;
        }
    }
}
