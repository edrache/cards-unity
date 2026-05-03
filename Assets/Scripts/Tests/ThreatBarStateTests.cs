using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class ThreatBarStateTests
    {
        [Test]
        public void New_bar_starts_fully_filled()
        {
            var bar = MakeBar(5, 3);

            Assert.AreEqual(5, bar.Segments[0].CurrentValue);
            Assert.AreEqual(3, bar.Segments[1].CurrentValue);
        }

        [Test]
        public void TakeDamage_drains_outermost_segment_first()
        {
            var bar = MakeBar(10, 5);

            bar.TakeDamage(4);

            Assert.AreEqual(6, bar.Segments[0].CurrentValue);
            Assert.AreEqual(5, bar.Segments[1].CurrentValue);
        }

        [Test]
        public void TakeDamage_overflow_cascades_into_next_segment()
        {
            var bar = MakeBar(3, 10);

            bar.TakeDamage(5);

            Assert.AreEqual(0, bar.Segments[0].CurrentValue);
            Assert.AreEqual(8, bar.Segments[1].CurrentValue);
        }

        [Test]
        public void TakeDamage_fires_OnTierDepleted_when_segment_reaches_zero()
        {
            var bar = MakeBar(3, 10);
            int depletedIndex = -1;

            bar.OnTierDepleted += index => depletedIndex = index;
            bar.TakeDamage(3);

            Assert.AreEqual(0, depletedIndex);
        }

        [Test]
        public void TakeDamage_does_not_fire_OnTierDepleted_for_partial_drain()
        {
            var bar = MakeBar(5, 10);
            int callCount = 0;

            bar.OnTierDepleted += _ => callCount++;
            bar.TakeDamage(2);

            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void TakeDamage_fires_OnReachedZero_when_all_segments_empty()
        {
            var bar = MakeBar(2, 3);
            bool fired = false;

            bar.OnReachedZero += () => fired = true;
            bar.TakeDamage(5);

            Assert.IsTrue(fired);
        }

        [Test]
        public void TakeDamage_does_not_fire_OnReachedZero_when_segments_remain()
        {
            var bar = MakeBar(2, 3);
            bool fired = false;

            bar.OnReachedZero += () => fired = true;
            bar.TakeDamage(2);

            Assert.IsFalse(fired);
        }

        [Test]
        public void IsAtZero_true_when_all_segments_depleted()
        {
            var bar = MakeBar(2, 3);

            bar.TakeDamage(5);

            Assert.IsTrue(bar.IsAtZero);
        }

        [Test]
        public void IsAtZero_false_when_segments_remain()
        {
            var bar = MakeBar(2, 3);

            bar.TakeDamage(1);

            Assert.IsFalse(bar.IsAtZero);
        }

        [Test]
        public void TakeDamage_excess_beyond_all_segments_is_clamped_at_zero()
        {
            var bar = MakeBar(3);

            bar.TakeDamage(999);

            Assert.AreEqual(0, bar.Segments[0].CurrentValue);
        }

        [Test]
        public void TakeDamage_fires_OnTierDepleted_once_per_crossed_segment()
        {
            var bar = MakeBar(2, 3, 5);
            int callCount = 0;

            bar.OnTierDepleted += _ => callCount++;
            bar.TakeDamage(5);

            Assert.AreEqual(2, callCount);
        }

        [Test]
        public void TakeDamage_fires_OnReachedZero_only_once()
        {
            var bar = MakeBar(3);
            int callCount = 0;

            bar.OnReachedZero += () => callCount++;
            bar.TakeDamage(3);
            bar.TakeDamage(1);

            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void TakeDamage_ignores_non_positive_damage()
        {
            var bar = MakeBar(4, 2);

            bar.TakeDamage(0);
            bar.TakeDamage(-2);

            Assert.AreEqual(4, bar.Segments[0].CurrentValue);
            Assert.AreEqual(2, bar.Segments[1].CurrentValue);
        }

        [Test]
        public void Constructor_throws_when_tier_definition_is_null()
        {
            var definitions = new List<ThreatTierDefinition> { null };

            Assert.Throws<System.ArgumentException>(() => new ThreatBarState(definitions));
        }

        private static ThreatBarState MakeBar(params int[] tierMaxValues)
        {
            var definitions = new List<ThreatTierDefinition>(tierMaxValues.Length);

            foreach (int maxValue in tierMaxValues)
                definitions.Add(MakeTier(maxValue));

            return new ThreatBarState(definitions);
        }

        private static ThreatTierDefinition MakeTier(int maxValue)
        {
            var tier = ScriptableObject.CreateInstance<ThreatTierDefinition>();
            tier.maxValue = maxValue;
            return tier;
        }
    }
}
