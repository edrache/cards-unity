using CardsUnity.Config;
using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests
{
    public sealed class ConfigDefaultsTests
    {
        [Test]
        public void GameConfig_UsesMigrationSlotCountByDefault()
        {
            GameConfig config = ScriptableObject.CreateInstance<GameConfig>();

            Assert.AreEqual(3, config.SlotCount);
        }

        [Test]
        public void AnimConfig_UsesMigrationTimingsByDefault()
        {
            AnimConfig config = ScriptableObject.CreateInstance<AnimConfig>();

            Assert.AreEqual(1.15f, config.LiftScale);
            Assert.AreEqual(40f, config.TiltMax);
            Assert.AreEqual(0.05f, config.SnapDuration);
            Assert.AreEqual(0.55f, config.LiftDuration);
            Assert.AreEqual(100f, config.SlotSnapRadius);
            Assert.AreEqual(0.45f, config.ResolveFlashDuration);
            Assert.AreEqual(0.22f, config.ResolvePauseDuration);
            Assert.AreEqual(30f, config.ResolveTiltAngle);
            Assert.AreEqual(0.55f, config.CleanupDuration);
        }

        [Test]
        public void CardVisualConfig_UsesMigrationDimensionsByDefault()
        {
            CardVisualConfig config = ScriptableObject.CreateInstance<CardVisualConfig>();

            Assert.AreEqual(150f, config.Width);
            Assert.AreEqual(210f, config.Height);
            Assert.AreEqual(10f, config.CornerRadius);
        }
    }
}
