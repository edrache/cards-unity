using NUnit.Framework;

namespace Loom.Tests.PlayMode
{
    public sealed class AssemblyAvailabilityTests
    {
        [Test]
        public void RuntimeAssemblyChainIsAvailable()
        {
            Assert.That(Demo.AssemblyAnchor.CoreAnchorType, Is.EqualTo(typeof(Core.AssemblyAnchor)));
            Assert.That(Demo.AssemblyAnchor.FmodAnchorType, Is.EqualTo(typeof(Fmod.AssemblyAnchor)));
            Assert.That(Demo.AssemblyAnchor.UnityAnchorType, Is.EqualTo(typeof(Unity.AssemblyAnchor)));
        }
    }
}
