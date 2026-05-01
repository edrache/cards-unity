using NUnit.Framework;

namespace CardsUnity.Tests
{
    public class PlayedCardCounterDominanceResolverTests
    {
        [Test]
        public void Resolve_returns_force_when_force_has_highest_value()
        {
            var state = new PlayedCardCounterState();
            state.Add(CardType.Force, 5);
            state.Add(CardType.Presence, 2);
            state.Add(CardType.Wit, 3);

            Assert.AreEqual(StoryEffectResolutionKey.Force, PlayedCardCounterDominanceResolver.Resolve(state));
        }

        [Test]
        public void Resolve_returns_presence_when_presence_has_highest_value()
        {
            var state = new PlayedCardCounterState();
            state.Add(CardType.Force, 1);
            state.Add(CardType.Presence, 6);
            state.Add(CardType.Wit, 4);

            Assert.AreEqual(StoryEffectResolutionKey.Presence, PlayedCardCounterDominanceResolver.Resolve(state));
        }

        [Test]
        public void Resolve_returns_wit_when_wit_has_highest_value()
        {
            var state = new PlayedCardCounterState();
            state.Add(CardType.Force, 2);
            state.Add(CardType.Presence, 4);
            state.Add(CardType.Wit, 7);

            Assert.AreEqual(StoryEffectResolutionKey.Wit, PlayedCardCounterDominanceResolver.Resolve(state));
        }

        [Test]
        public void Resolve_breaks_force_presence_tie_using_rps()
        {
            var state = new PlayedCardCounterState();
            state.Add(CardType.Force, 5);
            state.Add(CardType.Presence, 5);
            state.Add(CardType.Wit, 2);

            Assert.AreEqual(StoryEffectResolutionKey.Force, PlayedCardCounterDominanceResolver.Resolve(state));
        }

        [Test]
        public void Resolve_breaks_presence_wit_tie_using_rps()
        {
            var state = new PlayedCardCounterState();
            state.Add(CardType.Force, 1);
            state.Add(CardType.Presence, 4);
            state.Add(CardType.Wit, 4);

            Assert.AreEqual(StoryEffectResolutionKey.Presence, PlayedCardCounterDominanceResolver.Resolve(state));
        }

        [Test]
        public void Resolve_breaks_wit_force_tie_using_rps()
        {
            var state = new PlayedCardCounterState();
            state.Add(CardType.Force, 8);
            state.Add(CardType.Presence, 2);
            state.Add(CardType.Wit, 8);

            Assert.AreEqual(StoryEffectResolutionKey.Wit, PlayedCardCounterDominanceResolver.Resolve(state));
        }

        [Test]
        public void Resolve_returns_full_tie_when_all_three_values_match()
        {
            var state = new PlayedCardCounterState();
            state.Add(CardType.Force, 3);
            state.Add(CardType.Presence, 3);
            state.Add(CardType.Wit, 3);

            Assert.AreEqual(StoryEffectResolutionKey.FullTie, PlayedCardCounterDominanceResolver.Resolve(state));
        }
    }
}
