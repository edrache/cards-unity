using UnityEngine;
using UnityEngine.UI;

namespace GlyphSystem
{
    /// <summary>
    /// Attach to a UI GameObject alongside an Image component.
    /// Set _actionName to the Rewired action name (e.g. "Jump").
    /// Wire up _inputManager and _glyphsProvider in the Inspector or via a parent initializer.
    /// The Image is shown only when a gamepad is active, and refreshes when the controller changes.
    /// </summary>
    public class ControllerGlyphDisplayer : MonoBehaviour
    {
        [SerializeField] protected string _actionName;
        [SerializeField] protected Image _glyphImage;

        [Space]
        [SerializeField] private GlyphInputManager _inputManager;
        [SerializeField] protected ControllerGlyphsProvider _glyphsProvider;

        private bool IsGamepad => InputControllerState.IsUsingGamepad;

        private void Start()
        {
            if (_inputManager != null)
                _inputManager.ControllerChanged += OnControllerChanged;

            RefreshGlyph();
        }

        private void OnDestroy()
        {
            if (_inputManager != null)
                _inputManager.ControllerChanged -= OnControllerChanged;
        }

        private void OnEnable()
        {
            UpdateVisibility();
        }

        private void OnDisable()
        {
            if (_glyphImage != null)
                _glyphImage.enabled = false;
        }

        private void OnControllerChanged(GameControlType _)
        {
            if (!isActiveAndEnabled) return;
            RefreshGlyph();
        }

        protected virtual void RefreshGlyph()
        {
            if (_glyphImage == null || _glyphsProvider == null) return;
            _glyphImage.sprite = _glyphsProvider.GetGlyphSprite(_actionName);
            UpdateVisibility();
        }

        protected void UpdateVisibility()
        {
            if (_glyphImage == null) return;
            _glyphImage.enabled = IsGamepad;
        }
    }
}
