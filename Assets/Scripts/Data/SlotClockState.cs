namespace CardsUnity
{
    public class SlotClockState
    {
        public int MaxValue { get; }
        public int CurrentValue { get; private set; }
        public bool IsFull => MaxValue > 0 && CurrentValue >= MaxValue;
        public float Progress => MaxValue > 0 ? (float)CurrentValue / MaxValue : 0f;

        public SlotClockState(int maxValue)
        {
            MaxValue = maxValue;
        }

        public void Increment(int amount = 1)
        {
            CurrentValue += amount;
        }
    }
}
