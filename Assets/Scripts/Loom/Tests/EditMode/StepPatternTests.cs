using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class StepPatternTests
    {
        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(7)]
        [TestCase(961)]
        public void PatternRejectsGridThatDoesNotDivideTheBeat(int stepsPerBeat)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.StepPattern(stepsPerBeat, Core.PatternStep.Rest));
        }

        [Test]
        public void PatternRejectsMissingOrEmptySteps()
        {
            Assert.Throws<ArgumentNullException>(
                () => new Core.StepPattern(4, null));
            Assert.Throws<ArgumentException>(
                () => new Core.StepPattern(4, Array.Empty<Core.PatternStep>()));
        }

        [Test]
        public void PatternOwnsItsStepDataAndExposesGridLength()
        {
            var source = new[]
            {
                new Core.PatternStep(new Core.Note(60), 100, 120L),
                Core.PatternStep.Rest,
                new Core.PatternStep(new Core.Note(64), 90, 240L),
                Core.PatternStep.Rest
            };
            var pattern = new Core.StepPattern(4, source);

            source[0] = Core.PatternStep.Rest;

            Assert.That(pattern.StepsPerBeat, Is.EqualTo(4));
            Assert.That(pattern.TicksPerStep, Is.EqualTo(240));
            Assert.That(pattern.Count, Is.EqualTo(4));
            Assert.That(pattern.LengthTicks, Is.EqualTo(960L));
            Assert.That(pattern[0].IsRest, Is.False);
            Assert.That(pattern[1].IsRest, Is.True);
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void PatternIndexerRejectsIndicesOutsideTheOwnedSteps(int index)
        {
            var pattern = new Core.StepPattern(
                4,
                Core.PatternStep.Rest,
                Core.PatternStep.Rest);

            ArgumentOutOfRangeException exception =
                Assert.Throws<ArgumentOutOfRangeException>(() => _ = pattern[index]);

            Assert.That(exception.ParamName, Is.EqualTo("index"));
        }

        [TestCase(-240)]
        [TestCase(240)]
        public void PatternRejectsMicrotimingOutsideOneStepInterval(int offset)
        {
            var step = new Core.PatternStep(
                new Core.Note(60),
                100,
                120L,
                microtimingTickOffset: offset);

            Assert.Throws<ArgumentException>(() => new Core.StepPattern(4, step));
        }

        [Test]
        public void PatternAcceptsMicrotimingEdgesAndRecordsTheEarliestOffset()
        {
            var pattern = new Core.StepPattern(
                4,
                new Core.PatternStep(
                    new Core.Note(60),
                    100,
                    120L,
                    microtimingTickOffset: -239),
                new Core.PatternStep(
                    new Core.Note(64),
                    100,
                    120L,
                    microtimingTickOffset: 239));

            Assert.That(pattern.MinimumMicrotimingTickOffset, Is.EqualTo(-239));
        }

        [Test]
        public void PatternRejectsMoreRatchetsThanDistinctTicksInTheStep()
        {
            var step = new Core.PatternStep(
                new Core.Note(60),
                100,
                120L,
                ratchetCount: 2);

            Assert.Throws<ArgumentException>(
                () => new Core.StepPattern(
                    Core.MusicalTime.TicksPerBeat,
                    step));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(10)]
        [TestCase(12)]
        [TestCase(15)]
        [TestCase(16)]
        public void PatternAcceptsBoundedRatchetsWithDistinctIntegerOnsets(
            int ratchetCount)
        {
            var step = new Core.PatternStep(
                new Core.Note(60),
                100,
                120L,
                ratchetCount: ratchetCount);

            var pattern = new Core.StepPattern(4, step);

            Assert.That(pattern[0].RatchetCount, Is.EqualTo(ratchetCount));
        }

        [Test]
        public void RestStepsDoNotContributeTimingOrRatchetValidation()
        {
            var pattern = new Core.StepPattern(
                MusicalTimeDivisorForSingleTickSteps(),
                Core.PatternStep.Rest);

            Assert.That(pattern.TicksPerStep, Is.EqualTo(1));
            Assert.That(pattern.MinimumMicrotimingTickOffset, Is.Zero);
        }

        private static int MusicalTimeDivisorForSingleTickSteps()
        {
            return Core.MusicalTime.TicksPerBeat;
        }
    }
}
