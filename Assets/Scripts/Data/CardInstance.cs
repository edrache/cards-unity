using System;

namespace CardsUnity.Data
{
    public sealed class CardInstance
    {
        public CardInstance(CardData definition)
        {
            Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            Id = definition.Id;
            CardName = definition.CardName;
            Rps = definition.Rps;
            BaseValue = definition.Value;
            Value = definition.Value;
            Role = definition.Role;
            Owner = definition.Owner;
            Emoji = definition.Emoji;
            Flavor = definition.Flavor;
        }

        public CardData Definition { get; }
        public string Id { get; }
        public string CardName { get; }
        public RpsType Rps { get; }
        public int BaseValue { get; }
        public int Value { get; set; }
        public CardRole Role { get; }
        public CardOwner Owner { get; }
        public string Emoji { get; }
        public string Flavor { get; }

        public bool IsDestroyed => Value <= 0;
    }
}
