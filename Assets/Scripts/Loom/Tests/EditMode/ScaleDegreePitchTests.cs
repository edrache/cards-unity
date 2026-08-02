using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class ScaleDegreePitchTests
    {
        [Test]
        public void ConstructorPreservesDegreeAndOctaveOffset()
        {
            var pitch = new Core.ScaleDegreePitch(-3, 2);

            Assert.That(pitch.Degree, Is.EqualTo(-3));
            Assert.That(pitch.OctaveOffset, Is.EqualTo(2));
        }

        [Test]
        public void DegreePitchSupportsExtremeValues()
        {
            var pitch = new Core.ScaleDegreePitch(int.MinValue, int.MaxValue);

            Assert.That(pitch.Degree, Is.EqualTo(int.MinValue));
            Assert.That(pitch.OctaveOffset, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void ValueEqualityIncludesDegreeAndOctaveOffset()
        {
            var value = new Core.ScaleDegreePitch(4, -1);
            var equal = new Core.ScaleDegreePitch(4, -1);

            Assert.That(value, Is.EqualTo(equal));
            Assert.That(value == equal, Is.True);
            Assert.That(value.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(value != new Core.ScaleDegreePitch(4, 0), Is.True);
            Assert.That(value != new Core.ScaleDegreePitch(3, -1), Is.True);
        }

        [Test]
        public void DefaultDegreePitchRepresentsTonicWithoutOctaveOffset()
        {
            Core.ScaleDegreePitch pitch = default;

            Assert.That(pitch, Is.EqualTo(new Core.ScaleDegreePitch(0, 0)));
        }
    }
}
