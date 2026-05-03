using System;
using UnityEngine;

namespace CardsUnity
{
    public class TurnController
    {
        private readonly DeckState _playerDeck;
        private readonly HandState _playerHand;
        private readonly DeckState _opponentDeck;
        private readonly BoardState _board;
        private readonly ClockState _opponentClock;
        private readonly ThreatState _threatState;
        private readonly ProgressionState _progression;
        private readonly PlayedCardCounterState _playedCardCounters;
        private readonly int _draftValue;
        private readonly System.Random _rng;
        private bool _turnEndedByEffect;

        public event Action OnOpponentCardDestroyed;
        public event Action OnPlayerCardDestroyed;

        public TurnController(
            DeckState playerDeck,
            HandState playerHand,
            DeckState opponentDeck,
            BoardState board,
            ThreatState threatState,
            ClockState opponentClock,
            ProgressionState progression,
            PlayedCardCounterState playedCardCounters,
            int draftValue,
            System.Random rng)
        {
            _playerDeck = playerDeck;
            _playerHand = playerHand;
            _opponentDeck = opponentDeck;
            _board = board;
            _opponentClock = opponentClock;
            _threatState = threatState;
            _progression = progression;
            _playedCardCounters = playedCardCounters;
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
            if (!slot.IsActive || slot.PlayerCard != null)
                return;

            if (!CanHostPlayerCard(slot))
                return;

            if (!_playerHand.Remove(card))
                return;

            slot.PlayerCard = card;
            slot.PlayerCard.IsExhausted = true;
            RegisterPlayedCard(slot, card);

            if (slot.HasOpponentCard)
                ResolveSlotCombat(slot);

            // Passive OnCardPlayed effects fire regardless of whether the card survived combat.
            // This is intentional: the effect is triggered by the act of playing, not card survival.
            EvaluateSlotEffectsForSlot(slot, SlotEffectTrigger.OnCardPlayed);
        }

        public void EndTurn()
        {
            Debug.Log($"[TurnController] EndTurn started. Board before OnRoundEnd: {DescribeBoardState()}");
            EvaluateSlotEffects(SlotEffectTrigger.OnRoundEnd);
            Debug.Log($"[TurnController] EndTurn continuing after OnRoundEnd. Board before returning player cards: {DescribeBoardState()}");

            foreach (var slot in _board.Slots)
            {
                if (!slot.IsActive)
                    continue;

                if (slot.PlayerCard == null)
                    continue;

                Debug.Log($"[TurnController] Returning player card from slot {GetSlotIndex(slot)} to hand. Card={DescribeCard(slot.PlayerCard)}");
                slot.PlayerCard.IsExhausted = false;
                _playerHand.Add(slot.PlayerCard);
                slot.PlayerCard = null;
            }

            Debug.Log($"[TurnController] EndTurn after returning player cards. Board before opponent refill: {DescribeBoardState()}");
            PlaceOpponentCards();
            Debug.Log($"[TurnController] EndTurn completed. Final board state: {DescribeBoardState()}");
        }

        public bool ConsumeTurnEndedByEffect()
        {
            bool result = _turnEndedByEffect;
            _turnEndedByEffect = false;
            return result;
        }

        public void AddCardsToDeck(
            System.Collections.Generic.IEnumerable<CardDefinition> cards,
            StoryEffectDeckTarget targetDeck,
            StoryEffectDeckPlacement placement)
        {
            var deck = targetDeck == StoryEffectDeckTarget.Player ? _playerDeck : _opponentDeck;
            if (deck == null || cards == null)
                return;

            foreach (var card in cards)
            {
                if (card == null)
                    continue;

                if (placement == StoryEffectDeckPlacement.DrawPile)
                    deck.AddToDrawPile(card);
                else
                    deck.AddToDiscardPile(card);
            }
        }

        private void ResolveSlotCombat(SlotState slot)
        {
            var result = CombatResolver.ResolveExchange(slot.PlayerCard, slot.OpponentCard, _rng);

            if (result.FirstResult.Destroyed)
            {
                var defeatedCard = slot.OpponentCard;
                _opponentClock.Increment();
                slot.OpponentCard = null;
                if (defeatedCard?.Definition != null)
                    _progression?.AddDefeatedCard(defeatedCard.Definition.type, defeatedCard.Definition.value);
                OnOpponentCardDestroyed?.Invoke();
            }

            if (result.SecondResult.Destroyed)
            {
                slot.PlayerCard = null;
                OnPlayerCardDestroyed?.Invoke();
            }
        }

        private void PlaceOpponentCards()
        {
            foreach (var slot in _board.Slots)
            {
                if (!slot.IsActive)
                    continue;

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
                {
                    slot.OpponentCard = new CardInstance(card);
                    Debug.Log($"[TurnController] Placed opponent card on slot {GetSlotIndex(slot)} during EndTurn. Card={DescribeCard(slot.OpponentCard)}");
                    EvaluateSlotEffects(SlotEffectTrigger.OnOpponentCardPlaced, slot, slot.OpponentCard);
                }
            }
        }

        private void EvaluateSlotEffects(SlotEffectTrigger trigger, SlotState triggeredSlot = null, CardInstance triggeringOpponentCard = null)
        {
            foreach (var slot in _board.Slots)
            {
                if (!slot.IsActive)
                    continue;

                EvaluateSlotEffectsForSlot(slot, trigger, triggeredSlot, triggeringOpponentCard);
            }
        }

        private void EvaluateSlotEffectsForSlot(
            SlotState slot,
            SlotEffectTrigger trigger,
            SlotState triggeredSlot = null,
            CardInstance triggeringOpponentCard = null)
        {
            if (slot.Definition?.effects == null)
                return;

            foreach (var effect in slot.Definition.effects)
            {
                if (effect == null || effect.trigger != trigger)
                    continue;

                bool contextMet = IsContextConditionMet(effect.context, slot);
                Debug.Log(
                    $"[TurnController] Evaluating slot effect. Trigger={trigger}, Slot={GetSlotIndex(slot)}, Context={effect.context}, " +
                    $"ContextMet={contextMet}, Action={effect.action}, ActionValue={effect.actionValue}, TargetType={effect.targetCardType}, " +
                    $"SlotState={DescribeSlotState(slot)}, TriggeredSlot={DescribeTriggeredSlot(triggeredSlot)}, TriggeringOpponentCard={DescribeCard(triggeringOpponentCard)}");

                if (!contextMet)
                    continue;

                ExecuteSlotEffect(effect, slot, triggeredSlot, triggeringOpponentCard);
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

        private void ExecuteSlotEffect(
            SlotEffectDefinition effect,
            SlotState slot,
            SlotState triggeredSlot = null,
            CardInstance triggeringOpponentCard = null)
        {
            Debug.Log(
                $"[TurnController] Executing slot effect. Slot={GetSlotIndex(slot)}, Context={effect.context}, Trigger={effect.trigger}, " +
                $"Action={effect.action}, ActionValue={effect.actionValue}, TargetType={effect.targetCardType}, " +
                $"BoardStateBeforeAction={DescribeBoardState()}");

            switch (effect.action)
            {
                case SlotEffectAction.DrawOpponentCard:
                    DrawOpponentCardToSlot(slot);
                    break;
                case SlotEffectAction.AddToClock:
                    _opponentClock.Increment(effect.actionValue);
                    break;
                case SlotEffectAction.DamageToThreat:
                    _threatState?.GetBar(effect.targetCardType)?.TakeDamage(effect.actionValue);
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
                case SlotEffectAction.IncreaseOpponentCardsOfTypeValue:
                    IncreaseOpponentCardsOfTypeValue(effect.targetCardType, effect.actionValue);
                    break;
                case SlotEffectAction.IncreaseAppearingOpponentCardOfTypeValue:
                    IncreaseAppearingOpponentCardOfTypeValue(triggeringOpponentCard, effect.targetCardType, effect.actionValue);
                    break;
            }

            Debug.Log(
                $"[TurnController] Finished slot effect. Slot={GetSlotIndex(slot)}, Action={effect.action}, BoardStateAfterAction={DescribeBoardState()}");
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
            {
                slot.OpponentCard = new CardInstance(card);
                EvaluateSlotEffects(SlotEffectTrigger.OnOpponentCardPlaced, slot, slot.OpponentCard);
            }
        }

        // Intentionally includes the slot that triggered this effect: if a draw slot
        // fires ReturnCardsFromSlots, the just-played card returns to hand as well.
        private void ReturnAllPlayerCardsToHand()
        {
            foreach (var slot in _board.Slots)
            {
                if (!slot.IsActive)
                    continue;

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

        private void IncreaseOpponentCardsOfTypeValue(CardType targetCardType, int amount)
        {
            if (amount == 0)
                return;

            foreach (var boardSlot in _board.Slots)
            {
                if (!boardSlot.IsActive || boardSlot.OpponentCard?.Definition == null)
                    continue;

                if (boardSlot.OpponentCard.Definition.type != targetCardType)
                    continue;

                boardSlot.OpponentCard.CurrentValue += amount;
            }
        }

        private static void IncreaseAppearingOpponentCardOfTypeValue(CardInstance card, CardType targetCardType, int amount)
        {
            if (card?.Definition == null || amount == 0)
                return;

            if (card.Definition.type != targetCardType)
                return;

            card.CurrentValue += amount;
        }

        private static bool CanHostPlayerCard(SlotState slot)
        {
            return slot?.Definition == null || slot.Definition.hasPlayerCardSpot;
        }

        private static bool CanHostOpponentCard(SlotState slot)
        {
            return slot?.Definition == null || slot.Definition.hasOpponentCardSpot;
        }

        private void RegisterPlayedCard(SlotState slot, CardInstance card)
        {
            if (_playedCardCounters == null || slot?.Definition == null || card?.Definition == null)
                return;

            if (!slot.Definition.contributesToGlobalPlayedCardCounts)
                return;

            int amount = slot.Definition.playedCardCountMode == PlayedCardCountMode.UseFixedValue
                ? slot.Definition.fixedPlayedCardCountValue
                : card.CurrentValue;

            _playedCardCounters.Add(card.Definition.type, amount);
        }

        private string DescribeBoardState()
        {
            var parts = new string[_board.Slots.Length];
            for (int i = 0; i < _board.Slots.Length; i++)
                parts[i] = DescribeSlotState(_board.Slots[i]);

            return string.Join(" | ", parts);
        }

        private string DescribeSlotState(SlotState slot)
        {
            if (slot == null)
                return "Slot<null>";

            return $"Slot[{GetSlotIndex(slot)}](Active={slot.IsActive}, Player={DescribeCard(slot.PlayerCard)}, Opponent={DescribeCard(slot.OpponentCard)}, HasDefinition={slot.Definition != null})";
        }

        private string DescribeTriggeredSlot(SlotState slot)
        {
            return slot == null ? "none" : $"Slot[{GetSlotIndex(slot)}]";
        }

        private int GetSlotIndex(SlotState slot)
        {
            if (slot == null || _board?.Slots == null)
                return -1;

            for (int i = 0; i < _board.Slots.Length; i++)
            {
                if (ReferenceEquals(_board.Slots[i], slot))
                    return i;
            }

            return -1;
        }

        private static string DescribeCard(CardInstance card)
        {
            if (card?.Definition == null)
                return "none";

            return $"{card.Definition.type}:{card.CurrentValue}(Exhausted={card.IsExhausted})";
        }
    }
}
