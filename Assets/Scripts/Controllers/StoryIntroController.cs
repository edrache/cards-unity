using System.Collections;
using System.Collections.Generic;
using Rewired;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CardsUnity.Controllers
{
    /// <summary>Owns the one-shot story gate and the unscaled reveal into gameplay.</summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class StoryIntroController : MonoBehaviour
    {
        [SerializeField] private StorySequencePlayer sequencePlayer;
        [SerializeField] private GameObject playerRoot;
        [SerializeField] private HandheldTorch playerTorch;
        [SerializeField] private CanvasGroup blackout;

        [Header("Reveal")]
        [SerializeField, Min(0f)] private float transitionDuration = 1.25f;
        [SerializeField, Range(0.1f, 0.95f)] private float stickPressThreshold = 0.55f;
        [SerializeField] private string rewiredPlayerName = "Player0";

        private readonly List<RendererState> playerRenderers = new();
        private readonly List<BehaviourState> playerBehaviours = new();
        private float previousTimeScale;
        private bool ownsTimeScale;
        private bool stickWasPressed;
        private bool transitionStarted;
        private bool introInitialized;
        private Coroutine transition;

        private void Awake()
        {
            if (!enabled) return;
            InitializeIntro();
        }

        private void InitializeIntro()
        {
            if (introInitialized || transitionStarted) return;
            CacheAndHidePlayer();
            ShowBlackout();
            if (playerTorch != null) playerTorch.SetPresentationMultiplier(0f);

            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            ownsTimeScale = true;
            introInitialized = true;
        }

        private void OnEnable()
        {
            if (!introInitialized && !transitionStarted) InitializeIntro();
            if (!introInitialized || transitionStarted || sequencePlayer == null) return;
            sequencePlayer.Completed += HandleSequenceCompleted;
        }

        private void Start()
        {
            if (!introInitialized) return;
            // Reassert these after all scene objects have initialized so the first rendered frame
            // cannot briefly expose the player or an already-lit torch.
            SetPlayerVisible(false);
            ShowBlackout();
            if (playerTorch != null) playerTorch.SetPresentationMultiplier(0f);

            if (sequencePlayer == null)
            {
                HandleSequenceCompleted();
                return;
            }

            sequencePlayer.Begin();
            if (!sequencePlayer.IsPlaying) HandleSequenceCompleted();
        }

        private void Update()
        {
            bool stickPressed = IsStickPressed();
            bool pressedThisFrame = AnyButtonPressedThisFrame() || RewiredButtonPressedThisFrame()
                || (stickPressed && !stickWasPressed);
            stickWasPressed = stickPressed;

            if (!transitionStarted && sequencePlayer != null && sequencePlayer.IsPlaying && pressedThisFrame)
                sequencePlayer.Advance();
        }

        private void LateUpdate()
        {
            if (!transitionStarted && playerTorch != null)
                playerTorch.SetPresentationMultiplier(0f);
        }

        private void HandleSequenceCompleted()
        {
            if (transitionStarted) return;
            transitionStarted = true;
            transition = StartCoroutine(RevealGameplay());
        }

        private IEnumerator RevealGameplay()
        {
            SetPlayerVisible(true);
            float elapsed = 0f;
            do
            {
                elapsed += Time.unscaledDeltaTime;
                float t = transitionDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / transitionDuration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                if (blackout != null) blackout.alpha = 1f - eased;
                if (playerTorch != null) playerTorch.SetPresentationMultiplier(eased);
                yield return null;
            }
            while (elapsed < transitionDuration);

            if (blackout != null)
            {
                blackout.alpha = 0f;
                blackout.blocksRaycasts = false;
                blackout.interactable = false;
            }
            if (playerTorch != null) playerTorch.SetPresentationMultiplier(1f);

            // Do not hand the press that dismissed the final page to attacks, inventory or menus.
            yield return null;
            while (AnyControlIsPressed()) yield return null;

            RestorePlayerBehaviours();
            RestoreTimeScale();
            playerRenderers.Clear();
            transition = null;
            enabled = false;
        }

        private void CacheAndHidePlayer()
        {
            if (playerRoot == null) return;

            foreach (Renderer renderer in playerRoot.GetComponentsInChildren<Renderer>(true))
            {
                playerRenderers.Add(new RendererState(renderer, renderer.enabled));
                renderer.enabled = false;
            }

            foreach (MonoBehaviour behaviour in playerRoot.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour == this || behaviour == playerTorch) continue;
                playerBehaviours.Add(new BehaviourState(behaviour, behaviour.enabled));
                behaviour.enabled = false;
            }
        }

        private void SetPlayerVisible(bool visible)
        {
            foreach (RendererState state in playerRenderers)
                if (state.Renderer != null) state.Renderer.enabled = visible && state.WasEnabled;
        }

        private void RestorePlayerBehaviours()
        {
            foreach (BehaviourState state in playerBehaviours)
                if (state.Behaviour != null) state.Behaviour.enabled = state.WasEnabled;
            playerBehaviours.Clear();
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

        private bool AnyControlIsPressed()
        {
            if (IsStickPressed()) return true;
            Rewired.Player player = GetRewiredPlayer();
            if (player != null && player.GetAnyButton()) return true;
            foreach (InputDevice device in InputSystem.devices)
                foreach (InputControl control in device.allControls)
                    if (control is ButtonControl button && button.isPressed) return true;
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

        private void RestoreTimeScale()
        {
            if (!ownsTimeScale) return;
            Time.timeScale = previousTimeScale;
            ownsTimeScale = false;
        }

        private void ShowBlackout()
        {
            if (blackout == null) return;
            blackout.alpha = 1f;
            blackout.blocksRaycasts = true;
            blackout.interactable = true;
        }

        private void OnDisable()
        {
            if (sequencePlayer != null) sequencePlayer.Completed -= HandleSequenceCompleted;
            if (!introInitialized) return;
            if (sequencePlayer != null) sequencePlayer.Stop();
            if (transition != null) StopCoroutine(transition);
            transition = null;
            SetPlayerVisible(true);
            RestorePlayerBehaviours();
            if (playerTorch != null) playerTorch.SetPresentationMultiplier(1f);
            if (blackout != null)
            {
                blackout.alpha = 0f;
                blackout.blocksRaycasts = false;
                blackout.interactable = false;
            }
            RestoreTimeScale();
        }

        private readonly struct RendererState
        {
            public RendererState(Renderer renderer, bool wasEnabled)
            {
                Renderer = renderer;
                WasEnabled = wasEnabled;
            }

            public Renderer Renderer { get; }
            public bool WasEnabled { get; }
        }

        private readonly struct BehaviourState
        {
            public BehaviourState(Behaviour behaviour, bool wasEnabled)
            {
                Behaviour = behaviour;
                WasEnabled = wasEnabled;
            }

            public Behaviour Behaviour { get; }
            public bool WasEnabled { get; }
        }
    }
}
