namespace CardsUnity
{
    public class BoardState
    {
        public SlotState[] Slots { get; }

        public BoardState(int slotCount)
        {
            Slots = new SlotState[slotCount];
            for (int i = 0; i < slotCount; i++)
                Slots[i] = new SlotState();
        }

        public BoardState(SlotDefinition[] definitions)
        {
            Slots = new SlotState[definitions.Length];
            for (int i = 0; i < definitions.Length; i++)
                Slots[i] = new SlotState(definitions[i]);
        }
    }
}
