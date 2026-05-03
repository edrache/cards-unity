using System;

namespace CardsUnity
{
    public class ProgressionState
    {
        public int CurrentXp { get; private set; }
        public int XpCap { get; }
        public int CurrentTier { get; private set; }
        public float Progress => XpCap > 0 ? (float)CurrentXp / XpCap : 0f;

        public PlayedCardCounterState DefeatedCounters { get; } = new PlayedCardCounterState();

        public event Action<StoryEffectResolutionKey> OnLevelUp;

        public ProgressionState(int xpCap)
        {
            if (xpCap <= 0)
                throw new ArgumentOutOfRangeException(nameof(xpCap), "XP cap must be greater than zero.");

            XpCap = xpCap;
        }

        public void AddDefeatedCard(CardType type, int value)
        {
            if (value <= 0)
                return;

            int remainingValue = value;

            while (remainingValue > 0)
            {
                int xpRequired = XpCap - CurrentXp;
                int appliedValue = Math.Min(remainingValue, xpRequired);

                DefeatedCounters.Add(type, appliedValue);
                CurrentXp += appliedValue;
                remainingValue -= appliedValue;

                if (CurrentXp < XpCap)
                    continue;

                var dominantType = PlayedCardCounterDominanceResolver.Resolve(DefeatedCounters);
                CurrentTier++;
                CurrentXp = 0;
                DefeatedCounters.Reset();
                OnLevelUp?.Invoke(dominantType);
            }
        }
    }
}
