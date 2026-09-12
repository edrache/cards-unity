using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.Rendering
{
    /// <summary>Opt-in silhouette for the player, restricted to its gameplay camera.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerOcclusionOutline : MonoBehaviour
    {
        public Camera targetCamera;
        public Material maskMaterial;
        public Color color = new Color(0.05f, 0.85f, 1f, 1f);
        [Range(0, 6)] public float width = 2f;
        private static readonly List<PlayerOcclusionOutline> active = new List<PlayerOcclusionOutline>();
        public Renderer[] Renderers { get; private set; }

        private void OnEnable()
        {
            RefreshRenderers();
            active.Add(this);
        }

        private void OnDisable() => active.Remove(this);
        private void OnTransformChildrenChanged() => RefreshRenderers();

        public void RefreshRenderers()
        {
            var renderers = new List<Renderer>();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                if ((renderer is MeshRenderer || renderer is SkinnedMeshRenderer) &&
                    renderer.GetComponentInParent<CardsUnity.Controllers.HandheldTorch>() == null)
                    renderers.Add(renderer);
            Renderers = renderers.ToArray();
        }

        public static PlayerOcclusionOutline ForCamera(Camera camera)
        {
            foreach (var outline in active)
                if (outline != null && outline.targetCamera == camera && outline.width > 0 && outline.maskMaterial != null)
                    return outline;
            return null;
        }
    }
}
