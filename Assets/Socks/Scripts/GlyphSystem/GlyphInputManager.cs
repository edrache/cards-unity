using System;
using Rewired;
using UnityEngine;

namespace GlyphSystem
{
    /// <summary>
    /// Stripped version of FlamecraftInputManager — only the glyph-related Rewired API.
    /// Drop this MonoBehaviour onto a persistent GameObject and wire up the SerializeField
    /// if you need to override the default Rewired Player ID (0).
    /// </summary>
    public class GlyphInputManager : MonoBehaviour
    {
        private const int PlayerId = 0;
        private const string DefaultCategoryName = "Default";
        private const string DefaultLayoutName = "Default";

        private static readonly Guid s_gamepadTemplate        = new("83b427e4-086f-47f3-bb06-be266abd1ca5");
        private static readonly Guid s_nintendoSwitchHandheld = new("1fbdd13b-0795-4173-8a95-a2a75de9d204");
        private static readonly Guid s_nintendoSwitchDual     = new("521b808c-0248-4526-bc10-f1d16ee76bf1");
        private static readonly Guid s_nintendoSwitchProController = new("7bf3154b-9db8-4d52-950f-cd0eed8a5819");
        private static readonly Guid s_sonyDualSenseTemplate  = new("5286706d-19b4-4a45-b635-207ce78d8394");

        private Player _player;
        private ControllerTemplateMap _defaultControllerTemplateMap;
        private Controller _lastActiveController;

        public static GameControlType GameControlType { get; private set; }

        public event Action<GameControlType> ControllerChanged;

        private void Awake()
        {
            _player = ReInput.players.GetPlayer(PlayerId);
            _defaultControllerTemplateMap = ReInput.mapping.GetControllerTemplateMapInstance(
                s_gamepadTemplate, DefaultCategoryName, DefaultLayoutName);

            _player.controllers.AddLastActiveControllerChangedDelegate(OnActiveControllerChanged);

            bool hasJoystick = _player.controllers.joystickCount > 0;
            GameControlType = hasJoystick ? GameControlType.Gamepad : GameControlType.MouseTouch;
            InputControllerState.IsUsingGamepad = hasJoystick;
        }

        private void OnDestroy()
        {
            _player.controllers.RemoveLastActiveControllerChangedDelegate(OnActiveControllerChanged);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public Player GetPlayer() => _player;

        public bool HasAnyJoystickController() => _player.controllers.joystickCount > 0;

        /// <summary>Returns the Rewired hardware GUID for a given console platform.</summary>
        public Guid GetControllerGuid(RuntimePlatform platform) => platform switch
        {
            RuntimePlatform.XboxOne             => s_gamepadTemplate,
            RuntimePlatform.GameCoreXboxSeries  => s_gamepadTemplate,
            RuntimePlatform.GameCoreXboxOne     => s_gamepadTemplate,
            RuntimePlatform.Switch              => s_nintendoSwitchProController,
            RuntimePlatform.PS5                 => s_sonyDualSenseTemplate,
            _                                   => default
        };

        /// <summary>Normalises the three Switch GUIDs to a single one used for glyph lookup.</summary>
        public Guid NormalizeSwitchControllerGuid(Guid guid)
        {
            if (guid == s_nintendoSwitchDual || guid == s_nintendoSwitchProController)
                return s_nintendoSwitchHandheld;
            return guid;
        }

        public bool GetControllerElementDataByController(Guid controllerId, string inputId, out int elementId)
        {
            elementId = -1;
            ControllerIdentifier identifier = new()
            {
                controllerId = -1,
                hardwareTypeGuid = controllerId,
                controllerType = ControllerType.Joystick,
            };
            ControllerMap map = ReInput.mapping.GetControllerMapInstance(identifier, DefaultCategoryName, DefaultLayoutName);
            InputAction action = ReInput.mapping.GetAction(inputId);
            for (int i = 0; i < map.ElementMaps.Count; i++)
            {
                if (map.ElementMaps[i].actionId != action.id) continue;
                elementId = map.ElementMaps[i].elementIdentifierId;
                return true;
            }
            return false;
        }

        public bool GetControllerElementDataByActiveController(string inputId,
            out Guid controllerId, out int elementId, out string elementName)
        {
            controllerId = Guid.Empty;
            elementId = -1;
            elementName = "";

            Player player = ReInput.players.GetPlayer(0);
            Controller active;
            if (player.controllers.joystickCount > 0)
                active = player.controllers.Joysticks[0];
            else if (player.controllers.customControllerCount > 0)
                active = player.controllers.CustomControllers[0];
            else
            {
                active = player.controllers.GetLastActiveController() ?? player.controllers.Keyboard;
            }

            controllerId = active.hardwareTypeGuid;
            InputAction action = ReInput.mapping.GetAction(inputId);
            if (action == null) return false;

            ActionElementMap aem = player.controllers.maps.GetFirstElementMapWithAction(active, action.id, true);
            if (aem == null) return false;

            if (controllerId == s_nintendoSwitchProController || controllerId == s_nintendoSwitchDual)
                controllerId = s_nintendoSwitchHandheld;

            elementId = aem.elementIdentifierId;
            elementName = aem.elementIdentifierName;
            return true;
        }

        public bool GetControllerElementDataByMouseKeyboard(string inputId,
            out Guid controllerId, out int elementId, out string elementName)
        {
            controllerId = Guid.Empty;
            elementId = -1;
            elementName = "";

            Player player = ReInput.players.GetPlayer(0);
            Controller active = player.controllers.Mouse;

            InputAction action = ReInput.mapping.GetAction(inputId);
            if (action == null)
            {
                Debug.LogError($"GlyphInputManager: action not found: {inputId}");
                return false;
            }

            ActionElementMap aem = player.controllers.maps.GetFirstElementMapWithAction(active, action.id, true);
            if (aem == null)
            {
                active = player.controllers.Keyboard;
                aem = player.controllers.maps.GetFirstElementMapWithAction(active, action.id, true);
            }
            if (aem == null) return false;

            controllerId = active.hardwareTypeGuid;
            if (controllerId == s_nintendoSwitchProController || controllerId == s_nintendoSwitchDual)
                controllerId = s_nintendoSwitchHandheld;

            elementId = aem.elementIdentifierId;
            elementName = aem.elementIdentifierName;
            return true;
        }

        public bool GetControllerElementDataByDefaultTemplate(string inputId, out Guid controllerId, out int elementId)
        {
            controllerId = s_gamepadTemplate;
            elementId = -1;

            InputAction action = ReInput.mapping.GetAction(inputId);
            if (action == null) return false;

            for (int i = 0; i < _defaultControllerTemplateMap.ElementMaps.Count; i++)
            {
                if (_defaultControllerTemplateMap.ElementMaps[i].actionId != action.id) continue;
                elementId = _defaultControllerTemplateMap.ElementMaps[i].elementIdentifierId;
                return true;
            }
            return false;
        }

        // ── Private ────────────────────────────────────────────────────────────

        private void OnActiveControllerChanged(Player player, Controller controller)
        {
            Controller last = _player.controllers.GetLastActiveController();
            if (last == null) return;
            if (last == _lastActiveController) return;

            _lastActiveController = last;

            bool isMouseOrKeyboard = last == _player.controllers.Mouse || last == _player.controllers.Keyboard;
            GameControlType = isMouseOrKeyboard ? GameControlType.MouseTouch : GameControlType.Gamepad;
            InputControllerState.IsUsingGamepad = GameControlType == GameControlType.Gamepad;

            Cursor.visible = isMouseOrKeyboard;
            ControllerChanged?.Invoke(GameControlType);
        }
    }
}
