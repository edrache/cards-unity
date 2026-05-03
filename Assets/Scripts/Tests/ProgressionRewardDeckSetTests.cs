using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class ProgressionRewardDeckSetTests
    {
        private static CardDefinition MakeCard(CardType type)
        {
            var definition = ScriptableObject.CreateInstance<CardDefinition>();
            definition.type = type;
            definition.value = 1;
            return definition;
        }

        [Test]
        public void GetRewardCard_returns_card_from_force_list_when_key_is_force()
        {
            var deckSet = ScriptableObject.CreateInstance<ProgressionRewardDeckSet>();
            var card = MakeCard(CardType.Force);
            deckSet.forceCards = new() { card };

            var result = deckSet.GetRewardCard(StoryEffectResolutionKey.Force, new System.Random(42));

            Assert.AreSame(card, result);
        }

        [Test]
        public void GetRewardCard_returns_card_from_wit_list_when_key_is_wit()
        {
            var deckSet = ScriptableObject.CreateInstance<ProgressionRewardDeckSet>();
            var card = MakeCard(CardType.Wit);
            deckSet.witCards = new() { card };

            var result = deckSet.GetRewardCard(StoryEffectResolutionKey.Wit, new System.Random(42));

            Assert.AreSame(card, result);
        }

        [Test]
        public void GetRewardCard_returns_card_from_presence_list_when_key_is_presence()
        {
            var deckSet = ScriptableObject.CreateInstance<ProgressionRewardDeckSet>();
            var card = MakeCard(CardType.Presence);
            deckSet.presenceCards = new() { card };

            var result = deckSet.GetRewardCard(StoryEffectResolutionKey.Presence, new System.Random(42));

            Assert.AreSame(card, result);
        }

        [Test]
        public void GetRewardCard_returns_card_from_full_tie_list_when_key_is_full_tie()
        {
            var deckSet = ScriptableObject.CreateInstance<ProgressionRewardDeckSet>();
            var card = MakeCard(CardType.Force);
            deckSet.fullTieCards = new() { card };

            var result = deckSet.GetRewardCard(StoryEffectResolutionKey.FullTie, new System.Random(42));

            Assert.AreSame(card, result);
        }

        [Test]
        public void GetRewardCard_returns_null_when_list_is_empty()
        {
            var deckSet = ScriptableObject.CreateInstance<ProgressionRewardDeckSet>();
            deckSet.forceCards = new();

            var result = deckSet.GetRewardCard(StoryEffectResolutionKey.Force, new System.Random(42));

            Assert.IsNull(result);
        }

        [Test]
        public void GetRewardCard_returns_null_when_list_is_null()
        {
            var deckSet = ScriptableObject.CreateInstance<ProgressionRewardDeckSet>();
            deckSet.forceCards = null;

            var result = deckSet.GetRewardCard(StoryEffectResolutionKey.Force, new System.Random(42));

            Assert.IsNull(result);
        }
    }
}
