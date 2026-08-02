using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class PercussionPatternStepTests
    {
        [Test]
        public void DefaultAndCanonicalRestExposeNoPlayableNote()
        {
            Core.PercussionPatternStep defaultStep = default;
            Core.PercussionPatternStep rest = Core.PercussionPatternStep.Rest;

            Assert.That(defaultStep.IsRest, Is.True);
            Assert.That(rest.IsRest, Is.True);
            Assert.That(rest.TryGetNote(out _), Is.False);
            Assert.That(defaultStep, Is.EqualTo(rest));
        }

        [Test]
        public void PlayableSampleSlotRemainsDistinctFromRest()
        {
            var step = new Core.PercussionPatternStep(new Core.PercussionNote(1UL), 1, 1L);

            Assert.That(step.IsRest, Is.False);
            Assert.That(step.TryGetNote(out Core.PercussionNote note), Is.True);
            Assert.That(note.SampleSlotId, Is.EqualTo(1UL));
            Assert.That(step, Is.Not.EqualTo(Core.PercussionPatternStep.Rest));
        }

        [Test]
        public void PlayableStepPreservesEveryResolvedValue()
        {
            var note = new Core.PercussionNote(69UL);
            var step = new Core.PercussionPatternStep(
                note,
                velocity: 96,
                durationTicks: 180L,
                probability: 0.75f,
                ratchetCount: 3,
                microtimingTickOffset: -12);

            Assert.That(step.IsRest, Is.False);
            Assert.That(step.TryGetNote(out Core.PercussionNote resolvedNote), Is.True);
            Assert.That(resolvedNote, Is.EqualTo(note));
            Assert.That(step.Velocity, Is.EqualTo(96));
            Assert.That(step.DurationTicks, Is.EqualTo(180L));
            Assert.That(step.Probability, Is.EqualTo(0.75f));
            Assert.That(step.RatchetCount, Is.EqualTo(3));
            Assert.That(step.MicrotimingTickOffset, Is.EqualTo(-12));
        }

        [TestCase(0)]
        [TestCase(128)]
        public void PlayableStepRejectsVelocityOutsideThePlayableRange(int velocity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.PercussionPatternStep(new Core.PercussionNote(60), velocity, 120L));
        }

        [TestCase(0L)]
        [TestCase(-1L)]
        public void PlayableStepRejectsNonPositiveDuration(long durationTicks)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.PercussionPatternStep(new Core.PercussionNote(60), 100, durationTicks));
        }

        [TestCase(-0.01f)]
        [TestCase(1.01f)]
        public void PlayableStepRejectsProbabilityOutsideTheUnitInterval(float probability)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.PercussionPatternStep(
                    new Core.PercussionNote(60),
                    100,
                    120L,
                    probability));
        }

        [Test]
        public void PlayableStepRejectsNonFiniteProbability()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.PercussionPatternStep(new Core.PercussionNote(60), 100, 120L, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.PercussionPatternStep(
                    new Core.PercussionNote(60),
                    100,
                    120L,
                    float.PositiveInfinity));
        }

        [TestCase(0)]
        [TestCase(256)]
        public void PlayableStepRejectsRatchetCountOutsideTheBoundedRange(
            int ratchetCount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.PercussionPatternStep(
                    new Core.PercussionNote(60),
                    100,
                    120L,
                    ratchetCount: ratchetCount));
        }

        [Test]
        public void StepValueEqualityIncludesEveryField()
        {
            var left = new Core.PercussionPatternStep(new Core.PercussionNote(60), 100, 120L, 0.5f, 2, -10);
            var equal = new Core.PercussionPatternStep(new Core.PercussionNote(60), 100, 120L, 0.5f, 2, -10);
            var different = new Core.PercussionPatternStep(new Core.PercussionNote(60), 100, 120L, 0.5f, 2, 10);

            Assert.That(left, Is.EqualTo(equal));
            Assert.That(left.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(left == equal, Is.True);
            Assert.That(left != different, Is.True);
        }
    }
}
