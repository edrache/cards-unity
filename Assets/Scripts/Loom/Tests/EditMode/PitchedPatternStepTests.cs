using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class PitchedPatternStepTests
    {
        [Test]
        public void DefaultStepIsRestWhileDefaultPitchCanBePlayable()
        {
            Core.PitchedPatternStep rest = default;
            var playable = new Core.PitchedPatternStep(default, 100, 120L);

            Assert.That(rest.IsRest, Is.True);
            Assert.That(rest, Is.EqualTo(Core.PitchedPatternStep.Rest));
            Assert.That(rest.TryGetPitch(out _), Is.False);
            Assert.That(playable.IsRest, Is.False);
            Assert.That(playable.TryGetPitch(out Core.ScaleDegreePitch pitch), Is.True);
            Assert.That(pitch, Is.EqualTo(default(Core.ScaleDegreePitch)));
        }

        [Test]
        public void ConstructorPreservesPitchAndTriggerFields()
        {
            var pitch = new Core.ScaleDegreePitch(-3, 2);
            var step = new Core.PitchedPatternStep(
                pitch,
                87,
                360L,
                0.25f,
                3,
                -12);

            Assert.That(step.TryGetPitch(out Core.ScaleDegreePitch actualPitch), Is.True);
            Assert.That(actualPitch, Is.EqualTo(pitch));
            Assert.That(step.Velocity, Is.EqualTo(87));
            Assert.That(step.DurationTicks, Is.EqualTo(360L));
            Assert.That(step.Probability, Is.EqualTo(0.25f));
            Assert.That(step.RatchetCount, Is.EqualTo(3));
            Assert.That(step.MicrotimingTickOffset, Is.EqualTo(-12));
        }

        [TestCase(0)]
        [TestCase(128)]
        public void ConstructorRejectsVelocityOutsidePlayableRange(int velocity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.PitchedPatternStep(default, velocity, 120L));
        }

        [TestCase(0L)]
        [TestCase(-1L)]
        public void ConstructorRejectsNonPositiveDuration(long durationTicks)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.PitchedPatternStep(default, 100, durationTicks));
        }

        [Test]
        public void ConstructorRejectsInvalidProbability()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.PitchedPatternStep(default, 100, 120L, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.PitchedPatternStep(default, 100, 120L, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.PitchedPatternStep(default, 100, 120L, -0.01f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.PitchedPatternStep(default, 100, 120L, 1.01f));
        }

        [TestCase(0)]
        [TestCase(256)]
        public void ConstructorRejectsRatchetCountOutsideByteRange(int ratchetCount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Core.PitchedPatternStep(
                    default,
                    100,
                    120L,
                    ratchetCount: ratchetCount));
        }

        [Test]
        public void EqualityIncludesPitchAndEveryTriggerField()
        {
            var value = new Core.PitchedPatternStep(
                new Core.ScaleDegreePitch(2, -1),
                100,
                120L,
                0.5f,
                2,
                4);
            var equal = new Core.PitchedPatternStep(
                new Core.ScaleDegreePitch(2, -1),
                100,
                120L,
                0.5f,
                2,
                4);

            Assert.That(value, Is.EqualTo(equal));
            Assert.That(value == equal, Is.True);
            Assert.That(value.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(
                value != new Core.PitchedPatternStep(
                    new Core.ScaleDegreePitch(3, -1),
                    100,
                    120L,
                    0.5f,
                    2,
                    4),
                Is.True);
        }
    }
}
