using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CardsUnity.Rendering
{
    /// <summary>Keeps Gaussian background blur behind the followed character.</summary>
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(Camera))]
    public sealed class CharacterDepthOfField : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Volume volume;
        [Tooltip("Clear depth behind the character's root, in world metres. Includes room for the animated body.")]
        [SerializeField, Min(0f)] private float sharpMargin = 2f;
        [Tooltip("Depth interval from the start of background blur to its full strength, in metres.")]
        [SerializeField, Min(0.1f)] private float blurRange = 6f;

        private DepthOfField depthOfField;
        private float originalStart, originalEnd;
        private bool originalStartOverride, originalEndOverride;

        private void OnEnable()
        {
            if (volume == null || volume.sharedProfile == null ||
                !volume.profile.TryGet(out depthOfField)) return;
            // Volume.profile is a runtime instance; never animate the shared asset.
            originalStart = depthOfField.gaussianStart.value;
            originalEnd = depthOfField.gaussianEnd.value;
            originalStartOverride = depthOfField.gaussianStart.overrideState;
            originalEndOverride = depthOfField.gaussianEnd.overrideState;
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (target == null || depthOfField == null) return;
            // View-axis depth works for orthographic projection and camera orbit/shake.
            float depth = Vector3.Dot(target.position - transform.position, transform.forward);
            float start = Mathf.Max(0f, depth + sharpMargin);
            depthOfField.gaussianStart.Override(start);
            depthOfField.gaussianEnd.Override(start + Mathf.Max(0.1f, blurRange));
        }

        private void OnDisable()
        {
            if (depthOfField == null) return;
            depthOfField.gaussianStart.value = originalStart;
            depthOfField.gaussianEnd.value = originalEnd;
            depthOfField.gaussianStart.overrideState = originalStartOverride;
            depthOfField.gaussianEnd.overrideState = originalEndOverride;
            depthOfField = null;
        }
    }
}
