using System.Linq;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class AssemblyBoundaryTests
    {
        [Test]
        public void CoreAssemblyDoesNotReferenceUnityOrFmod()
        {
            string[] references = typeof(Core.AssemblyAnchor).Assembly
                .GetReferencedAssemblies()
                .Select(assembly => assembly.Name)
                .ToArray();

            Assert.That(references.Any(name => name.StartsWith("UnityEngine")), Is.False);
            Assert.That(references, Does.Not.Contain("FMODUnity"));
            Assert.That(references, Does.Not.Contain("Loom.Fmod"));
        }

        [Test]
        public void AdapterAssembliesExposeTheirDeclaredDependencies()
        {
            Assert.That(Fmod.AssemblyAnchor.CoreAnchorType, Is.EqualTo(typeof(Core.AssemblyAnchor)));
            Assert.That(Fmod.AssemblyAnchor.FmodSystemType.FullName, Is.EqualTo("FMOD.System"));
            Assert.That(Unity.AssemblyAnchor.FmodAnchorType, Is.EqualTo(typeof(Fmod.AssemblyAnchor)));
        }
    }
}
