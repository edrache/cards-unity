using System;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class TrackIdTests
    {
        [Test]
        public void DefaultTrackIdIsInvalidAndZeroIsRejected()
        {
            Core.TrackId id = default;

            Assert.That(id.IsValid, Is.False);
            Assert.That(id.Value, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => new Core.TrackId(0UL));
        }

        [Test]
        public void TrackIdPreservesStableValueSemantics()
        {
            var first = new Core.TrackId(42UL);
            var equal = new Core.TrackId(42UL);
            var different = new Core.TrackId(43UL);

            Assert.That(first.IsValid, Is.True);
            Assert.That(first.Value, Is.EqualTo(42UL));
            Assert.That(first, Is.EqualTo(equal));
            Assert.That(first.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(first == equal, Is.True);
            Assert.That(first != different, Is.True);
        }
    }
}
