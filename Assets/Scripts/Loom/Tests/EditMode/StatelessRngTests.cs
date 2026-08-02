using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class StatelessRngTests
    {
        [TestCase(0UL, 0UL, 0L, Core.RandomSlot.StepTrigger, 0x2848D6A4A7B28BC1UL)]
        [TestCase(1UL, 2UL, 3L, Core.RandomSlot.StepTrigger, 0xF98C64DB8A608330UL)]
        [TestCase(
            0x0123456789ABCDEFUL,
            0xFEDCBA9876543210UL,
            960L,
            Core.RandomSlot.StepTrigger,
            0x6F71C0267798638DUL)]
        [TestCase(
            ulong.MaxValue,
            ulong.MaxValue,
            long.MaxValue,
            Core.RandomSlot.StepRatchetCount,
            0x558B9823CEAB1F5BUL)]
        public void HashMatchesFrozenGoldenVectors(
            ulong seed,
            ulong streamId,
            long tick,
            Core.RandomSlot slot,
            ulong expectedHash)
        {
            Assert.That(
                Core.StatelessRng.Hash(seed, streamId, tick, slot),
                Is.EqualTo(expectedHash));
        }

        [Test]
        public void IdenticalInputsAlwaysReturnTheSameValue()
        {
            const ulong Seed = 42UL;
            const ulong StreamId = 7UL;
            const long Tick = 3_840L;

            ulong first = Core.StatelessRng.Hash(
                Seed,
                StreamId,
                Tick,
                Core.RandomSlot.StepTrigger);

            for (int iteration = 0; iteration < 100; iteration++)
            {
                Assert.That(
                    Core.StatelessRng.Hash(
                        Seed,
                        StreamId,
                        Tick,
                        Core.RandomSlot.StepTrigger),
                    Is.EqualTo(first));
            }
        }

        [Test]
        public void EveryInputLaneChangesTheDecisionDomain()
        {
            ulong baseline = Core.StatelessRng.Hash(
                42UL,
                7UL,
                3_840L,
                Core.RandomSlot.StepTrigger);

            Assert.That(
                Core.StatelessRng.Hash(
                    43UL,
                    7UL,
                    3_840L,
                    Core.RandomSlot.StepTrigger),
                Is.Not.EqualTo(baseline));
            Assert.That(
                Core.StatelessRng.Hash(
                    42UL,
                    8UL,
                    3_840L,
                    Core.RandomSlot.StepTrigger),
                Is.Not.EqualTo(baseline));
            Assert.That(
                Core.StatelessRng.Hash(
                    42UL,
                    7UL,
                    3_841L,
                    Core.RandomSlot.StepTrigger),
                Is.Not.EqualTo(baseline));
            Assert.That(
                Core.StatelessRng.Hash(
                    42UL,
                    7UL,
                    3_840L,
                    Core.RandomSlot.StepVelocity),
                Is.Not.EqualTo(baseline));
        }

        [Test]
        public void NamedSlotsHaveFrozenUniqueIdentifiers()
        {
            Assert.That((ulong)Core.RandomSlot.StepTrigger, Is.EqualTo(1UL));
            Assert.That((ulong)Core.RandomSlot.StepVelocity, Is.EqualTo(2UL));
            Assert.That((ulong)Core.RandomSlot.StepDuration, Is.EqualTo(3UL));
            Assert.That((ulong)Core.RandomSlot.StepRatchetCount, Is.EqualTo(4UL));
            Assert.That((ulong)Core.RandomSlot.StepMicrotiming, Is.EqualTo(5UL));
        }

        [Test]
        public void HashRejectsNegativeTicks()
        {
            ArgumentOutOfRangeException exception =
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => Core.StatelessRng.Hash(
                        0UL,
                        0UL,
                        -1L,
                        Core.RandomSlot.StepTrigger));

            Assert.That(exception.ParamName, Is.EqualTo("tick"));
        }

        [TestCase(0UL)]
        [TestCase(6UL)]
        [TestCase(ulong.MaxValue)]
        public void HashRejectsUnnamedSlots(ulong slotValue)
        {
            ArgumentOutOfRangeException exception =
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => Core.StatelessRng.Hash(
                        0UL,
                        0UL,
                        0L,
                        (Core.RandomSlot)slotValue));

            Assert.That(exception.ParamName, Is.EqualTo("slot"));
        }

        [Test]
        public void DecisionsDoNotDependOnEvaluationOrder()
        {
            var ticks = new[] { 0L, 1L, 960L, 3_840L, long.MaxValue };
            var forward = new ulong[ticks.Length];
            var reverse = new ulong[ticks.Length];

            for (int index = 0; index < ticks.Length; index++)
            {
                forward[index] = Core.StatelessRng.Hash(
                    99UL,
                    5UL,
                    ticks[index],
                    Core.RandomSlot.StepTrigger);
            }

            for (int index = ticks.Length - 1; index >= 0; index--)
            {
                reverse[index] = Core.StatelessRng.Hash(
                    99UL,
                    5UL,
                    ticks[index],
                    Core.RandomSlot.StepTrigger);
            }

            Assert.That(reverse, Is.EqualTo(forward));
        }

        [TestCase(0UL, 0f)]
        [TestCase(0x8000000000000000UL, 0.5f)]
        [TestCase(ulong.MaxValue, 0.9999999403953552f)]
        [TestCase(0x2848D6A4A7B28BC1UL, 0.15736138820648193f)]
        public void Float01MapsTheHighTwentyFourBitsIntoTheUnitInterval(
            ulong hash,
            float expectedValue)
        {
            float value = Core.StatelessRng.Float01(hash);

            Assert.That(value, Is.EqualTo(expectedValue));
            Assert.That(value, Is.GreaterThanOrEqualTo(0f));
            Assert.That(value, Is.LessThan(1f));
        }

        [Test]
        public void Float01IgnoresBitsBelowSinglePrecisionMantissa()
        {
            const ulong HighBits = 0xABCDEF0000000000UL;

            Assert.That(
                Core.StatelessRng.Float01(HighBits),
                Is.EqualTo(Core.StatelessRng.Float01(HighBits | 0xFFFFFFFFFFUL)));
        }
    }
}
