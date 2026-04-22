using System.Collections.Generic;

namespace CardsUnity
{
    public class HandState
    {
        private readonly List<CardDefinition> _cards = new();

        public IReadOnlyList<CardDefinition> Cards => _cards;

        public void Add(CardDefinition card) => _cards.Add(card);
        public bool Remove(CardDefinition card) => _cards.Remove(card);
        public void Clear() => _cards.Clear();
    }
}
