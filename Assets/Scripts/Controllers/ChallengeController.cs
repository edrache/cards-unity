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
        [SerializeField] private ClockView playerClockView;
        [SerializeField] private ClockView opponentClockView;
        [SerializeField] private PlayedCardCounterView playedCardCounterView;
        [SerializeField] private EndTurnButton endTurnButton;
        [SerializeField] private GameObject winPanel;
        [SerializeField] private GameObject losePanel;
        [SerializeField] private StoryDeckDefinition storyDeck;
        [SerializeField] private StoryCardView storyCardView;

        private TurnController _turn;
        private HandState _hand;
        private BoardState _board;
        private ClockState _playerClock;
        private ClockState _opponentClock;
        private PlayedCardCounterState _playedCardCounters;
        private System.Random _rng;
        private StoryDeckState _storyDeck;
        private Action<CardView> _cardDragStartHandler;
        private Action<CardView> _cardDragEndHandler;

        private void Start()
        {
            if (config == null || boardView == null || handView == null ||
                playerClockView == null || opponentClockView == null || endTurnButton == null)
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
            _board = slotDefs != null
                ? new BoardState(slotDefs)
                : new BoardState(config.boardSlotCount);
            _playerClock = new ClockState(config.startingDeck != null ? config.startingDeck.Count : 0);
            _opponentClock = new ClockState(config.opponentDeck != null ? config.opponentDeck.Count : 0);
            _playedCardCounters = new PlayedCardCounterState();

            _turn = new TurnController(
                playerDeck,
                _hand,
                opponentDeck,
                _board,
                _playerClock,
                _opponentClock,
                _playedCardCounters,
                config.draftValue,
                _rng);

            if (storyDeck != null)
                _storyDeck = new StoryDeckState(storyDeck, _rng);

            _cardDragStartHandler = _ => boardView.SetHighlightAll(true);
            _cardDragEndHandler = _ => boardView.SetHighlightAll(false);

            boardView.OnCardPlayed += OnCardPlayed;
            handView.OnCardDragStart += _cardDragStartHandler;
            handView.OnCardDragEnd += _cardDragEndHandler;
            endTurnButton.OnClicked += OnEndTurn;
            _turn.OnCardDestroyed += OnCardDestroyed;

            if (winPanel != null)
                winPanel.SetActive(false);

            if (losePanel != null)
                losePanel.SetActive(false);

            // ConfigureSlots must run before BeginTurn so slot labels are ready before the board populates
            if (slotDefs != null)
                boardView.ConfigureSlots(slotDefs);

            BeginTurn();
            _turn.EndTurn();
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
                _turn.OnCardDestroyed -= OnCardDestroyed;
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

        private void BeginTurn()
        {
            _turn.StartTurn();
            RefreshUI();
        }

        private void OnCardDestroyed() => IncrementStoryClock(1);

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
            playerClockView.Refresh(_playerClock);
            opponentClockView.Refresh(_opponentClock);

            if (playedCardCounterView != null)
                playedCardCounterView.Refresh(_playedCardCounters);

            if (storyCardView != null)
                storyCardView.Refresh(_storyDeck?.ActiveCard, _storyDeck?.ActiveCardClock);
        }

        private void CheckEndCondition()
        {
            if (_opponentClock.IsFull && winPanel != null)
                winPanel.SetActive(true);

            if (_playerClock.IsFull && losePanel != null)
                losePanel.SetActive(true);
        }

        private bool IsGameOver()
        {
            return _playerClock.IsFull || _opponentClock.IsFull;
        }

        public void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void IncrementStoryClock(int amount)
        {
            if (_storyDeck == null || _storyDeck.ActiveCard == null) return;
            _storyDeck.ActiveCardClock.Increment(amount);
            // Overflow is intentional: excess points are discarded, not carried to the next card.
            if (_storyDeck.ActiveCardClock.IsFull)
                _storyDeck.AdvanceCard();
        }
    }
}
