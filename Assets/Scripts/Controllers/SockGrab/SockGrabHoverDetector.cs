using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity
{
    public static class SockGrabHoverDetector
    {
        public static int FindClosestHandleWithinRadius(Vector2 pointerScreenPosition, IReadOnlyList<Vector2> handleScreenPositions, float radiusPixels)
        {
            int closestIndex = -1;
            float closestSqrDistance = radiusPixels * radiusPixels;

            for (int i = 0; i < handleScreenPositions.Count; i++)
            {
                float sqrDistance = (handleScreenPositions[i] - pointerScreenPosition).sqrMagnitude;
                if (sqrDistance <= closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }
    }
}
