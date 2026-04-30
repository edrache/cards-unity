using System.Collections.Generic;

namespace CardsUnity
{
    public class HandState
    {
        private readonly List<CardInstance> _cards = new();

        public IReadOnlyList<CardInstance> Cards => _cards;

        public void Add(CardInstance card) => _cards.Add(card);
        public bool Remove(CardInstance card) => _cards.Remove(card);
        public void Clear() => _cards.Clear();
    }
}
