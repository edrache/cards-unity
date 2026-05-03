using System.Collections.Generic;
using NUnit.Framework;

namespace CardsUnity.Tests
{
    public class ProgressionStateTests
    {
        [Test]
        public void Constructor_throws_when_xp_cap_is_not_positive()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new ProgressionState(0));
        }

        [Test]
        public void AddDefeatedCard_increases_CurrentXp_by_card_value()
        {
            var progression = new ProgressionState(xpCap: 10);

            progression.AddDefeatedCard(CardType.Force, 4);

            Assert.AreEqual(4, progression.CurrentXp);
        }

        [Test]
        public void AddDefeatedCard_tracks_per_type_totals()
        {
            var progression = new ProgressionState(xpCap: 100);

            progression.AddDefeatedCard(CardType.Force, 3);
            progression.AddDefeatedCard(CardType.Wit, 5);

            Assert.AreEqual(3, progression.DefeatedCounters.Force);
            Assert.AreEqual(5, progression.DefeatedCounters.Wit);
            Assert.AreEqual(0, progression.DefeatedCounters.Presence);
        }

        [Test]
        public void AddDefeatedCard_fires_OnLevelUp_when_xp_reaches_cap()
        {
            var progression = new ProgressionState(xpCap: 5);
            StoryEffectResolutionKey? result = null;

            progression.OnLevelUp += key => result = key;
            progression.AddDefeatedCard(CardType.Force, 3);
            progression.AddDefeatedCard(CardType.Force, 2);

            Assert.IsNotNull(result);
        }

        [Test]
        public void AddDefeatedCard_resolves_dominant_type_at_level_up()
        {
            var progression = new ProgressionState(xpCap: 5);
            StoryEffectResolutionKey? result = null;

            progression.OnLevelUp += key => result = key;
            progression.AddDefeatedCard(CardType.Force, 3);
            progression.AddDefeatedCard(CardType.Wit, 1);
            progression.AddDefeatedCard(CardType.Force, 1);

            Assert.AreEqual(StoryEffectResolutionKey.Force, result);
        }

        [Test]
        public void AddDefeatedCard_resets_xp_and_counters_after_level_up()
        {
            var progression = new ProgressionState(xpCap: 5);

            progression.AddDefeatedCard(CardType.Force, 5);

            Assert.AreEqual(0, progression.CurrentXp);
            Assert.AreEqual(0, progression.DefeatedCounters.Force);
            Assert.AreEqual(0, progression.DefeatedCounters.Wit);
            Assert.AreEqual(0, progression.DefeatedCounters.Presence);
        }

        [Test]
        public void AddDefeatedCard_carries_overflow_into_next_tier()
        {
            var progression = new ProgressionState(xpCap: 5);

            progression.AddDefeatedCard(CardType.Force, 7);

            Assert.AreEqual(2, progression.CurrentXp);
        }

        [Test]
        public void AddDefeatedCard_carries_overflow_type_value_into_next_tier_counters()
        {
            var progression = new ProgressionState(xpCap: 5);

            progression.AddDefeatedCard(CardType.Force, 7);

            Assert.AreEqual(2, progression.DefeatedCounters.Force);
            Assert.AreEqual(0, progression.DefeatedCounters.Wit);
            Assert.AreEqual(0, progression.DefeatedCounters.Presence);
        }

        [Test]
        public void AddDefeatedCard_increments_tier_after_level_up()
        {
            var progression = new ProgressionState(xpCap: 5);

            progression.AddDefeatedCard(CardType.Force, 5);

            Assert.AreEqual(1, progression.CurrentTier);
        }

        [Test]
        public void AddDefeatedCard_can_level_up_multiple_times_from_one_large_reward()
        {
            var progression = new ProgressionState(xpCap: 5);
            var resolutions = new List<StoryEffectResolutionKey>();

            progression.OnLevelUp += key => resolutions.Add(key);
            progression.AddDefeatedCard(CardType.Wit, 12);

            Assert.AreEqual(2, progression.CurrentTier);
            Assert.AreEqual(2, progression.CurrentXp);
            CollectionAssert.AreEqual(
                new[] { StoryEffectResolutionKey.Wit, StoryEffectResolutionKey.Wit },
                resolutions);
            Assert.AreEqual(2, progression.DefeatedCounters.Wit);
        }

        [Test]
        public void AddDefeatedCard_ignores_non_positive_values()
        {
            var progression = new ProgressionState(xpCap: 5);

            progression.AddDefeatedCard(CardType.Presence, 0);
            progression.AddDefeatedCard(CardType.Presence, -3);

            Assert.AreEqual(0, progression.CurrentXp);
            Assert.AreEqual(0, progression.DefeatedCounters.Presence);
            Assert.AreEqual(0, progression.CurrentTier);
        }

        [Test]
        public void Progress_is_fraction_of_CurrentXp_over_cap()
        {
            var progression = new ProgressionState(xpCap: 10);

            progression.AddDefeatedCard(CardType.Force, 4);

            Assert.AreEqual(0.4f, progression.Progress, 0.001f);
        }
    }
}
