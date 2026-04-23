using System;

namespace CardsUnity
{
    public class TurnController
    {
        private readonly DeckState _playerDeck;
        private readonly HandState _playerHand;
        private readonly DeckState _opponentDeck;
        private readonly BoardState _board;
        private readonly ClockState _playerClock;
        private readonly ClockState _opponentClock;
        private readonly int _draftValue;
        private readonly Random _rng;

        public TurnController(
            DeckState playerDeck,
            HandState playerHand,
            DeckState opponentDeck,
            BoardState board,
            ClockState playerClock,
            ClockState opponentClock,
            int draftValue,
            Random rng)
        {
            _playerDeck = playerDeck;
            _playerHand = playerHand;
            _opponentDeck = opponentDeck;
            _board = board;
            _playerClock = playerClock;
            _opponentClock = opponentClock;
            _draftValue = draftValue;
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public void StartTurn()
        {
            int cardsToDraw = Math.Max(0, _draftValue - _playerHand.Cards.Count);

            for (int i = 0; i < cardsToDraw; i++)
            {
                if (_playerDeck.DrawCount == 0 && _playerDeck.CanDraw)
                    _playerDeck.ShuffleDiscardIntoDrawPile(_rng);

                if (!_playerDeck.CanDraw)
                    break;

                var card = _playerDeck.Draw();
                if (card != null)
                    _playerHand.Add(card);
            }
        }

        public void PlayCard(CardDefinition card, int slotIndex)
        {
            if (card == null)
                throw new ArgumentNullException(nameof(card));

            if (slotIndex < 0 || slotIndex >= _board.Slots.Length)
                throw new ArgumentOutOfRangeException(nameof(slotIndex));

            var slot = _board.Slots[slotIndex];
            if (slot.PlayerCard != null)
                return;

            if (!_playerHand.Remove(card))
                return;

            slot.PlayerCard = new CardInstance(card);

            if (slot.HasOpponentCard)
                ResolveSlotCombat(slot);
        }

        public void EndTurn()
        {
            foreach (var slot in _board.Slots)
            {
                if (slot.PlayerCard == null)
                    continue;

                _playerHand.Add(slot.PlayerCard.Definition);
                slot.PlayerCard = null;
            }

            PlaceOpponentCards();
        }

        private void ResolveSlotCombat(SlotState slot)
        {
            var result = CombatResolver.ResolveExchange(slot.PlayerCard, slot.OpponentCard, _rng);

            if (result.FirstResult.Destroyed)
            {
                _opponentClock.Increment();
                slot.OpponentCard = null;
            }

            if (result.SecondResult.Destroyed)
            {
                _playerClock.Increment();
                slot.PlayerCard = null;
            }
        }

        private void PlaceOpponentCards()
        {
            foreach (var slot in _board.Slots)
            {
                if (slot.HasOpponentCard)
                    continue;

                if (_opponentDeck.DrawCount == 0 && _opponentDeck.CanDraw)
                    _opponentDeck.ShuffleDiscardIntoDrawPile(_rng);

                if (!_opponentDeck.CanDraw)
                    break;

                var card = _opponentDeck.Draw();
                if (card != null)
                    slot.OpponentCard = new CardInstance(card);
            }
        }
    }
}
