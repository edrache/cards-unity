using UnityEngine;

namespace CardsUnity.Controllers
{
    public sealed class StoryVeilDissolve : MonoBehaviour
    {
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");

        [SerializeField] private Renderer targetRenderer;
        [Range(0f, 1f)] [SerializeField] private float progress;

        public float Progress => progress;

        private MaterialPropertyBlock propertyBlock;
        private bool authoredEnabled;
        private bool hasAuthoredEnabled;

        private void Awake()
        {
            CaptureAuthoredEnabledState();
            SetProgress(progress);
        }

        private void OnEnable()
        {
            CaptureAuthoredEnabledState();
            SetProgress(progress);
        }

        public void SetProgress(float value)
        {
            progress = Mathf.Clamp01(value);
            if (targetRenderer == null) return;

            CaptureAuthoredEnabledState();
            propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(ProgressId, progress);
            targetRenderer.SetPropertyBlock(propertyBlock);

            targetRenderer.enabled = progress < 1f && authoredEnabled;
        }

        private void CaptureAuthoredEnabledState()
        {
            if (hasAuthoredEnabled || targetRenderer == null) return;
            authoredEnabled = targetRenderer.enabled;
            hasAuthoredEnabled = true;
        }
    }
}
