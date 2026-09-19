using System;
using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Authoring for one independently seeded grass room-content rule.</summary>
    [Serializable]
    public sealed class CaveGrassSettings
    {
        [Range(0f, 48f)] public float grassBladesPerSquareMetre = 18f;
        [Range(0.3f, 2.5f)] public float grassHeight = 1.2f;
        [Tooltip("Independent per-blade height variation around Grass Height. 0.55 gives 45%-155%; zero gives equal heights.")]
        [Range(0f, 0.85f)] public float grassHeightVariation = 0.55f;
        [Tooltip("Nominal full blade width at the root, in metres. Tips taper independently of height.")]
        [Range(0.02f, 0.25f)] public float grassWidth = 0.1f;
        [Tooltip("Independent per-blade width variation around Grass Width. 0.65 gives 35%-165%; zero gives equal widths.")]
        [Range(0f, 0.85f)] public float grassWidthVariation = 0.65f;
        [Tooltip("Total blade budget for this grass rule. Large rooms share a reduced density to stay within this limit.")]
        [Range(0, 120000)] public int grassMaximumBlades = 60000;
        [Range(8f, 80f)] public float grassDrawDistance = 32f;
        [Range(0.3f, 3f)] public float grassBendRadius = 1.1f;
        [Tooltip("Assign a Cards Unity/Cave Grass material. Missing material disables grass, including in builds.")]
        public Material grassMaterial;

        public CaveGrassSettings Clone() => (CaveGrassSettings)MemberwiseClone();

        public void Validate()
        {
            grassBladesPerSquareMetre = Mathf.Clamp(Finite(grassBladesPerSquareMetre, 18f), 0f, 48f);
            grassHeight = Mathf.Clamp(Finite(grassHeight, 1.2f), 0.3f, 2.5f);
            grassHeightVariation = Mathf.Clamp(Finite(grassHeightVariation, 0.55f), 0f, 0.85f);
            grassWidth = Mathf.Clamp(Finite(grassWidth, 0.1f), 0.02f, 0.25f);
            grassWidthVariation = Mathf.Clamp(Finite(grassWidthVariation, 0.65f), 0f, 0.85f);
            grassMaximumBlades = Mathf.Clamp(grassMaximumBlades, 0, 120000);
            grassDrawDistance = Mathf.Clamp(Finite(grassDrawDistance, 32f), 8f, 80f);
            grassBendRadius = Mathf.Clamp(Finite(grassBendRadius, 1.1f), 0.3f, 3f);
        }

        private static float Finite(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }
}
