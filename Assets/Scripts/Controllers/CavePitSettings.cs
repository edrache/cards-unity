using System;
using UnityEngine;

namespace CardsUnity.Controllers
{
    [Serializable]
    public sealed class CavePitSettings
    {
        [Tooltip("Percentage of rooms requesting one open pit. Zero disables this variant; crowded rooms may be skipped.")]
        [Range(0f, 100f)] public float openRoomPercentage;
        [Tooltip("Percentage of rooms requesting one cracked, collapsing pit. Placement keeps existing content and routes clear.")]
        [Range(0f, 100f)] public float crackedRoomPercentage;
        [Tooltip("Minimum full extent of each independently sampled ellipse axis, before irregular polygon shaping.")]
        [Range(2f, 12f)] public float minimumSize = 2.5f;
        [Range(2f, 12f)] public float maximumSize = 4f;
        [Tooltip("Uneven spacing of rim vertices. Shape remains convex, with a separate random rotation and aspect ratio.")]
        [Range(0.1f, 1f)] public float irregularity = 0.75f;
        [Min(3f)] public float depth = 7f;
        [Tooltip("One duration is chosen per cracked pit from this list. Time from stepping on the cover until all fragments have fallen. One entry gives a fixed duration.")]
        public float[] collapseSeconds = { 0.8f, 1.2f, 1.8f };

        public CavePitSettings Clone()
        {
            var copy = (CavePitSettings)MemberwiseClone();
            copy.collapseSeconds = collapseSeconds != null ? (float[])collapseSeconds.Clone() : null;
            return copy;
        }

        public void Validate()
        {
            openRoomPercentage = Mathf.Clamp(Finite(openRoomPercentage, 0f), 0f, 100f);
            crackedRoomPercentage = Mathf.Clamp(Finite(crackedRoomPercentage, 0f), 0f, 100f);
            minimumSize = Mathf.Clamp(Finite(minimumSize, 2.5f), 2f, 12f);
            maximumSize = Mathf.Clamp(Finite(maximumSize, 4f), minimumSize, 12f);
            irregularity = Mathf.Clamp(Finite(irregularity, 0.75f), 0.1f, 1f);
            depth = Mathf.Clamp(Finite(depth, 7f), 3f, 30f);
            if (collapseSeconds == null || collapseSeconds.Length == 0) collapseSeconds = new[] { 1.2f };
            for (int i = 0; i < collapseSeconds.Length; i++)
                collapseSeconds[i] = Mathf.Clamp(Finite(collapseSeconds[i], 1.2f), 0.1f, 30f);
        }

        private static float Finite(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }
}
