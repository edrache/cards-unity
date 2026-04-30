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
        private bool _turnEndedByEffect;

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
                    _playerHand.Add(new CardInstance(card));
            }

            EvaluateSlotEffects(SlotEffectTrigger.OnPlayerTurnStart);
        }

        public void PlayCard(CardInstance card, int slotIndex)
        {
            _turnEndedByEffect = false;

            if (card == null)
                throw new ArgumentNullException(nameof(card));

            if (slotIndex < 0 || slotIndex >= _board.Slots.Length)
                throw new ArgumentOutOfRangeException(nameof(slotIndex));

            var slot = _board.Slots[slotIndex];
            if (slot.PlayerCard != null)
                return;

            if (!CanHostPlayerCard(slot))
                return;

            if (!_playerHand.Remove(card))
                return;

            slot.PlayerCard = card;
            slot.PlayerCard.IsExhausted = true;

            if (slot.HasOpponentCard)
                ResolveSlotCombat(slot);

            // Passive OnCardPlayed effects fire regardless of whether the card survived combat.
            // This is intentional: the effect is triggered by the act of playing, not card survival.
            EvaluateSlotEffectsForSlot(slot, SlotEffectTrigger.OnCardPlayed);
        }

        public void EndTurn()
        {
            foreach (var slot in _board.Slots)
            {
                if (slot.PlayerCard == null)
                    continue;

                slot.PlayerCard.IsExhausted = false;
                _playerHand.Add(slot.PlayerCard);
                slot.PlayerCard = null;
            }

            PlaceOpponentCards();
        }

        public bool ConsumeTurnEndedByEffect()
        {
            bool result = _turnEndedByEffect;
            _turnEndedByEffect = false;
            return result;
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

                if (slot.Definition != null)
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

        private void EvaluateSlotEffects(SlotEffectTrigger trigger)
        {
            foreach (var slot in _board.Slots)
                EvaluateSlotEffectsForSlot(slot, trigger);
        }

        private void EvaluateSlotEffectsForSlot(SlotState slot, SlotEffectTrigger trigger)
        {
            if (slot.Definition?.effects == null)
                return;

            foreach (var effect in slot.Definition.effects)
            {
                if (effect == null || effect.trigger != trigger)
                    continue;

                if (!IsContextConditionMet(effect.context, slot))
                    continue;

                ExecuteSlotEffect(effect, slot);
            }
        }

        private static bool IsContextConditionMet(SlotEffectContext context, SlotState slot)
        {
            return context switch
            {
                SlotEffectContext.NoOpponentCard => !slot.HasOpponentCard,
                SlotEffectContext.NoPlayerCard => !slot.HasPlayerCard,
                SlotEffectContext.Passive => true,
                _ => false
            };
        }

        private void ExecuteSlotEffect(SlotEffectDefinition effect, SlotState slot)
        {
            switch (effect.action)
            {
                case SlotEffectAction.DrawOpponentCard:
                    DrawOpponentCardToSlot(slot);
                    break;
                case SlotEffectAction.AddToClock:
                    var clock = effect.clockTarget == ClockTarget.PlayerClock ? _playerClock : _opponentClock;
                    clock.Increment(effect.actionValue);
                    break;
                case SlotEffectAction.ReturnCardsFromSlots:
                    ReturnAllPlayerCardsToHand();
                    break;
                case SlotEffectAction.DrawToHandLimit:
                    DrawPlayerCardsToHandLimit();
                    break;
                case SlotEffectAction.TurnEnd:
                    EndTurn();
                    _turnEndedByEffect = true;
                    break;
            }
        }

        private void DrawOpponentCardToSlot(SlotState slot)
        {
            if (!CanHostOpponentCard(slot))
                return;

            if (slot.HasOpponentCard)
                return;

            if (_opponentDeck.DrawCount == 0 && _opponentDeck.CanDraw)
                _opponentDeck.ShuffleDiscardIntoDrawPile(_rng);

            if (!_opponentDeck.CanDraw)
                return;

            var card = _opponentDeck.Draw();
            if (card != null)
                slot.OpponentCard = new CardInstance(card);
        }

        // Intentionally includes the slot that triggered this effect: if a draw slot
        // fires ReturnCardsFromSlots, the just-played card returns to hand as well.
        private void ReturnAllPlayerCardsToHand()
        {
            foreach (var slot in _board.Slots)
            {
                if (slot.PlayerCard == null)
                    continue;

                slot.PlayerCard.IsExhausted = false;
                _playerHand.Add(slot.PlayerCard);
                slot.PlayerCard = null;
            }
        }

        private void DrawPlayerCardsToHandLimit()
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
                    _playerHand.Add(new CardInstance(card));
            }
        }

        private static bool CanHostPlayerCard(SlotState slot)
        {
            return slot?.Definition == null || slot.Definition.hasPlayerCardSpot;
        }

        private static bool CanHostOpponentCard(SlotState slot)
        {
            return slot?.Definition == null || slot.Definition.hasOpponentCardSpot;
        }
    }
}
