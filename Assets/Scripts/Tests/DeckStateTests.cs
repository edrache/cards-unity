using System.Collections.Generic;
using System.Linq;
using CardsUnity;
using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class DeckStateTests
    {
        [Test]
        public void Draw_returns_card()
        {
            Assert.IsNotNull(MakeDeck(3).Draw());
        }

        [Test]
        public void Draw_reduces_draw_pile_count()
        {
            var deck = MakeDeck(3);
            deck.Draw();
            Assert.AreEqual(2, deck.DrawCount);
        }

        [Test]
        public void Draw_returns_null_when_both_piles_empty()
        {
            var deck = new DeckState();
            deck.Initialize(new List<CardDefinition>());
            Assert.IsNull(deck.Draw());
        }

        [Test]
        public void Discard_adds_to_discard_pile()
        {
            var deck = MakeDeck(2);
            deck.Discard(deck.Draw());
            Assert.AreEqual(1, deck.DiscardCount);
        }

        [Test]
        public void ShuffleDiscard_moves_all_cards_to_draw_pile()
        {
            var deck = MakeDeck(3);
            deck.Discard(deck.Draw());
            deck.Discard(deck.Draw());
            deck.Discard(deck.Draw());
            deck.ShuffleDiscardIntoDrawPile(new System.Random(0));
            Assert.AreEqual(3, deck.DrawCount);
            Assert.AreEqual(0, deck.DiscardCount);
        }

        [Test]
        public void CanDraw_false_when_both_piles_empty()
        {
            var deck = new DeckState();
            deck.Initialize(new List<CardDefinition>());
            Assert.IsFalse(deck.CanDraw);
        }

        [Test]
        public void CanDraw_true_when_only_discard_has_cards()
        {
            var deck = MakeDeck(1);
            deck.Discard(deck.Draw());
            Assert.AreEqual(0, deck.DrawCount);
            Assert.IsTrue(deck.CanDraw);
        }

        private static DeckState MakeDeck(int count)
        {
            var cards = Enumerable.Range(0, count)
                .Select(_ => ScriptableObject.CreateInstance<CardDefinition>())
                .ToList();
            var deck = new DeckState();
            deck.Initialize(cards);
            return deck;
        }
    }
}
