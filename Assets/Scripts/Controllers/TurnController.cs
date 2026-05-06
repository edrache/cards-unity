using System;
using System.Linq;
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
        private readonly StatusCollection _statusCollection;
        private readonly NpcDeckState _npcDeck;
        private readonly int _draftValue;
        private readonly System.Random _rng;
        private bool _turnEndedByEffect;

        public event Action OnOpponentCardDestroyed;
        public event Action OnPlayerCardDestroyed;
        public event Action<NpcCardDefinition> OnNpcCardDrawn;

        public TurnController(
            DeckState playerDeck,
            HandState playerHand,
            DeckState opponentDeck,
            BoardState board,
            ThreatState threatState,
            ClockState opponentClock,
            ProgressionState progression,
            PlayedCardCounterState playedCardCounters,
            StatusCollection statusCollection,
            NpcDeckState npcDeck,
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
            _statusCollection = statusCollection;
            _npcDeck = npcDeck;
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

            EvaluateStatusEffects();
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
                MovePlayerBoardCardToDiscard(slot, "combat");
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
                    $"ValueSource={effect.valueSource}, SlotCardTarget={effect.slotCardTarget}, " +
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
                $"ValueSource={effect.valueSource}, SlotCardTarget={effect.slotCardTarget}, " +
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
                    ApplyDamageToThreat(effect, slot);
                    break;
                case SlotEffectAction.DrawNpcCard:
                    DrawNpcCard();
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
                    if (TryResolveFixedTargetCardType(effect.targetCardType, out CardType fixedTargetCardType))
                        IncreaseOpponentCardsOfTypeValue(fixedTargetCardType, effect.actionValue);
                    break;
                case SlotEffectAction.IncreaseAppearingOpponentCardOfTypeValue:
                    if (TryResolveFixedTargetCardType(effect.targetCardType, out CardType appearingTargetCardType))
                        IncreaseAppearingOpponentCardOfTypeValue(triggeringOpponentCard, appearingTargetCardType, effect.actionValue);
                    break;
                case SlotEffectAction.ModifyPlayerCardsOfTypeValue:
                    if (TryResolveFixedTargetCardType(effect.targetCardType, out CardType playerTargetCardType))
                        ModifyPlayerCardsOfTypeValue(playerTargetCardType, effect.actionValue);
                    break;
                case SlotEffectAction.ProgressSlotClock:
                    ProgressSlotClock(slot, effect.actionValue);
                    break;
            }

            DiscardPlayerCardsAtOrBelowZero();

            Debug.Log(
                $"[TurnController] Finished slot effect. Slot={GetSlotIndex(slot)}, Action={effect.action}, BoardStateAfterAction={DescribeBoardState()}");
        }

        private void DrawNpcCard()
        {
            if (_npcDeck == null)
                return;

            var card = _npcDeck.Draw(_rng);
            if (card != null)
                OnNpcCardDrawn?.Invoke(card);
        }

        private void ProgressSlotClock(SlotState slot, int amount)
        {
            if (slot?.ClockState == null)
            {
                Debug.Log(
                    $"[TurnController] Skipping ProgressSlotClock because slot {GetSlotIndex(slot)} has no clock state.");
                return;
            }

            int previousValue = slot.ClockState.CurrentValue;
            slot.ClockState.Increment(amount);

            Debug.Log(
                $"[TurnController] Progressed slot clock. Slot={GetSlotIndex(slot)}, Delta={amount}, Previous={previousValue}, Current={slot.ClockState.CurrentValue}, Max={slot.ClockState.MaxValue}, IsFull={slot.ClockState.IsFull}");

            if (!slot.ClockState.IsFull)
                return;

            ExecuteSlotClockCompletion(slot);
        }

        private void ExecuteSlotClockCompletion(SlotState slot)
        {
            var config = slot?.Definition?.clock;
            if (config == null)
            {
                Debug.Log(
                    $"[TurnController] Skipping slot clock completion because slot {GetSlotIndex(slot)} has no clock config.");
                return;
            }

            Debug.Log(
                $"[TurnController] Resolving slot clock completion. Slot={GetSlotIndex(slot)}, CompletionEffect={config.completionEffect}, BoardStateBeforeCompletion={DescribeBoardState()}");

            switch (config.completionEffect)
            {
                case SlotClockCompletionEffect.RemoveSlot:
                    slot.SetActive(false);
                    break;
            }

            Debug.Log(
                $"[TurnController] Finished slot clock completion. Slot={GetSlotIndex(slot)}, CompletionEffect={config.completionEffect}, SlotActive={slot.IsActive}, BoardStateAfterCompletion={DescribeBoardState()}");
        }

        private static int GetEffectValue(SlotEffectDefinition effect, SlotState slot)
        {
            if (effect.valueSource != SlotEffectValueSource.CardOnSlotValue)
                return effect.actionValue;

            CardInstance sourceCard = GetCardOnSlot(slot, effect.slotCardTarget);

            return sourceCard?.CurrentValue ?? 0;
        }

        private void ApplyDamageToThreat(SlotEffectDefinition effect, SlotState slot)
        {
            if (effect.valueSource == SlotEffectValueSource.CardOnSlotValue &&
                GetCardOnSlot(slot, effect.slotCardTarget) == null)
            {
                Debug.Log(
                    $"[TurnController] Skipping DamageToThreat because slot card target {effect.slotCardTarget} is missing on slot {GetSlotIndex(slot)}.");
                return;
            }

            if (!TryResolveTargetCardType(effect, slot, out CardType targetCardType))
            {
                Debug.Log(
                    $"[TurnController] Skipping DamageToThreat because target card type {effect.targetCardType} could not be resolved on slot {GetSlotIndex(slot)}.");
                return;
            }

            int damage = GetEffectValue(effect, slot);
            if (damage <= 0)
                return;

            _threatState?.GetBar(targetCardType)?.TakeDamage(damage);
        }

        private static bool TryResolveTargetCardType(SlotEffectDefinition effect, SlotState slot, out CardType targetCardType)
        {
            switch (effect.targetCardType)
            {
                case SlotEffectTargetCardType.Force:
                    targetCardType = CardType.Force;
                    return true;
                case SlotEffectTargetCardType.Presence:
                    targetCardType = CardType.Presence;
                    return true;
                case SlotEffectTargetCardType.Wit:
                    targetCardType = CardType.Wit;
                    return true;
                case SlotEffectTargetCardType.OpponentCard:
                    return TryGetCardTypeFromSlotTarget(slot, SlotEffectSlotCardTarget.OpponentCard, out targetCardType);
                case SlotEffectTargetCardType.PlayerCard:
                    return TryGetCardTypeFromSlotTarget(slot, SlotEffectSlotCardTarget.PlayerCard, out targetCardType);
                default:
                    targetCardType = default;
                    return false;
            }
        }

        private static bool TryResolveFixedTargetCardType(SlotEffectTargetCardType targetCardType, out CardType resolvedCardType)
        {
            switch (targetCardType)
            {
                case SlotEffectTargetCardType.Force:
                    resolvedCardType = CardType.Force;
                    return true;
                case SlotEffectTargetCardType.Presence:
                    resolvedCardType = CardType.Presence;
                    return true;
                case SlotEffectTargetCardType.Wit:
                    resolvedCardType = CardType.Wit;
                    return true;
                default:
                    resolvedCardType = default;
                    return false;
            }
        }

        private static bool TryGetCardTypeFromSlotTarget(
            SlotState slot,
            SlotEffectSlotCardTarget slotCardTarget,
            out CardType targetCardType)
        {
            CardInstance sourceCard = GetCardOnSlot(slot, slotCardTarget);
            if (sourceCard?.Definition == null)
            {
                targetCardType = default;
                return false;
            }

            targetCardType = sourceCard.Definition.type;
            return true;
        }

        private static CardInstance GetCardOnSlot(SlotState slot, SlotEffectSlotCardTarget slotCardTarget)
        {
            return slotCardTarget switch
            {
                SlotEffectSlotCardTarget.PlayerCard => slot.PlayerCard,
                _ => slot.OpponentCard,
            };
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

        private void ModifyPlayerCardsOfTypeValue(CardType targetCardType, int amount)
        {
            if (amount == 0)
                return;

            int modifiedCount = 0;

            foreach (var handCard in _playerHand.Cards)
            {
                if (handCard?.Definition == null || handCard.Definition.type != targetCardType)
                    continue;

                int previousValue = handCard.CurrentValue;
                handCard.CurrentValue += amount;
                modifiedCount++;

                Debug.Log(
                    $"[TurnController] Modified player hand card value. Type={targetCardType}, Delta={amount}, Before={previousValue}, After={handCard.CurrentValue}, Card={DescribeCard(handCard)}");
            }

            foreach (var boardSlot in _board.Slots)
            {
                if (!boardSlot.IsActive || boardSlot.PlayerCard?.Definition == null)
                    continue;

                if (boardSlot.PlayerCard.Definition.type != targetCardType)
                    continue;

                int previousValue = boardSlot.PlayerCard.CurrentValue;
                boardSlot.PlayerCard.CurrentValue += amount;
                modifiedCount++;

                Debug.Log(
                    $"[TurnController] Modified player board card value. Slot={GetSlotIndex(boardSlot)}, Type={targetCardType}, Delta={amount}, Before={previousValue}, After={boardSlot.PlayerCard.CurrentValue}, Card={DescribeCard(boardSlot.PlayerCard)}");
            }

            Debug.Log(
                $"[TurnController] Finished modifying player card values. Type={targetCardType}, Delta={amount}, ModifiedCards={modifiedCount}");
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

        private void EvaluateStatusEffects()
        {
            _statusCollection?.EvaluateAndTick(ApplyStatusEffect);
        }

        private void ApplyStatusEffect(StatusState status)
        {
            var definition = status?.Definition;
            if (definition == null)
                return;

            Debug.Log(
                $"[TurnController] Applying status effect. Name={definition.statusName}, DurationType={definition.durationType}, RemainingTurns={status.RemainingTurns}, " +
                $"Action={definition.action}, TargetType={definition.targetCardType}, ActionValue={definition.actionValue}");

            switch (definition.action)
            {
                case SlotEffectAction.DamageToThreat:
                    if (TryResolveFixedTargetCardType(definition.targetCardType, out CardType threatType))
                        _threatState?.GetBar(threatType)?.TakeDamage(definition.actionValue);
                    break;
                case SlotEffectAction.AddToClock:
                    _opponentClock?.Increment(definition.actionValue);
                    break;
            }

            DiscardPlayerCardsAtOrBelowZero();
        }

        private void DiscardPlayerCardsAtOrBelowZero()
        {
            var zeroHandCards = _playerHand.Cards
                .Where(card => card?.Definition != null && card.CurrentValue <= 0)
                .ToList();

            foreach (var handCard in zeroHandCards)
                MovePlayerHandCardToDiscard(handCard, "value reached zero in hand");

            foreach (var boardSlot in _board.Slots)
            {
                if (!boardSlot.IsActive || boardSlot.PlayerCard?.Definition == null)
                    continue;

                if (boardSlot.PlayerCard.CurrentValue > 0)
                    continue;

                MovePlayerBoardCardToDiscard(boardSlot, "value reached zero on board");
            }
        }

        private void MovePlayerHandCardToDiscard(CardInstance card, string reason)
        {
            if (card?.Definition == null)
                return;

            if (!_playerHand.Remove(card))
                return;

            _playerDeck?.AddToDiscardPile(card.Definition);
            Debug.Log(
                $"[TurnController] Discarded player hand card. Reason={reason}, Card={DescribeCard(card)}");
            OnPlayerCardDestroyed?.Invoke();
        }

        private void MovePlayerBoardCardToDiscard(SlotState slot, string reason)
        {
            if (slot?.PlayerCard?.Definition == null)
                return;

            CardInstance card = slot.PlayerCard;
            slot.PlayerCard = null;
            _playerDeck?.AddToDiscardPile(card.Definition);
            Debug.Log(
                $"[TurnController] Discarded player board card. Reason={reason}, Slot={GetSlotIndex(slot)}, Card={DescribeCard(card)}");
            OnPlayerCardDestroyed?.Invoke();
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
