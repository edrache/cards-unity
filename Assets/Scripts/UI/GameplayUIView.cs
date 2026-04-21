using CardsUnity.Controllers;
using UnityEngine;

namespace CardsUnity.UI
{
    public sealed class GameplayUIView : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private RoundController roundController;
        [SerializeField] private CombatController combatController;
        [SerializeField] private RewardController rewardController;
        [SerializeField] private BoardView boardView;
        [SerializeField] private HandView handView;
        [SerializeField] private HUDView hudView;
        [SerializeField] private DeckPanelView deckPanelView;
        [SerializeField] private RewardView rewardView;

        public void Initialize(
            GameManager manager,
            RoundController round,
            CombatController combat,
            RewardController reward,
            BoardView board,
            HandView hand,
            HUDView hud,
            DeckPanelView deckPanel,
            RewardView rewards)
        {
            gameManager = manager;
            roundController = round;
            combatController = combat;
            rewardController = reward;
            boardView = board;
            handView = hand;
            hudView = hud;
            deckPanelView = deckPanel;
            rewardView = rewards;

            hudView?.Bind(gameManager, roundController, combatController);
            rewardView?.BindController(rewardController);
            Subscribe();
            Refresh();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (gameManager == null) return;
            gameManager.OnStateChanged -= Refresh;
            gameManager.OnStateChanged += Refresh;
        }

        private void Unsubscribe()
        {
            if (gameManager != null)
                gameManager.OnStateChanged -= Refresh;
        }

        public void Refresh()
        {
            if (gameManager == null || gameManager.State == null) return;

            boardView?.Bind(gameManager.State);
            handView?.Bind(gameManager.State.PlayerHand);
            hudView?.Refresh(gameManager.State);
            deckPanelView?.Bind(gameManager.State);
            rewardView?.Bind(gameManager.State);
        }
    }
}
