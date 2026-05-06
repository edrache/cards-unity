using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class NpcDeckStateTests
    {
        private static NpcCardDefinition MakeNpcCard(string name)
        {
            var card = ScriptableObject.CreateInstance<NpcCardDefinition>();
            card.characterName = name;
            return card;
        }

        private static NpcDeckDefinition MakeDefinition(bool shuffle, params NpcCardDefinition[] cards)
        {
            var definition = ScriptableObject.CreateInstance<NpcDeckDefinition>();
            definition.shuffleOnInit = shuffle;
            definition.cards = new List<NpcCardDefinition>(cards);
            return definition;
        }

        [Test]
        public void Draw_returns_card_from_draw_pile()
        {
            var card = MakeNpcCard("Alice");
            var state = new NpcDeckState(MakeDefinition(false, card), new System.Random(42));

            var drawn = state.Draw(new System.Random(42));

            Assert.AreSame(card, drawn);
        }

        [Test]
        public void Draw_sets_ActiveCard()
        {
            var card = MakeNpcCard("Bob");
            var state = new NpcDeckState(MakeDefinition(false, card), new System.Random(42));

            state.Draw(new System.Random(42));

            Assert.AreSame(card, state.ActiveCard);
        }

        [Test]
        public void Draw_discards_previous_ActiveCard_before_drawing_next()
        {
            var first = MakeNpcCard("First");
            var second = MakeNpcCard("Second");
            var rng = new System.Random(42);
            var state = new NpcDeckState(MakeDefinition(false, first, second), rng);

            state.Draw(rng);
            state.Draw(rng);

            Assert.AreEqual(1, state.DiscardCount);
        }

        [Test]
        public void DrawCount_decrements_after_draw()
        {
            var card = MakeNpcCard("Alice");
            var state = new NpcDeckState(MakeDefinition(false, card), new System.Random(42));

            state.Draw(new System.Random(42));

            Assert.AreEqual(0, state.DrawCount);
        }

        [Test]
        public void Draw_shuffles_discard_into_draw_pile_when_draw_pile_is_empty()
        {
            var card = MakeNpcCard("Alice");
            var rng = new System.Random(42);
            var state = new NpcDeckState(MakeDefinition(false, card), rng);

            state.Draw(rng);
            var second = state.Draw(rng);

            Assert.AreSame(card, second);
        }

        [Test]
        public void Draw_returns_null_when_all_piles_empty_and_no_active_card()
        {
            var state = new NpcDeckState(MakeDefinition(false), new System.Random(42));

            var result = state.Draw(new System.Random(42));

            Assert.IsNull(result);
        }

        [Test]
        public void ActiveCard_is_null_before_first_draw()
        {
            var card = MakeNpcCard("Alice");
            var state = new NpcDeckState(MakeDefinition(false, card), new System.Random(42));

            Assert.IsNull(state.ActiveCard);
        }
    }
}
