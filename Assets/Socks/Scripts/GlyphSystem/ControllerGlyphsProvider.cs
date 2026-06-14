using System;
using UnityEngine;

namespace GlyphSystem
{
    /// <summary>
    /// Resolves the correct glyph sprite for a given Rewired action name.
    /// Place on a GameObject alongside GlyphInputManager and wire up in the Inspector.
    /// </summary>
    public class ControllerGlyphsProvider : MonoBehaviour
    {
        [SerializeField] private GlyphInputManager _inputManager;
        [SerializeField] private ControllerEntry[] _controllerEntries;
        [SerializeField] private ControllerEntry _keyboardMouseEntry;

        /// <summary>Returns the sprite for <paramref name="inputId"/> using the currently active controller.</summary>
        public Sprite GetGlyphSprite(string inputId, Guid controllerId = default)
        {
            bool isTemplateUsed = false;

            bool found = controllerId == default
                ? _inputManager.GetControllerElementDataByActiveController(inputId, out controllerId, out int elementId, out string elementName)
                : _inputManager.GetControllerElementDataByController(controllerId, inputId, out elementId);

            if (!found)
            {
                isTemplateUsed = true;
                _inputManager.GetControllerElementDataByDefaultTemplate(inputId, out controllerId, out elementId);
                elementName = "";
            }
            else
            {
                _inputManager.GetControllerElementDataByActiveController(inputId, out _, out elementId, out elementName);
            }

            controllerId = _inputManager.NormalizeSwitchControllerGuid(controllerId);
            Sprite sprite = GetGlyphSpriteInternal(controllerId, elementId, elementName);

            if (sprite == null && !isTemplateUsed)
            {
                _inputManager.GetControllerElementDataByDefaultTemplate(inputId, out controllerId, out elementId);
                sprite = GetGlyphSpriteInternal(controllerId, elementId, "");
            }

            return sprite;
        }

        /// <summary>Returns the full <see cref="ControllerGlyphEntry"/> for <paramref name="inputId"/>.</summary>
        public ControllerGlyphEntry GetControllerEntry(string inputId)
        {
            if (!InputControllerState.IsUsingGamepad)
                return GetMouseKeyboardEntry(inputId);

            if (_inputManager.GetControllerElementDataByActiveController(inputId, out Guid controllerId, out int elementId, out string _))
            {
                ControllerGlyphEntry entry = GetControllerGlyphEntryById(controllerId, elementId);
                if (entry != null) return entry;
            }

            ControllerGlyphEntry byName = GetControllerGlyphEntryByName(default, inputId);
            if (byName != null) return byName;

            if (_inputManager.GetControllerElementDataByDefaultTemplate(inputId, out controllerId, out elementId))
            {
                ControllerGlyphEntry entry = GetControllerGlyphEntryById(controllerId, elementId);
                if (entry != null) return entry;
            }

            return GetControllerGlyphEntryByName(default, inputId);
        }

        // ── Private ────────────────────────────────────────────────────────────

        private Sprite GetGlyphSpriteInternal(Guid controllerId, int elementId, string elementName)
        {
            for (int i = 0; i < _controllerEntries.Length; i++)
            {
                ControllerEntry entry = _controllerEntries[i];
                if (!entry.IsSetUpForControllerGuid(controllerId)) continue;

                ControllerGlyphEntry glyphEntry = entry.GetGlyphById(elementId, out _);
                if (glyphEntry == null || glyphEntry.GlyphIcon == null) return null;

                return glyphEntry.GlyphIcon;
            }
            return null;
        }

        private ControllerGlyphEntry GetMouseKeyboardEntry(string inputId)
        {
            _inputManager.GetControllerElementDataByMouseKeyboard(inputId, out _, out int elementId, out _);
            ControllerGlyphEntry entry = _keyboardMouseEntry.GetGlyphById(elementId, out _);
            return entry ?? _keyboardMouseEntry.GetGlyphByName(inputId);
        }

        private ControllerGlyphEntry GetControllerGlyphEntryById(Guid controllerId, int elementId)
        {
            for (int i = 0; i < _controllerEntries.Length; i++)
            {
                ControllerEntry entry = _controllerEntries[i];
                if (!entry.IsSetUpForControllerGuid(controllerId)) continue;
                return entry.GetGlyphById(elementId, out _);
            }
            return null;
        }

        private ControllerGlyphEntry GetControllerGlyphEntryByName(Guid controllerId, string name)
        {
            for (int i = 0; i < _controllerEntries.Length; i++)
            {
                ControllerEntry entry = _controllerEntries[i];
                if (controllerId != default && !entry.IsSetUpForControllerGuid(controllerId)) continue;
                ControllerGlyphEntry found = entry.GetGlyphByName(name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
