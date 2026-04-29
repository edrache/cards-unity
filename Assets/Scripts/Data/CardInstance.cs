using System;

namespace CardsUnity
{
    public class CardInstance
    {
        public CardDefinition Definition { get; }

        public int CurrentValue { get; set; }
        public bool IsExhausted { get; set; }

        public CardInstance(CardDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            CurrentValue = definition.value;
            IsExhausted = false;
        }
    }
}
