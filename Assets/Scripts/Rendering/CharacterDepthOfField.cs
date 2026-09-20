using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CardsUnity.Rendering
{
    /// <summary>Camera-local character focus for circular, tilt-shift or background-depth blur.</summary>
    [ExecuteAlways, DefaultExecutionOrder(1000), RequireComponent(typeof(Camera))]
    public sealed class CharacterDepthOfField : MonoBehaviour
    {
        public enum FocusMode { BackgroundDepth, Circle, TiltShift }

        [SerializeField] private Transform target;
        [SerializeField] private Volume volume;
        [SerializeField] private FocusMode mode = FocusMode.Circle;
        [Tooltip("Offset from the projected target, in fractions of viewport height. Positive Y moves up.")]
        [SerializeField] private Vector2 centerOffset = new Vector2(0f, 0.03f);
        [Tooltip("Maximum Gaussian blur radius. Zero disables blur. URP supports up to 1.5.")]
        [SerializeField, Range(0f, 1.5f)] private float blurStrength = 1.2f;

        [Header("Circle (fractions of viewport height)")]
        [SerializeField, Range(0f, 0.5f)] private float sharpRadius = 0.14f;
        [SerializeField, Range(0.001f, 0.5f)] private float circleTransition = 0.16f;

        [Header("Tilt Shift (fractions of viewport height)")]
        [Tooltip("Rotation of the sharp band. Zero is horizontal; positive angles rise to the right.")]
        [SerializeField, Range(-90f, 90f)] private float tiltAngle;
        [SerializeField, Range(0f, 0.5f)] private float upperSharpWidth = 0.08f;
        [SerializeField, Range(0f, 0.5f)] private float lowerSharpWidth = 0.08f;
        [SerializeField, Range(0.001f, 0.5f)] private float upperTransition = 0.18f;
        [SerializeField, Range(0.001f, 0.5f)] private float lowerTransition = 0.18f;

        [Header("Background Depth (world metres)")]
        [SerializeField, Min(0f)] private float sharpMargin = 2f;
        [SerializeField, Min(0.1f)] private float blurRange = 6f;

        private static readonly Dictionary<Camera, CharacterDepthOfField> Cameras = new();
        private static readonly int FocusCenter = Shader.PropertyToID("_CharacterFocusCenter");
        private static readonly int FocusShape = Shader.PropertyToID("_CharacterFocusShape");
        private static readonly int FocusBand = Shader.PropertyToID("_CharacterFocusBand");
        private Camera focusCamera;
        private Volume boundVolume;
        private DepthOfField depthOfField;
        private DepthOfField original;

        private void OnEnable()
        {
            focusCamera = GetComponent<Camera>();
            if (Cameras.Count == 0)
            {
                RenderPipelineManager.beginCameraRendering += BeginCamera;
                RenderPipelineManager.endCameraRendering += EndCamera;
            }
            Cameras[focusCamera] = this;
        }

        private static void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            // Always reset for UI, Scene View and other cameras using the same renderer.
            Shader.SetGlobalVector(FocusCenter, Vector4.zero);
            if (Cameras.TryGetValue(camera, out var focus) && focus != null)
                focus.UpdateFocus();
        }

        private static void EndCamera(ScriptableRenderContext context, Camera camera)
            => Shader.SetGlobalVector(FocusCenter, Vector4.zero);

        private void UpdateFocus()
        {
            if (volume != boundVolume)
                RestoreProfile();
            if (target == null || volume == null || !volume.isActiveAndEnabled)
            {
                RestoreProfile();
                return;
            }
            if (depthOfField == null)
            {
                if (volume.sharedProfile == null || !volume.profile.TryGet(out depthOfField)) return;
                boundVolume = volume;
                // Snapshot only this effect; animate a Volume instance, never the shared asset.
                original = Instantiate(depthOfField);
                original.hideFlags = HideFlags.HideAndDontSave;
            }
            Vector3 center = focusCamera.WorldToViewportPoint(target.position);
            if (center.z <= 0f)
            {
                RestoreProfile();
                return;
            }
            depthOfField.active = true;
            depthOfField.mode.Override(DepthOfFieldMode.Gaussian);
            depthOfField.gaussianMaxRadius.Override(Mathf.Clamp(blurStrength, 0f, 1.5f));
            float start = Mathf.Max(0f, center.z + sharpMargin);
            depthOfField.gaussianStart.Override(start);
            depthOfField.gaussianEnd.Override(start + Mathf.Max(0.1f, blurRange));
            if (mode == FocusMode.BackgroundDepth) return;

            // Shader coordinates are measured in viewport heights, so circles stay circular.
            float aspect = Mathf.Max(0.001f, focusCamera.aspect);
            center.x += centerOffset.x / aspect;
            center.y += centerOffset.y;
            Shader.SetGlobalVector(FocusCenter, new Vector4(center.x, center.y, aspect, (float)mode));
            Shader.SetGlobalVector(FocusShape, new Vector4(Mathf.Max(0f, sharpRadius),
                Mathf.Max(0.001f, circleTransition), Mathf.Sin(tiltAngle * Mathf.Deg2Rad),
                Mathf.Cos(tiltAngle * Mathf.Deg2Rad)));
            Shader.SetGlobalVector(FocusBand, new Vector4(Mathf.Max(0f, upperSharpWidth),
                Mathf.Max(0f, lowerSharpWidth), Mathf.Max(0.001f, upperTransition),
                Mathf.Max(0.001f, lowerTransition)));
        }

        private void RestoreProfile()
        {
            if (depthOfField != null && original != null)
            {
                depthOfField.active = original.active;
                depthOfField.mode.value = original.mode.value;
                depthOfField.mode.overrideState = original.mode.overrideState;
                depthOfField.gaussianStart.value = original.gaussianStart.value;
                depthOfField.gaussianStart.overrideState = original.gaussianStart.overrideState;
                depthOfField.gaussianEnd.value = original.gaussianEnd.value;
                depthOfField.gaussianEnd.overrideState = original.gaussianEnd.overrideState;
                depthOfField.gaussianMaxRadius.value = original.gaussianMaxRadius.value;
                depthOfField.gaussianMaxRadius.overrideState = original.gaussianMaxRadius.overrideState;
            }
            CoreUtils.Destroy(original);
            original = null;
            depthOfField = null;
            boundVolume = null;
        }

        private void OnDisable()
        {
            if (focusCamera != null) Cameras.Remove(focusCamera);
            if (Cameras.Count == 0)
            {
                RenderPipelineManager.beginCameraRendering -= BeginCamera;
                RenderPipelineManager.endCameraRendering -= EndCamera;
                Shader.SetGlobalVector(FocusCenter, Vector4.zero);
            }
            RestoreProfile();
        }
    }
}
