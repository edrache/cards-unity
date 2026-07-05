using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class SockGrabMathTests
    {
        [Test]
        public void TryGetPointOnPlane_RayHitsPlane_ReturnsTrueAndPoint()
        {
            var ray = new Ray(new Vector3(0f, 0f, -10f), Vector3.forward);
            bool hit = SockGrabMath.TryGetPointOnPlane(ray, Vector3.zero, Vector3.back, out Vector3 point);
            Assert.IsTrue(hit);
            Assert.AreEqual(Vector3.zero, point);
        }

        [Test]
        public void TryGetPointOnPlane_RayParallelToPlane_ReturnsFalse()
        {
            var ray = new Ray(new Vector3(0f, 5f, -10f), Vector3.up);
            bool hit = SockGrabMath.TryGetPointOnPlane(ray, Vector3.zero, Vector3.back, out _);
            Assert.IsFalse(hit);
        }

        [Test]
        public void TryGetPointOnPlane_OffsetPlanePoint_ReturnsCorrectIntersection()
        {
            var ray = new Ray(new Vector3(2f, 3f, -10f), Vector3.forward);
            bool hit = SockGrabMath.TryGetPointOnPlane(ray, new Vector3(0f, 0f, 5f), Vector3.back, out Vector3 point);
            Assert.IsTrue(hit);
            Assert.AreEqual(new Vector3(2f, 3f, 5f), point);
        }
    }
}
