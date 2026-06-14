using System;
using Rewired.Data.Mapping;
using UnityEngine;

namespace GlyphSystem
{
    [Serializable]
    public class ControllerEntry
    {
        public string Name;
        [Header("Only one of Joystick or Template should be set")]
        public HardwareJoystickMap Joystick;
        public HardwareJoystickTemplateMap Template;
        public ControllerGlyphEntry[] ControllerGlyphs;

        public bool IsSetUpForControllerGuid(Guid controllerGuid)
        {
            return (Joystick && Joystick.Guid == controllerGuid)
                || (Template && Template.Guid == controllerGuid);
        }

        public ControllerGlyphEntry GetGlyphById(int elementIdentifierId, out string elementName)
        {
            elementName = "";
            if (ControllerGlyphs == null) return null;
            for (int i = 0; i < ControllerGlyphs.Length; i++)
            {
                if (ControllerGlyphs[i] == null || ControllerGlyphs[i].ElementIdentifierId != elementIdentifierId)
                    continue;
                elementName = ControllerGlyphs[i].ElementIdentifierName;
                return ControllerGlyphs[i];
            }
            return null;
        }

        public ControllerGlyphEntry GetGlyphByName(string elementName)
        {
            if (ControllerGlyphs == null) return null;
            for (int i = 0; i < ControllerGlyphs.Length; i++)
            {
                if (ControllerGlyphs[i] == null || ControllerGlyphs[i].ElementIdentifierName != elementName)
                    continue;
                return ControllerGlyphs[i];
            }
            return null;
        }
    }
}
