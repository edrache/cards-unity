using UnityEngine;

namespace CardsUnity
{
    public static class SockGrabMath
    {
        public static bool TryGetPointOnPlane(Ray ray, Vector3 planePoint, Vector3 planeNormal, out Vector3 worldPoint)
        {
            Plane plane = new Plane(planeNormal, planePoint);
            if (plane.Raycast(ray, out float distance))
            {
                worldPoint = ray.GetPoint(distance);
                return true;
            }

            worldPoint = planePoint;
            return false;
        }
    }
}
