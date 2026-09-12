using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.Rendering
{
    /// <summary>Owns the screen-sized UI target; the presentation material is the UI effects entry point.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class UiRenderTexture : MonoBehaviour
    {
        [SerializeField] private Camera uiCamera;
        [SerializeField] private RawImage presentation;
        private RenderTexture target;

        public RenderTexture Target => target;

        private void OnEnable() => UpdateTarget();
        private void LateUpdate() => UpdateTarget();

        private void UpdateTarget()
        {
            if (uiCamera == null || presentation == null) return;
            int width = Mathf.Max(1, Screen.width);
            int height = Mathf.Max(1, Screen.height);
            if (target != null && target.width == width && target.height == height && target.IsCreated()) return;

            ReleaseTarget();
            // Depth/stencil is required by uGUI Mask (the inventory ScrollRect).
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "UI Render Texture (Runtime)",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1,
                useMipMap = false
            };
            target.Create();
            uiCamera.targetTexture = target;
            presentation.texture = target;
            presentation.enabled = true;
            uiCamera.enabled = true;
        }

        private void OnDisable()
        {
            if (uiCamera != null) uiCamera.enabled = false;
            if (presentation != null) presentation.enabled = false;
            ReleaseTarget();
        }

        private void ReleaseTarget()
        {
            if (target == null) return;
            if (uiCamera != null && uiCamera.targetTexture == target) uiCamera.targetTexture = null;
            if (presentation != null && presentation.texture == target) presentation.texture = null;
            target.Release();
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
            target = null;
        }
    }
}
