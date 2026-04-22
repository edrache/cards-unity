using System;
using System.Collections.Generic;

namespace CardsUnity
{
    public class DeckState
    {
        private readonly List<CardDefinition> _drawPile = new();
        private readonly List<CardDefinition> _discardPile = new();

        public int DrawCount => _drawPile.Count;
        public int DiscardCount => _discardPile.Count;
        public bool CanDraw => _drawPile.Count > 0 || _discardPile.Count > 0;

        public void Initialize(IEnumerable<CardDefinition> cards)
        {
            _drawPile.Clear();
            _discardPile.Clear();
            _drawPile.AddRange(cards);
        }

        public CardDefinition Draw()
        {
            if (_drawPile.Count == 0)
                return null;

            var card = _drawPile[_drawPile.Count - 1];
            _drawPile.RemoveAt(_drawPile.Count - 1);
            return card;
        }

        public void Discard(CardDefinition card) => _discardPile.Add(card);

        public void ShuffleDiscardIntoDrawPile(Random rng)
        {
            _drawPile.AddRange(_discardPile);
            _discardPile.Clear();

            for (int i = _drawPile.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_drawPile[i], _drawPile[j]) = (_drawPile[j], _drawPile[i]);
            }
        }
    }
}
