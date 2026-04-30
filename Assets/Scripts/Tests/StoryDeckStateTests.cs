using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class StoryDeckStateTests
    {
        private static StoryCardDefinition MakeCard(string title, int clockMax)
        {
            var card = ScriptableObject.CreateInstance<StoryCardDefinition>();
            card.title = title;
            card.clockMaxValue = clockMax;
            return card;
        }

        private static StoryDeckDefinition MakeDeck(StoryDrawMode mode, params StoryCardDefinition[] cards)
        {
            var deck = ScriptableObject.CreateInstance<StoryDeckDefinition>();
            deck.drawMode = mode;
            deck.cards = new System.Collections.Generic.List<StoryCardDefinition>(cards);
            return deck;
        }

        [Test]
        public void Constructor_sequential_sets_first_card_as_active()
        {
            var card1 = MakeCard("A", 3);
            var card2 = MakeCard("B", 5);
            var deckDef = MakeDeck(StoryDrawMode.Sequential, card1, card2);

            var state = new StoryDeckState(deckDef);

            Assert.AreEqual(card1, state.ActiveCard);
        }

        [Test]
        public void Constructor_initializes_clock_with_active_card_clockMaxValue()
        {
            var card = MakeCard("A", 7);
            var deckDef = MakeDeck(StoryDrawMode.Sequential, card);

            var state = new StoryDeckState(deckDef);

            Assert.AreEqual(7, state.ActiveCardClock.MaxValue);
            Assert.AreEqual(0, state.ActiveCardClock.CurrentValue);
        }

        [Test]
        public void AdvanceCard_sequential_moves_to_next_card()
        {
            var card1 = MakeCard("A", 3);
            var card2 = MakeCard("B", 5);
            var deckDef = MakeDeck(StoryDrawMode.Sequential, card1, card2);
            var state = new StoryDeckState(deckDef);

            state.AdvanceCard();

            Assert.AreEqual(card2, state.ActiveCard);
        }

        [Test]
        public void AdvanceCard_sequential_resets_clock_to_new_card_max()
        {
            var card1 = MakeCard("A", 3);
            var card2 = MakeCard("B", 5);
            var deckDef = MakeDeck(StoryDrawMode.Sequential, card1, card2);
            var state = new StoryDeckState(deckDef);

            state.AdvanceCard();

            Assert.AreEqual(5, state.ActiveCardClock.MaxValue);
            Assert.AreEqual(0, state.ActiveCardClock.CurrentValue);
        }

        [Test]
        public void AdvanceCard_sequential_sets_active_card_null_when_deck_exhausted()
        {
            var card = MakeCard("A", 3);
            var deckDef = MakeDeck(StoryDrawMode.Sequential, card);
            var state = new StoryDeckState(deckDef);

            state.AdvanceCard();

            Assert.IsNull(state.ActiveCard);
            Assert.IsNull(state.ActiveCardClock);
        }

        [Test]
        public void Constructor_random_sets_one_of_the_cards_as_active()
        {
            var card1 = MakeCard("A", 3);
            var card2 = MakeCard("B", 5);
            var card3 = MakeCard("C", 2);
            var deckDef = MakeDeck(StoryDrawMode.Random, card1, card2, card3);

            var state = new StoryDeckState(deckDef, new System.Random(42));

            Assert.IsTrue(
                state.ActiveCard == card1 ||
                state.ActiveCard == card2 ||
                state.ActiveCard == card3);
        }

        [Test]
        public void AdvanceCard_random_does_not_repeat_cards()
        {
            var card1 = MakeCard("A", 1);
            var card2 = MakeCard("B", 1);
            var card3 = MakeCard("C", 1);
            var deckDef = MakeDeck(StoryDrawMode.Random, card1, card2, card3);
            var state = new StoryDeckState(deckDef, new System.Random(42));

            var seen = new System.Collections.Generic.HashSet<StoryCardDefinition>();
            seen.Add(state.ActiveCard);
            state.AdvanceCard();
            seen.Add(state.ActiveCard);
            state.AdvanceCard();
            seen.Add(state.ActiveCard);

            Assert.AreEqual(3, seen.Count);
        }

        [Test]
        public void AdvanceCard_random_sets_active_card_null_when_all_cards_seen()
        {
            var card1 = MakeCard("A", 1);
            var card2 = MakeCard("B", 1);
            var deckDef = MakeDeck(StoryDrawMode.Random, card1, card2);
            var state = new StoryDeckState(deckDef, new System.Random(42));

            state.AdvanceCard();
            state.AdvanceCard();

            Assert.IsNull(state.ActiveCard);
        }
    }
}
