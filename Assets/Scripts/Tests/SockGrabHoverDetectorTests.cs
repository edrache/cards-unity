using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class SockGrabHoverDetectorTests
    {
        [Test]
        public void FindClosestHandleWithinRadius_ReturnsMinusOne_WhenNoHandles()
        {
            int result = SockGrabHoverDetector.FindClosestHandleWithinRadius(Vector2.zero, new List<Vector2>(), 50f);
            Assert.AreEqual(-1, result);
        }

        [Test]
        public void FindClosestHandleWithinRadius_ReturnsMinusOne_WhenAllHandlesOutsideRadius()
        {
            var handles = new List<Vector2> { new Vector2(200f, 200f) };
            int result = SockGrabHoverDetector.FindClosestHandleWithinRadius(Vector2.zero, handles, 50f);
            Assert.AreEqual(-1, result);
        }

        [Test]
        public void FindClosestHandleWithinRadius_ReturnsIndex_WhenHandleWithinRadius()
        {
            var handles = new List<Vector2> { new Vector2(10f, 0f) };
            int result = SockGrabHoverDetector.FindClosestHandleWithinRadius(Vector2.zero, handles, 50f);
            Assert.AreEqual(0, result);
        }

        [Test]
        public void FindClosestHandleWithinRadius_ReturnsClosest_WhenMultipleWithinRadius()
        {
            var handles = new List<Vector2> { new Vector2(40f, 0f), new Vector2(10f, 0f) };
            int result = SockGrabHoverDetector.FindClosestHandleWithinRadius(Vector2.zero, handles, 50f);
            Assert.AreEqual(1, result);
        }
    }
}
