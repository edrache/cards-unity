using System;
using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Lightweight falling water authored within a room-content rule.</summary>
    [Serializable]
    public sealed class CaveWaterSettings
    {
        [Tooltip("Shared Cards Unity/Cave Water material. Missing material disables water and its extinguishing volume.")]
        public Material material;
        [Tooltip("Full stream width. Narrow values create a dripping stream.")]
        [Range(0.1f, 3f)] public float width = 0.8f;
        [Tooltip("Vertical distance from the floor to the source; cutaway walls do not limit this height.")]
        [Range(0.5f, 6f)] public float height = 2.5f;
        [Tooltip("Full stream depth; also defines the wet contact volume.")]
        [Range(0.1f, 2f)] public float depth = 0.35f;

        public CaveWaterSettings Clone() => (CaveWaterSettings)MemberwiseClone();
        public void Validate()
        {
            width = Mathf.Clamp(Finite(width, 0.8f), 0.1f, 3f);
            height = Mathf.Clamp(Finite(height, 2.5f), 0.5f, 6f);
            depth = Mathf.Clamp(Finite(depth, 0.35f), 0.1f, 2f);
        }
        private static float Finite(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }
}
