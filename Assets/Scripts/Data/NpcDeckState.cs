using System;
using System.Collections.Generic;

namespace CardsUnity
{
    public class NpcDeckState
    {
        private readonly List<NpcCardDefinition> _drawPile = new();
        private readonly List<NpcCardDefinition> _discardPile = new();

        public NpcCardDefinition ActiveCard { get; private set; }
        public int DrawCount => _drawPile.Count;
        public int DiscardCount => _discardPile.Count;

        public NpcDeckState(NpcDeckDefinition definition, Random rng)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            var cards = new List<NpcCardDefinition>(definition.cards ?? new List<NpcCardDefinition>());
            if (definition.shuffleOnInit && rng != null)
                Shuffle(cards, rng);

            _drawPile.AddRange(cards);
        }

        public NpcCardDefinition Draw(Random rng)
        {
            if (ActiveCard != null)
            {
                _discardPile.Add(ActiveCard);
                ActiveCard = null;
            }

            if (_drawPile.Count == 0)
            {
                if (_discardPile.Count == 0)
                    return null;

                if (rng == null)
                    throw new ArgumentNullException(nameof(rng));

                _drawPile.AddRange(_discardPile);
                _discardPile.Clear();
                Shuffle(_drawPile, rng);
            }

            ActiveCard = _drawPile[_drawPile.Count - 1];
            _drawPile.RemoveAt(_drawPile.Count - 1);
            return ActiveCard;
        }

        private static void Shuffle(List<NpcCardDefinition> cards, Random rng)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }
    }
}
