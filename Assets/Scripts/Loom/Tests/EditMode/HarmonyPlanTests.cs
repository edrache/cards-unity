using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class HarmonyPlanTests
    {
        [Test]
        public void ConstructorRejectsNullOrEmptySteps()
        {
            Assert.Throws<ArgumentNullException>(
                () => new Core.HarmonyPlan((Core.HarmonyStep[])null));
            Assert.Throws<ArgumentException>(() => new Core.HarmonyPlan());
        }

        [Test]
        public void ConstructorRejectsInvalidDefaultSteps()
        {
            var valid = new Core.HarmonyStep(0, 960L);

            Assert.Throws<ArgumentException>(
                () => new Core.HarmonyPlan(default(Core.HarmonyStep)));
            Assert.Throws<ArgumentException>(
                () => new Core.HarmonyPlan(valid, default));
        }

        [Test]
        public void ConstructorRejectsTotalDurationOverflow()
        {
            var maximum = new Core.HarmonyStep(0, long.MaxValue);
            var overflow = new Core.HarmonyStep(4, 1L);

            Assert.Throws<ArgumentException>(
                () => new Core.HarmonyPlan(maximum, overflow));
        }

        [Test]
        public void PlanOwnsStepsAndExposesTotalDuration()
        {
            var first = new Core.HarmonyStep(0, 960L);
            var second = new Core.HarmonyStep(4, 480L);
            var source = new[] { first, second };
            var plan = new Core.HarmonyPlan(source);

            source[0] = new Core.HarmonyStep(6, 1L);

            Assert.That(plan.Count, Is.EqualTo(2));
            Assert.That(plan.TotalDurationTicks, Is.EqualTo(1_440L));
            Assert.That(plan[0], Is.EqualTo(first));
            Assert.That(plan[1], Is.EqualTo(second));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void IndexerRejectsIndicesOutsidePlan(int index)
        {
            var plan = new Core.HarmonyPlan(new Core.HarmonyStep(0, 960L));

            Assert.Throws<ArgumentOutOfRangeException>(() => _ = plan[index]);
        }

        [TestCase(0L, 0)]
        [TestCase(959L, 0)]
        [TestCase(960L, 4)]
        [TestCase(1_439L, 4)]
        [TestCase(1_440L, 0)]
        [TestCase(2_400L, 4)]
        public void LookupUsesHalfOpenCyclicBoundaries(long tick, int expectedRoot)
        {
            var plan = new Core.HarmonyPlan(
                new Core.HarmonyStep(0, 960L),
                new Core.HarmonyStep(4, 480L));

            Assert.That(plan.GetStepAtTick(tick).RootScaleDegree, Is.EqualTo(expectedRoot));
        }

        [Test]
        public void LookupRejectsNegativeTicks()
        {
            var plan = new Core.HarmonyPlan(new Core.HarmonyStep(0, 960L));

            Assert.Throws<ArgumentOutOfRangeException>(() => plan.GetStepAtTick(-1L));
        }

        [Test]
        public void LookupHandlesInt64MaximumDeterministically()
        {
            var plan = new Core.HarmonyPlan(
                new Core.HarmonyStep(0, 1L),
                new Core.HarmonyStep(4, 1L));

            Assert.That(plan.GetStepAtTick(long.MaxValue).RootScaleDegree, Is.EqualTo(4));
            Assert.That(plan.GetStepAtTick(long.MaxValue).RootScaleDegree, Is.EqualTo(4));
        }
    }
}
