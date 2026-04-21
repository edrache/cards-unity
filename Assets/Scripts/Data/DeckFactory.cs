using System;
using System.Collections.Generic;

namespace CardsUnity.Data
{
    public static class DeckFactory
    {
        public static List<CardInstance> CreatePlayerDeck(IReadOnlyList<CardData> definitions, Random random = null)
        {
            return CreateDeck(definitions, CardOwner.Player, random);
        }

        public static List<CardInstance> CreateEnemyDeck(IReadOnlyList<CardData> definitions, Random random = null)
        {
            return CreateDeck(definitions, CardOwner.Enemy, random);
        }

        public static List<CardInstance> CreateDeck(IReadOnlyList<CardData> definitions, Random random = null)
        {
            return CreateDeck(definitions, null, random);
        }

        public static CardInstance Clone(CardData definition)
        {
            return new CardInstance(definition);
        }

        public static void Shuffle<T>(IList<T> deck, Random random = null)
        {
            if (deck == null)
            {
                throw new ArgumentNullException(nameof(deck));
            }

            Random rng = random ?? new Random();
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int swapIndex = rng.Next(i + 1);
                (deck[i], deck[swapIndex]) = (deck[swapIndex], deck[i]);
            }
        }

        private static List<CardInstance> CreateDeck(
            IReadOnlyList<CardData> definitions,
            CardOwner? expectedOwner,
            Random random)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            List<CardInstance> deck = new List<CardInstance>(definitions.Count);
            for (int i = 0; i < definitions.Count; i++)
            {
                CardData definition = definitions[i];
                if (definition == null)
                {
                    throw new ArgumentException("Deck definitions cannot contain null entries.", nameof(definitions));
                }

                if (expectedOwner.HasValue && definition.Owner != expectedOwner.Value)
                {
                    throw new ArgumentException(
                        $"Expected {expectedOwner.Value} card definition but found {definition.Owner} at index {i}.",
                        nameof(definitions));
                }

                deck.Add(Clone(definition));
            }

            Shuffle(deck, random);
            return deck;
        }
    }
}
