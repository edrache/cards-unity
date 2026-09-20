using Rewired;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CardsUnity.Controllers
{
    /// <summary>Walks the entrance during narration; room arrival independently ends journey safety.</summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class StoryIntroController : MonoBehaviour
    {
        [SerializeField] private StorySequencePlayer sequencePlayer;
        [SerializeField] private GameObject playerRoot;
        [SerializeField] private HandheldTorch playerTorch;
        [Tooltip("Legacy overlay. Kept transparent; an authored disabled Image remains disabled.")]
        [SerializeField] private CanvasGroup blackout;
        [SerializeField] private ProceduralCave cave;
        [SerializeField] private IntroJourneyProtection journeyProtection;
        [SerializeField] private StoryVeilDissolve veil;
        [Tooltip("Gameplay HUD groups hidden while narration owns movement.")]
        [SerializeField] private CanvasGroup[] gameplayUI = System.Array.Empty<CanvasGroup>();

        [Header("Entrance walk")]
        [SerializeField, Min(0.1f)] private float autoWalkSpeed = 1.15f;
        [Tooltip("Metres of tunnel before the first room reserved for player-controlled movement.")]
        [SerializeField, Min(2f)] private float manualApproachDistance = 18f;
        [SerializeField, Range(0.2f, 3f)] private float pathLookAhead = 0.9f;

        [Header("Final line reveal")]
        [SerializeField, Min(0f)] private float transitionDuration = 2.8f;
        [SerializeField, Range(0.1f, 0.95f)] private float stickPressThreshold = 0.55f;
        [SerializeField] private string rewiredPlayerName = "Player0";

        private ProceduralCharacter character;
        private CardsUnity.Rendering.PlayerOcclusionOutline playerOutline;
        private bool outlineWasEnabled;
        private float[] hudAlphas;
        private bool[] hudRaycasts;
        private bool[] hudInteractions;
        private bool initialized;
        private bool routeReady;
        private bool controlReleased;
        private bool narrationCompleted;
        private bool revealFinished;
        private bool protectionReleased;
        private bool stickWasPressed;
        private float revealElapsed;
        private float automaticTravelLimit;

        public bool HasReleasedControl => controlReleased;
        public bool IsAutoWalking => initialized && !controlReleased;
        public bool IsJourneyProtected => journeyProtection != null && journeyProtection.IsProtectionActive;
        public float AutomaticTravelLimit => automaticTravelLimit;

        private void Awake()
        {
            if (!enabled)
            {
                veil?.SetProgress(1f);
                return;
            }
            character = playerRoot != null ? playerRoot.GetComponent<ProceduralCharacter>() : null;
            playerOutline = playerRoot != null
                ? playerRoot.GetComponent<CardsUnity.Rendering.PlayerOcclusionOutline>() : null;
            if (playerOutline != null)
            {
                outlineWasEnabled = playerOutline.enabled;
                playerOutline.enabled = false;
            }
            if (journeyProtection == null && playerRoot != null)
                journeyProtection = playerRoot.GetComponent<IntroJourneyProtection>();
            initialized = true;
            character?.SetScriptedMove(Vector3.zero, 0f);
            journeyProtection?.BeginProtection();
            playerTorch?.SetPresentationMultiplier(1f);
            veil?.SetProgress(0f);
            SetLegacyOverlayTransparent();
            CacheAndHideHud();
        }

        private void OnEnable()
        {
            if (sequencePlayer == null) return;
            sequencePlayer.LineStarted += HandleLineStarted;
            sequencePlayer.Completed += HandleSequenceCompleted;
        }

        private void Start()
        {
            if (!initialized) return;
            if (character == null || cave == null || journeyProtection == null)
            {
                Debug.LogWarning("Story intro requires a character, cave and journey protection.", this);
                enabled = false;
                return;
            }
            // Cave rebuild normally runs in Update. Build once now so the very first
            // scripted movement uses the current profile and its actual spawn/route.
            cave.Rebuild();
            routeReady = cave.EntranceRouteWorldPoints.Count > 1;
            if (!routeReady)
            {
                Debug.LogWarning("Story intro has no generated entrance route.", this);
                enabled = false;
                return;
            }
            automaticTravelLimit = FindAutomaticTravelLimit();
            if (sequencePlayer != null) sequencePlayer.Begin();
            else HandleSequenceCompleted();
        }

        private void Update()
        {
            if (!initialized || !routeReady) return;
            bool stickPressed = IsStickPressed();
            bool pressed = AnyButtonPressedThisFrame() || RewiredButtonPressedThisFrame()
                || (stickPressed && !stickWasPressed);
            stickWasPressed = stickPressed;
            if (!controlReleased && pressed && sequencePlayer != null && sequencePlayer.IsPlaying)
                sequencePlayer.Advance();

            if (!controlReleased) DriveEntranceWalk();
            else UpdateReveal();

            if (!protectionReleased && HasEnteredFirstRoom())
            {
                journeyProtection.ReleaseProtection();
                protectionReleased = true;
            }
            if (controlReleased && revealFinished && narrationCompleted && protectionReleased)
                enabled = false;
        }

        private bool HasEnteredFirstRoom()
        {
            // Normally this is the entrance chamber. A branch intersection can let
            // the player choose another chamber; it must also end the one-time safety.
            for (int room = 0; room < cave.RoomCenters.Count; room++)
                if (cave.IsInRoom(playerRoot.transform.position, room)) return true;
            return false;
        }

        private float FindAutomaticTravelLimit()
        {
            float length = cave.EntranceRouteLength;
            float firstRoomDistance = length;
            for (float distance = 0f; distance <= length; distance += 0.5f)
            {
                if (!cave.HasReachedEntranceRoom(cave.SampleEntranceRoute(distance, out _))) continue;
                firstRoomDistance = distance;
                break;
            }
            return Mathf.Max(0f, firstRoomDistance - manualApproachDistance);
        }

        private void DriveEntranceWalk()
        {
            var points = cave.EntranceRouteWorldPoints;
            Vector3 position = playerRoot.transform.position;
            float nearestDistance = float.PositiveInfinity;
            float along = 0f, cumulative = 0f;
            for (int index = 1; index < points.Count; index++)
            {
                Vector3 segment = points[index] - points[index - 1];
                float length = segment.magnitude;
                float t = length > 0.0001f
                    ? Mathf.Clamp01(Vector3.Dot(position - points[index - 1], segment) / (length * length)) : 0f;
                float separation = (position - (points[index - 1] + segment * t)).sqrMagnitude;
                if (separation < nearestDistance)
                {
                    nearestDistance = separation;
                    along = cumulative + length * t;
                }
                cumulative += length;
            }
            Vector3 target = cave.SampleEntranceRoute(Mathf.Min(automaticTravelLimit, along + pathLookAhead), out _);
            Vector3 direction = Vector3.ProjectOnPlane(target - position, Vector3.up);
            bool arrived = along >= automaticTravelLimit - 0.2f && direction.magnitude < 0.3f;
            character.SetScriptedMove(arrived ? Vector3.zero : direction,
                arrived ? 0f : Mathf.Min(autoWalkSpeed, direction.magnitude / Mathf.Max(Time.deltaTime, 0.001f)));
        }

        private void HandleLineStarted(int index)
        {
            if (sequencePlayer != null && sequencePlayer.IsLastLine) ReleaseControl();
        }

        private void HandleSequenceCompleted()
        {
            narrationCompleted = true;
            ReleaseControl();
        }

        private void ReleaseControl()
        {
            if (controlReleased || !initialized) return;
            controlReleased = true;
            character?.ClearScriptedMove();
            revealElapsed = 0f;
            if (transitionDuration <= 0f) UpdateReveal();
        }

        private void UpdateReveal()
        {
            if (revealFinished) return;
            revealElapsed += Time.unscaledDeltaTime;
            float t = transitionDuration <= 0f ? 1f : Mathf.Clamp01(revealElapsed / transitionDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            veil?.SetProgress(eased);
            RestoreHud(eased);
            revealFinished = t >= 1f;
            if (revealFinished && playerOutline != null) playerOutline.enabled = outlineWasEnabled;
        }

        private void CacheAndHideHud()
        {
            int count = gameplayUI.Length;
            hudAlphas = new float[count];
            hudRaycasts = new bool[count];
            hudInteractions = new bool[count];
            for (int index = 0; index < count; index++)
            {
                var group = gameplayUI[index];
                if (group == null) continue;
                hudAlphas[index] = group.alpha;
                hudRaycasts[index] = group.blocksRaycasts;
                hudInteractions[index] = group.interactable;
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

        private void RestoreHud(float progress)
        {
            if (hudAlphas == null) return;
            for (int index = 0; index < gameplayUI.Length; index++)
            {
                var group = gameplayUI[index];
                if (group == null) continue;
                group.alpha = hudAlphas[index] * progress;
                group.blocksRaycasts = progress >= 1f && hudRaycasts[index];
                group.interactable = progress >= 1f && hudInteractions[index];
            }
        }

        private void SetLegacyOverlayTransparent()
        {
            if (blackout == null) return;
            blackout.alpha = 0f;
            blackout.blocksRaycasts = false;
            blackout.interactable = false;
        }

        private bool AnyButtonPressedThisFrame()
        {
            foreach (InputDevice device in InputSystem.devices)
                foreach (InputControl control in device.allControls)
                    if (control is ButtonControl button && button.wasPressedThisFrame) return true;
            return false;
        }

        private bool IsStickPressed()
        {
            float thresholdSquared = stickPressThreshold * stickPressThreshold;
            foreach (Gamepad gamepad in Gamepad.all)
                if (gamepad.leftStick.ReadValue().sqrMagnitude >= thresholdSquared
                    || gamepad.rightStick.ReadValue().sqrMagnitude >= thresholdSquared) return true;
            foreach (UnityEngine.InputSystem.Joystick joystick in UnityEngine.InputSystem.Joystick.all)
                if (joystick.stick.ReadValue().sqrMagnitude >= thresholdSquared) return true;
            Rewired.Player player = GetRewiredPlayer();
            if (player != null)
            {
                float x = player.GetAxis("MoveHorizontal");
                float y = player.GetAxis("MoveVertical");
                if (x * x + y * y >= thresholdSquared) return true;
            }
            return false;
        }

        private bool RewiredButtonPressedThisFrame()
        {
            Rewired.Player player = GetRewiredPlayer();
            return player != null && player.GetAnyButtonDown();
        }

        private Rewired.Player GetRewiredPlayer()
        {
            if (!ReInput.isReady || string.IsNullOrEmpty(rewiredPlayerName)) return null;
            return ReInput.players.GetPlayer(rewiredPlayerName);
        }

        private void OnDisable()
        {
            if (sequencePlayer != null)
            {
                sequencePlayer.LineStarted -= HandleLineStarted;
                sequencePlayer.Completed -= HandleSequenceCompleted;
            }
            if (!initialized) return;
            sequencePlayer?.Stop();
            if (!controlReleased) character?.ClearScriptedMove();
            journeyProtection?.ReleaseProtection();
            veil?.SetProgress(1f);
            if (playerOutline != null) playerOutline.enabled = outlineWasEnabled;
            RestoreHud(1f);
            SetLegacyOverlayTransparent();
        }
    }
}
