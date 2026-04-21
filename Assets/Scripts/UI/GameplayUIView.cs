using CardsUnity.Controllers;
using CardsUnity.Combat;
using CardsUnity.Config;
using CardsUnity.Data;
using UnityEngine;
using UnityEngine.Events;

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
        [SerializeField] private CombatResultView combatResultView;
        [SerializeField] private TooltipView tooltipView;
        [SerializeField] private AnimConfig animConfig;
        [SerializeField] private UnityEvent hitFeedback;
        [SerializeField] private UnityEvent defeatFeedback;
        [SerializeField] private UnityEvent rewardFeedback;
        [SerializeField] private UnityEvent gameOverFeedback;

        private GamePhase? _lastObservedPhase;

        public void Initialize(
            GameManager manager,
            RoundController round,
            CombatController combat,
            RewardController reward,
            BoardView board,
            HandView hand,
            HUDView hud,
            DeckPanelView deckPanel,
            RewardView rewards,
            CombatResultView combatResults = null,
            TooltipView tooltip = null,
            AnimConfig animationConfig = null)
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
            combatResultView = combatResults;
            tooltipView = tooltip;
            animConfig = animationConfig;

            hudView?.Bind(gameManager, roundController, combatController, rewardController);
            rewardView?.BindController(rewardController);
            BindInteractionEvents();
            BindCombatEvents();
            Subscribe();
            Refresh();
        }

        private void OnEnable()
        {
            Subscribe();
            BindCombatEvents();
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
            if (combatController != null)
                combatController.OnCombatResult -= HandleCombatResult;
        }

        public void Refresh()
        {
            if (gameManager == null || gameManager.State == null) return;

            boardView?.Bind(gameManager.State);
            boardView?.BindPreviewBadges(gameManager.State);
            handView?.Bind(gameManager.State.PlayerHand);
            hudView?.Refresh(gameManager.State);
            deckPanelView?.Bind(gameManager.State);
            rewardView?.Bind(gameManager.State);

            if (gameManager.State.Phase != GamePhase.Combat)
            {
                combatResultView?.Clear();
                PlayCleanupAnimations();
            }
            if (gameManager.State.Phase != GamePhase.Placement)
                tooltipView?.Hide();

            InvokePhaseFeedback(gameManager.State.Phase);
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
                slot.OnHoverChanged -= HandleSlotHoverChanged;
                slot.OnHoverChanged += HandleSlotHoverChanged;
            }

            if (boardView.EnemySlots == null) return;
            foreach (SlotView slot in boardView.EnemySlots)
            {
                if (slot == null) continue;
                slot.OnCardDropped -= HandleCardDropped;
                slot.OnCardDropped += HandleCardDropped;
                slot.OnHoverChanged -= HandleSlotHoverChanged;
                slot.OnHoverChanged += HandleSlotHoverChanged;
            }
        }

        private void BindCombatEvents()
        {
            if (combatController == null) return;

            combatController.OnCombatResult -= HandleCombatResult;
            combatController.OnCombatResult += HandleCombatResult;
        }

        private void HandleCombatResult(CombatResult result)
        {
            combatResultView?.Show(result);
            PlayCombatResultAnimation(result);
            if (result != null && (result.PlayerDamageDealt > 0 || result.EnemyDamageDealt > 0))
                hitFeedback?.Invoke();
            if (result != null && (result.PlayerDied || result.EnemyDied))
                defeatFeedback?.Invoke();
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
            slot.CardView?.PlaySnap();
        }

        private void HandleUnplaceRequested(SlotView slot)
        {
            if (gameManager == null || roundController == null || slot == null) return;
            if (gameManager.State.Phase != GamePhase.Placement) return;

            roundController.UnplaceCard(slot.SlotIndex);
        }

        private void HandleSlotHoverChanged(SlotView slot, bool hovering)
        {
            if (!hovering)
            {
                tooltipView?.Hide();
                return;
            }

            ShowPreviewForSlot(slot);
        }

        private void ShowPreviewForSlot(SlotView slot)
        {
            if (tooltipView == null || gameManager == null || slot == null)
                return;
            if (gameManager.State.Phase != GamePhase.Placement)
            {
                tooltipView.Hide();
                return;
            }

            int index = slot.SlotIndex;
            if (index < 0 || index >= gameManager.State.PlayerBoard.Length || index >= gameManager.State.EnemyBoard.Length)
            {
                tooltipView.Hide();
                return;
            }

            CardInstance playerCard = gameManager.State.PlayerBoard[index];
            CardInstance enemyCard = gameManager.State.EnemyBoard[index];
            if (playerCard == null || enemyCard == null)
            {
                tooltipView.Hide();
                return;
            }

            CombatPreview preview = CombatResolver.GetCombatPreview(
                playerCard,
                enemyCard,
                gameManager.State.PlayerBoard,
                gameManager.State.EnemyBoard,
                index);

            tooltipView.ShowPreview(index, playerCard, enemyCard, preview);
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

        private void PlayCombatResultAnimation(CombatResult result)
        {
            if (result == null || boardView == null) return;

            int slotIndex = result.SlotIndex;
            if (boardView.PlayerSlots != null && slotIndex >= 0 && slotIndex < boardView.PlayerSlots.Length)
                boardView.PlayerSlots[slotIndex]?.CardView?.PlayCombatReveal(result.PlayerDied, -1f);

            if (boardView.EnemySlots != null && slotIndex >= 0 && slotIndex < boardView.EnemySlots.Length)
                boardView.EnemySlots[slotIndex]?.CardView?.PlayCombatReveal(result.EnemyDied, 1f);
        }

        private void PlayCleanupAnimations()
        {
            if (boardView == null) return;

            PlayCleanupOnSlots(boardView.PlayerSlots);
            PlayCleanupOnSlots(boardView.EnemySlots);
        }

        private static void PlayCleanupOnSlots(SlotView[] slots)
        {
            if (slots == null) return;

            foreach (SlotView slot in slots)
                slot?.CardView?.PlayCleanup();
        }

        private void InvokePhaseFeedback(GamePhase phase)
        {
            if (_lastObservedPhase == phase) return;
            _lastObservedPhase = phase;

            if (phase == GamePhase.Reward)
                rewardFeedback?.Invoke();
            if (phase == GamePhase.End)
                gameOverFeedback?.Invoke();
        }
    }
}
