using System;
using System.Collections.Generic;
using CardsUnity.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardsUnity
{
    public class ChallengeController : MonoBehaviour
    {
        [SerializeField] private GameConfig config;
        [SerializeField] private BoardView boardView;
        [SerializeField] private HandView handView;
        [SerializeField] private ClockView opponentClockView;
        [SerializeField] private ThreatBarView forceThreatBarView;
        [SerializeField] private ThreatBarView witThreatBarView;
        [SerializeField] private ThreatBarView presenceThreatBarView;
        [SerializeField] private ProgressionView progressionView;
        [SerializeField] private StatusView statusView;
        [SerializeField] private PlayedCardCounterView playedCardCounterView;
        [SerializeField] private EndTurnButton endTurnButton;
        [SerializeField] private GameObject winPanel;
        [SerializeField] private GameObject losePanel;
        [SerializeField] private StoryDeckDefinition storyDeck;
        [SerializeField] private StoryCardView storyCardView;
        [SerializeField] private NpcCardView npcCardView;

        private TurnController _turn;
        private HandState _hand;
        private BoardState _board;
        private ClockState _opponentClock;
        private ThreatState _threatState;
        private ProgressionState _progression;
        private PlayedCardCounterState _playedCardCounters;
        private StatusCollection _statusCollection;
        private System.Random _rng;
        private StoryDeckState _storyDeck;
        private NpcDeckState _npcDeck;
        private Action<CardView> _cardDragStartHandler;
        private Action<CardView> _cardDragEndHandler;

        private void Start()
        {
            if (config == null || boardView == null || handView == null ||
                opponentClockView == null || endTurnButton == null)
            {
                Debug.LogError("ChallengeController is missing required references.");
                enabled = false;
                return;
            }

            _rng = new System.Random();

            var playerDeck = BuildDeck(config.startingDeck, _rng);
            var opponentDeck = BuildDeck(config.opponentDeck, _rng);

            _hand = new HandState();
            var slotDefs = ResolveSlotDefinitions();
            var slotActiveStates = ResolveSlotActiveStates(slotDefs);
            _board = slotDefs != null
                ? new BoardState(slotDefs, slotActiveStates)
                : new BoardState(config.boardSlotCount);
            _opponentClock = new ClockState(config.opponentDeck != null ? config.opponentDeck.Count : 0);
            _threatState = BuildThreatState();
            _progression = new ProgressionState(config.progressionXpCap);
            _playedCardCounters = new PlayedCardCounterState();
            _statusCollection = new StatusCollection();

            if (storyDeck != null)
                _storyDeck = new StoryDeckState(storyDeck, _rng);

            if (config.npcDeck != null)
                _npcDeck = new NpcDeckState(config.npcDeck, _rng);

            _turn = new TurnController(
                playerDeck,
                _hand,
                opponentDeck,
                _board,
                _threatState,
                _opponentClock,
                _progression,
                _playedCardCounters,
                _statusCollection,
                _npcDeck,
                config.draftValue,
                _rng);

            _cardDragStartHandler = _ => boardView.SetHighlightAll(true);
            _cardDragEndHandler = _ => boardView.SetHighlightAll(false);

            boardView.OnCardPlayed += OnCardPlayed;
            handView.OnCardDragStart += _cardDragStartHandler;
            handView.OnCardDragEnd += _cardDragEndHandler;
            endTurnButton.OnClicked += OnEndTurn;
            _turn.OnOpponentCardDestroyed += HandleOpponentCardDestroyed;
            _turn.OnPlayerCardDestroyed += HandlePlayerCardDestroyed;
            _turn.OnNpcCardDrawn += HandleNpcCardDrawn;
            _threatState.OnTierDepleted += HandleThreatTierDepleted;
            _threatState.OnBarAtZero += HandleThreatBarAtZero;
            _progression.OnLevelUp += HandleLevelUp;

            if (winPanel != null)
                winPanel.SetActive(false);

            if (losePanel != null)
                losePanel.SetActive(false);

            // ConfigureSlots must run before BeginTurn so slot labels are ready before the board populates
            if (slotDefs != null)
                boardView.ConfigureSlots(slotDefs);

            forceThreatBarView?.Build(_threatState.GetBar(CardType.Force));
            witThreatBarView?.Build(_threatState.GetBar(CardType.Wit));
            presenceThreatBarView?.Build(_threatState.GetBar(CardType.Presence));

            BeginTurn();
            RefreshUI();
            CheckEndCondition();
        }

        private void OnDestroy()
        {
            if (boardView != null)
                boardView.OnCardPlayed -= OnCardPlayed;

            if (handView != null && _cardDragStartHandler != null)
                handView.OnCardDragStart -= _cardDragStartHandler;

            if (handView != null && _cardDragEndHandler != null)
                handView.OnCardDragEnd -= _cardDragEndHandler;

            if (endTurnButton != null)
                endTurnButton.OnClicked -= OnEndTurn;

            if (_turn != null)
            {
                _turn.OnOpponentCardDestroyed -= HandleOpponentCardDestroyed;
                _turn.OnPlayerCardDestroyed -= HandlePlayerCardDestroyed;
                _turn.OnNpcCardDrawn -= HandleNpcCardDrawn;
            }

            if (_threatState != null)
            {
                _threatState.OnTierDepleted -= HandleThreatTierDepleted;
                _threatState.OnBarAtZero -= HandleThreatBarAtZero;
            }

            if (_progression != null)
                _progression.OnLevelUp -= HandleLevelUp;
        }

        private ThreatState BuildThreatState()
        {
            return new ThreatState(
                EnsureThreatTierList(config.GetThreatTiers(CardType.Force)),
                EnsureThreatTierList(config.GetThreatTiers(CardType.Wit)),
                EnsureThreatTierList(config.GetThreatTiers(CardType.Presence)));
        }

        private static List<ThreatTierDefinition> EnsureThreatTierList(List<ThreatTierDefinition> tiers)
        {
            if (tiers != null && tiers.Count > 0)
                return tiers;

            var fallback = ScriptableObject.CreateInstance<ThreatTierDefinition>();
            fallback.maxValue = 5;
            return new List<ThreatTierDefinition> { fallback };
        }

        private void HandleThreatTierDepleted(CardType type, int tierIndex)
        {
            var tiers = config.GetThreatTiers(type);
            if (tiers == null || tierIndex < 0 || tierIndex >= tiers.Count)
                return;

            var tierDefinition = tiers[tierIndex];
            if (tierDefinition == null)
                return;

            Debug.Log(
                $"[ChallengeController] Threat tier depleted. Type={type}, TierIndex={tierIndex}, " +
                $"PenaltySlot={(tierDefinition.penaltySlot != null ? tierDefinition.penaltySlot.name : "none")}, " +
                $"PenaltyStatus={(tierDefinition.penaltyStatus != null ? tierDefinition.penaltyStatus.statusName : "none")}");

            if (tierDefinition.penaltySlot != null)
                AddRuntimeStorySlot(tierDefinition.penaltySlot);

            if (tierDefinition.penaltyStatus != null)
                _statusCollection?.Add(tierDefinition.penaltyStatus);

            RefreshUI();
        }

        private void HandleThreatBarAtZero(CardType type)
        {
            CheckEndCondition();
        }

        private void HandleLevelUp(StoryEffectResolutionKey dominantType)
        {
            CardDefinition rewardCard = null;
            if (config.progressionRewardDeckSet != null)
            {
                rewardCard = config.progressionRewardDeckSet.GetRewardCard(dominantType, _rng);
                if (rewardCard != null)
                {
                    _turn.AddCardsToDeck(
                        new[] { rewardCard },
                        StoryEffectDeckTarget.Player,
                        StoryEffectDeckPlacement.DiscardPile);
                }
            }

            Debug.Log(
                $"[ChallengeController] Level up resolved. DominantType={dominantType}, Tier={_progression.CurrentTier}, " +
                $"RewardCard={(rewardCard != null ? rewardCard.title : "none")}");
            RefreshUI();
        }

        private void HandleNpcCardDrawn(NpcCardDefinition card)
        {
            if (card?.effects != null)
            {
                foreach (var effect in card.effects)
                {
                    if (effect != null)
                        ApplyNpcEffect(effect);
                }
            }

            RefreshUI();
        }

        private void ApplyNpcEffect(NpcEffect effect)
        {
            switch (effect.action)
            {
                case NpcEffectAction.AddCardToPlayerHand:
                    if (effect.cardToAdd != null)
                        _hand.Add(new CardInstance(effect.cardToAdd));
                    break;
                case NpcEffectAction.DamageToThreat:
                    _threatState?.GetBar(effect.targetCardType)?.TakeDamage(effect.actionValue);
                    break;
                case NpcEffectAction.IncrementOpponentClock:
                    _opponentClock?.Increment(effect.actionValue);
                    break;
            }
        }

        private static DeckState BuildDeck(List<CardDefinition> cards, System.Random rng)
        {
            var deck = new DeckState();
            var shuffledCards = cards != null ? new List<CardDefinition>(cards) : new List<CardDefinition>();

            if (rng != null)
            {
                for (int i = shuffledCards.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (shuffledCards[i], shuffledCards[j]) = (shuffledCards[j], shuffledCards[i]);
                }
            }

            deck.Initialize(shuffledCards);
            return deck;
        }

        private SlotDefinition[] ResolveSlotDefinitions()
        {
            if (boardView != null && boardView.HasAnySceneSlotDefinitions())
                return boardView.GetSceneSlotDefinitions();

            return config.slotDefinitions != null && config.slotDefinitions.Count > 0
                ? config.slotDefinitions.ToArray()
                : null;
        }

        private bool[] ResolveSlotActiveStates(SlotDefinition[] slotDefinitions)
        {
            if (slotDefinitions == null)
                return null;

            if (boardView != null)
            {
                var sceneActiveStates = boardView.GetSceneSlotActiveStates();
                if (sceneActiveStates != null && sceneActiveStates.Length == slotDefinitions.Length)
                    return sceneActiveStates;
            }

            var activeStates = new bool[slotDefinitions.Length];
            for (int i = 0; i < activeStates.Length; i++)
                activeStates[i] = true;

            return activeStates;
        }

        private void BeginTurn()
        {
            _turn.StartTurn();
            RefreshUI();
        }

        private void OnCardPlayed(CardInstance card, int slotIndex)
        {
            _turn.PlayCard(card, slotIndex);

            if (_turn.ConsumeTurnEndedByEffect())
            {
                RefreshUI();
                CheckEndCondition();

                if (!IsGameOver())
                    BeginTurn();

                return;
            }

            RefreshUI();
            CheckEndCondition();
        }

        private void HandleOpponentCardDestroyed() => EvaluateStoryEffects(StoryEffectTrigger.OnOpponentCardDestroyed);
        private void HandlePlayerCardDestroyed() => EvaluateStoryEffects(StoryEffectTrigger.OnPlayerCardDestroyed);

        private void EvaluateStoryEffects(StoryEffectTrigger trigger, StoryCardDefinition sourceCard = null)
        {
            var card = sourceCard ?? _storyDeck?.ActiveCard;
            if (card?.effects == null) return;

            foreach (var effect in card.effects)
            {
                if (effect == null || effect.trigger != trigger) continue;

                switch (effect.action)
                {
                    case StoryEffectAction.IncrementStoryClock:
                        IncrementStoryClock(effect.actionValue);
                        break;
                    case StoryEffectAction.ResolvePlayedCardCounters:
                        ExecutePlayedCardCounterStoryEffect(effect);
                        break;
                }
            }
        }

        private void OnEndTurn()
        {
            _turn.EndTurn();
            RefreshUI();
            CheckEndCondition();

            if (!IsGameOver())
                BeginTurn();
        }

        private void RefreshUI()
        {
            handView.Refresh(_hand);
            boardView.Refresh(_board);
            opponentClockView.Refresh(_opponentClock);
            forceThreatBarView?.Refresh(_threatState?.GetBar(CardType.Force));
            witThreatBarView?.Refresh(_threatState?.GetBar(CardType.Wit));
            presenceThreatBarView?.Refresh(_threatState?.GetBar(CardType.Presence));
            progressionView?.Refresh(_progression);

            if (playedCardCounterView != null)
                playedCardCounterView.Refresh(_playedCardCounters);

            if (storyCardView != null)
                storyCardView.Refresh(_storyDeck?.ActiveCard, _storyDeck?.ActiveCardClock);

            statusView?.Refresh(_statusCollection);
            npcCardView?.Refresh(_npcDeck?.ActiveCard);
        }

        private void CheckEndCondition()
        {
            // Opponent clock no longer ends the run in the current prototype.
            // if (_opponentClock.IsFull && winPanel != null)
            //     winPanel.SetActive(true);

            if (_threatState != null && _threatState.AnyAtZero && losePanel != null)
                losePanel.SetActive(true);
        }

        private bool IsGameOver()
        {
            return _threatState != null && _threatState.AnyAtZero;
        }

        public void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void IncrementStoryClock(int amount)
        {
            if (_storyDeck == null || _storyDeck.ActiveCard == null) return;
            var completedCard = _storyDeck.ActiveCard;
            _storyDeck.ActiveCardClock.Increment(amount);
            // Overflow is intentional: excess points are discarded, not carried to the next card.
            if (_storyDeck.ActiveCardClock.IsFull)
            {
                EvaluateStoryEffects(StoryEffectTrigger.OnStoryClockCompleted, completedCard);
                _storyDeck.AdvanceCard();
            }
        }

        private void ExecutePlayedCardCounterStoryEffect(StoryEffectDefinition effect)
        {
            if (effect == null)
                return;

            var resolution = PlayedCardCounterDominanceResolver.Resolve(_playedCardCounters);
            ApplyStoryEffectOutcome(effect.GetOutcome(resolution));
        }

        private void ApplyStoryEffectOutcome(StoryEffectOutcome outcome)
        {
            if (outcome == null)
                return;

            if (outcome.deckMutations != null)
            {
                foreach (var deckMutation in outcome.deckMutations)
                {
                    if (deckMutation == null || deckMutation.cards == null)
                        continue;

                    _turn.AddCardsToDeck(deckMutation.cards, deckMutation.targetDeck, deckMutation.placement);
                }
            }

            if (outcome.slotMutations == null)
                return;

            foreach (var slotMutation in outcome.slotMutations)
                ApplySlotMutation(slotMutation);
        }

        private void ApplySlotMutation(StoryEffectSlotMutation slotMutation)
        {
            if (slotMutation == null || _board?.Slots == null)
                return;

            if (slotMutation.mode == StoryEffectSlotMutationMode.Add)
            {
                AddRuntimeStorySlot(slotMutation.slotDefinition);
                return;
            }

            if (slotMutation.slotIndex < 0 || slotMutation.slotIndex >= _board.Slots.Length)
                return;

            var slot = _board.Slots[slotMutation.slotIndex];
            if (slot == null)
                return;

            if (!slotMutation.active)
            {
                ReturnPlayerCardToHand(slot);
                slot.OpponentCard = null;
            }

            slot.SetDefinition(slotMutation.slotDefinition);
            slot.SetActive(slotMutation.active);

            if (slot.HasPlayerCard && slot.Definition != null && !slot.Definition.hasPlayerCardSpot)
                ReturnPlayerCardToHand(slot);

            if (slot.HasOpponentCard && slot.Definition != null && !slot.Definition.hasOpponentCardSpot)
                slot.OpponentCard = null;
        }

        private void AddRuntimeStorySlot(SlotDefinition definition)
        {
            if (definition == null)
                return;

            int slotIndex = _board.AddSlot(definition);
            if (boardView == null)
                return;

            int createdIndex = boardView.CreateRuntimeSlot(definition);
            if (createdIndex >= 0 && createdIndex != slotIndex)
                Debug.LogWarning($"Runtime slot index mismatch. BoardState={slotIndex}, BoardView={createdIndex}.");
        }

        private void ReturnPlayerCardToHand(SlotState slot)
        {
            if (slot?.PlayerCard == null)
                return;

            slot.PlayerCard.IsExhausted = false;
            _hand.Add(slot.PlayerCard);
            slot.PlayerCard = null;
        }
    }
}
