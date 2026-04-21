using CardsUnity.Controllers;
using CardsUnity.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public sealed class HUDView : MonoBehaviour
    {
        [SerializeField] private Text phaseText;
        [SerializeField] private Text roundText;
        [SerializeField] private Text outcomeText;
        [SerializeField] private Button drawButton;
        [SerializeField] private Button resolveButton;
        [SerializeField] private Button continueButton;

        private GameManager _gameManager;
        private RoundController _roundController;
        private CombatController _combatController;

        public void Initialize(Text phase, Text round, Text outcome, Button draw, Button resolve, Button continueCombat)
        {
            phaseText = phase;
            roundText = round;
            outcomeText = outcome;
            drawButton = draw;
            resolveButton = resolve;
            continueButton = continueCombat;
        }

        public void Bind(GameManager gameManager, RoundController roundController, CombatController combatController)
        {
            _gameManager = gameManager;
            _roundController = roundController;
            _combatController = combatController;

            if (drawButton != null)
                drawButton.onClick.AddListener(DrawCards);
            if (resolveButton != null)
                resolveButton.onClick.AddListener(BeginCombat);
            if (continueButton != null)
                continueButton.onClick.AddListener(ContinueCombat);
        }

        public void Refresh(GameState state)
        {
            if (state == null) return;

            SetText(phaseText, $"Phase: {state.Phase}");
            SetText(roundText, $"Round: {state.Round}");
            SetText(outcomeText, GetOutcomeText(state));

            if (drawButton != null)
                drawButton.interactable = state.Phase == GamePhase.Draw;
            if (resolveButton != null)
                resolveButton.interactable = state.Phase == GamePhase.Placement && _roundController != null && _roundController.CanResolve();
            if (continueButton != null)
                continueButton.interactable = state.Phase == GamePhase.Combat;
        }

        private void DrawCards()
        {
            if (_gameManager != null && _gameManager.State.Phase == GamePhase.Draw)
                _roundController?.DrawCards();
        }

        private void BeginCombat()
        {
            if (_gameManager == null || _gameManager.State.Phase != GamePhase.Placement) return;
            if (_roundController == null || !_roundController.CanResolve()) return;

            _combatController?.PrepareResolveRound();
            _combatController?.ResolveCombatStep();
        }

        private void ContinueCombat()
        {
            if (_gameManager == null || _gameManager.State.Phase != GamePhase.Combat || _combatController == null) return;

            if (_combatController.HasPendingSteps)
                _combatController.ResolveCombatStep();
            else
                _combatController.FinalizeResolveRound();
        }

        private static string GetOutcomeText(GameState state)
        {
            if (state.Phase == GamePhase.End)
                return "Game over";
            if (state.Phase == GamePhase.Reward)
                return "Choose a reward";
            return string.Empty;
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
                target.text = value;
        }
    }
}
