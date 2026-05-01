using System;

namespace CardsUnity
{
    public class BoardState
    {
        public SlotState[] Slots { get; private set; }

        public BoardState(int slotCount)
        {
            Slots = new SlotState[slotCount];
            for (int i = 0; i < slotCount; i++)
                Slots[i] = new SlotState();
        }

        public BoardState(SlotDefinition[] definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            Slots = new SlotState[definitions.Length];
            // null entries are intentional: they produce a plain slot with no definition (legacy behaviour)
            for (int i = 0; i < definitions.Length; i++)
                Slots[i] = new SlotState(definitions[i]);
        }

        public BoardState(SlotDefinition[] definitions, bool[] activeStates)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            Slots = new SlotState[definitions.Length];

            for (int i = 0; i < definitions.Length; i++)
            {
                bool isActive = activeStates == null || i >= activeStates.Length || activeStates[i];
                Slots[i] = new SlotState(definitions[i], isActive);
            }
        }

        public int AddSlot(SlotDefinition definition, bool isActive = true)
        {
            int newIndex = Slots.Length;
            var resizedSlots = Slots;
            Array.Resize(ref resizedSlots, newIndex + 1);
            resizedSlots[newIndex] = new SlotState(definition, isActive);
            Slots = resizedSlots;
            return newIndex;
        }
    }
}
