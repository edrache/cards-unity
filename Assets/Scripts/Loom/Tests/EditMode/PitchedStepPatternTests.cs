using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class PitchedStepPatternTests
    {
        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(7)]
        public void ConstructorRejectsInvalidBeatSubdivision(int stepsPerBeat)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.PitchedStepPattern(
                    stepsPerBeat,
                    Core.PitchedPatternStep.Rest));
        }

        [Test]
        public void ConstructorRejectsMissingOrEmptySteps()
        {
            Assert.Throws<ArgumentNullException>(
                () => new Core.PitchedStepPattern(4, null));
            Assert.Throws<ArgumentException>(
                () => new Core.PitchedStepPattern(4));
        }

        [Test]
        public void PatternOwnsStepsAndDerivesGridLengths()
        {
            var original = new[]
            {
                new Core.PitchedPatternStep(
                    new Core.ScaleDegreePitch(1),
                    100,
                    120L),
                Core.PitchedPatternStep.Rest
            };
            var pattern = new Core.PitchedStepPattern(4, original);

            original[0] = Core.PitchedPatternStep.Rest;

            Assert.That(pattern.StepsPerBeat, Is.EqualTo(4));
            Assert.That(pattern.TicksPerStep, Is.EqualTo(240));
            Assert.That(pattern.LengthTicks, Is.EqualTo(480L));
            Assert.That(pattern.Count, Is.EqualTo(2));
            Assert.That(pattern[0].IsRest, Is.False);
        }

        [Test]
        public void IndexerRejectsIndicesOutsidePattern()
        {
            var pattern = new Core.PitchedStepPattern(
                4,
                Core.PitchedPatternStep.Rest);

            Assert.Throws<ArgumentOutOfRangeException>(() => _ = pattern[-1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = pattern[1]);
        }

        [TestCase(-240)]
        [TestCase(240)]
        public void ConstructorRejectsMicrotimingOutsideStepInterval(int offset)
        {
            var step = new Core.PitchedPatternStep(
                default,
                100,
                120L,
                microtimingTickOffset: offset);

            Assert.Throws<ArgumentException>(
                () => new Core.PitchedStepPattern(4, step));
        }

        [Test]
        public void ConstructorRejectsMoreRatchetsThanDistinctStepTicks()
        {
            var step = new Core.PitchedPatternStep(
                default,
                100,
                120L,
                ratchetCount: 121);

            Assert.Throws<ArgumentException>(
                () => new Core.PitchedStepPattern(8, step));
        }

        [Test]
        public void PatternReportsMinimumPlayableMicrotimingOffset()
        {
            var pattern = new Core.PitchedStepPattern(
                4,
                Core.PitchedPatternStep.Rest,
                new Core.PitchedPatternStep(
                    default,
                    100,
                    120L,
                    microtimingTickOffset: -17),
                new Core.PitchedPatternStep(
                    default,
                    100,
                    120L,
                    microtimingTickOffset: 20));

            Assert.That(pattern.MinimumMicrotimingTickOffset, Is.EqualTo(-17));
        }
    }
}
