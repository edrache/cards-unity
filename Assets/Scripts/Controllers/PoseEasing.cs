using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Bounded pose blends with zero velocity and acceleration at both ends.</summary>
    internal static class PoseEasing
    {
        public static float SmootherStep(float progress)
        {
            float t = Mathf.Clamp01(progress);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        public static float Window(float progress, float start, float end)
            => SmootherStep((progress - start) / Mathf.Max(0.0001f, end - start));
    }
}
