using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class HarmonyStepTests
    {
        [TestCase(-3, 1L)]
        [TestCase(4, 960L)]
        [TestCase(int.MinValue, long.MaxValue)]
        [TestCase(int.MaxValue, 1L)]
        public void ConstructorPreservesSignedRootAndPositiveDuration(
            int rootScaleDegree,
            long durationTicks)
        {
            var step = new Core.HarmonyStep(rootScaleDegree, durationTicks);

            Assert.That(step.RootScaleDegree, Is.EqualTo(rootScaleDegree));
            Assert.That(step.DurationTicks, Is.EqualTo(durationTicks));
        }

        [TestCase(0L)]
        [TestCase(-1L)]
        [TestCase(long.MinValue)]
        public void ConstructorRejectsNonPositiveDuration(long durationTicks)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.HarmonyStep(0, durationTicks));
        }

        [Test]
        public void HarmonyStepUsesValueSemantics()
        {
            var first = new Core.HarmonyStep(4, 960L);
            var equal = new Core.HarmonyStep(4, 960L);

            Assert.That(first, Is.EqualTo(equal));
            Assert.That(first.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(first == equal, Is.True);
            Assert.That(first != new Core.HarmonyStep(3, 960L), Is.True);
            Assert.That(first != new Core.HarmonyStep(4, 480L), Is.True);
        }
    }
}
