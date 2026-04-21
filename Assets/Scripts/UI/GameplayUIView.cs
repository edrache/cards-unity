using CardsUnity.Controllers;
using CardsUnity.Data;
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

            hudView?.Bind(gameManager, roundController, combatController, rewardController);
            rewardView?.BindController(rewardController);
            BindInteractionEvents();
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

        private void BindInteractionEvents()
        {
            if (handView != null)
            {
                handView.OnCardDragStarted -= HandleCardDragStarted;
                handView.OnCardDragEnded -= HandleCardDragEnded;
                handView.OnCardDragStarted += HandleCardDragStarted;
                handView.OnCardDragEnded += HandleCardDragEnded;
            }

            if (boardView?.PlayerSlots == null) return;
            foreach (SlotView slot in boardView.PlayerSlots)
            {
                if (slot == null) continue;
                slot.OnCardDropped -= HandleCardDropped;
                slot.OnUnplaceRequested -= HandleUnplaceRequested;
                slot.OnCardDropped += HandleCardDropped;
                slot.OnUnplaceRequested += HandleUnplaceRequested;
            }

            if (boardView.EnemySlots == null) return;
            foreach (SlotView slot in boardView.EnemySlots)
            {
                if (slot == null) continue;
                slot.OnCardDropped -= HandleCardDropped;
                slot.OnCardDropped += HandleCardDropped;
            }
        }

        private void HandleCardDragStarted(CardView cardView)
        {
            SetPlayerSlotDropHints(true, cardView);
        }

        private void HandleCardDragEnded(CardView cardView)
        {
            SetPlayerSlotDropHints(false, cardView);
        }

        private void HandleCardDropped(SlotView slot, CardView cardView)
        {
            if (gameManager == null || roundController == null || slot == null || cardView?.Card == null)
                return;

            if (!CanPlaceInSlot(slot, cardView))
            {
                slot.SetDropHint(true, false);
                return;
            }

            roundController.PlaceCard(cardView.Card, slot.SlotIndex);
            ClearPlayerSlotDropHints();
        }

        private void HandleUnplaceRequested(SlotView slot)
        {
            if (gameManager == null || roundController == null || slot == null) return;
            if (gameManager.State.Phase != GamePhase.Placement) return;

            roundController.UnplaceCard(slot.SlotIndex);
        }

        private void SetPlayerSlotDropHints(bool active, CardView draggedCard)
        {
            if (boardView?.PlayerSlots == null) return;

            foreach (SlotView slot in boardView.PlayerSlots)
            {
                if (slot != null)
                    slot.SetDropHint(active, CanPlaceInSlot(slot, draggedCard));
            }

            if (boardView.EnemySlots == null) return;
            foreach (SlotView slot in boardView.EnemySlots)
            {
                if (slot != null)
                    slot.SetDropHint(active, false);
            }
        }

        private void ClearPlayerSlotDropHints()
        {
            if (boardView?.PlayerSlots == null) return;

            foreach (SlotView slot in boardView.PlayerSlots)
            {
                if (slot != null)
                    slot.SetDropHint(false, false);
            }

            if (boardView.EnemySlots == null) return;
            foreach (SlotView slot in boardView.EnemySlots)
            {
                if (slot != null)
                    slot.SetDropHint(false, false);
            }
        }

        private bool CanPlaceInSlot(SlotView slot, CardView cardView)
        {
            if (gameManager == null || slot == null || cardView?.Card == null)
                return false;
            if (gameManager.State.Phase != GamePhase.Placement)
                return false;
            if (slot.Owner != CardOwner.Player)
                return false;
            if (slot.Card != null)
                return false;

            return gameManager.State.PlayerHand.Contains(cardView.Card);
        }
    }
}
