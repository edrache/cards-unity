using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class PatternIdTests
    {
        [Test]
        public void DefaultPatternIdIsInvalidAndZeroIsRejected()
        {
            Core.PatternId id = default;

            Assert.That(id.IsValid, Is.False);
            Assert.That(id.Value, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => new Core.PatternId(0UL));
        }

        [Test]
        public void PatternIdPreservesStableValueSemantics()
        {
            var first = new Core.PatternId(42UL);
            var equal = new Core.PatternId(42UL);
            var different = new Core.PatternId(43UL);

            Assert.That(first.IsValid, Is.True);
            Assert.That(first.Value, Is.EqualTo(42UL));
            Assert.That(first, Is.EqualTo(equal));
            Assert.That(first.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(first == equal, Is.True);
            Assert.That(first != different, Is.True);
        }
    }
}
