using System.Linq;
using NUnit.Framework;
using CardsUnity;
using UnityEngine;
using System.Collections.Generic;

namespace CardsUnity.Tests
{
    public class TurnControllerTests
    {
        private TurnController _turn;
        private DeckState _playerDeck;
        private HandState _playerHand;
        private DeckState _opponentDeck;
        private BoardState _board;
        private ThreatState _threatState;
        private ClockState _opponentClock;
        private ProgressionState _progression;
        private PlayedCardCounterState _playedCardCounters;
        private StatusCollection _statusCollection;
        private NpcDeckState _npcDeck;

        [SetUp]
        public void SetUp()
        {
            _playerDeck = MakeDeck(6, CardType.Force, value: 3);
            _playerHand = new HandState();
            _opponentDeck = MakeDeck(4, CardType.Presence, value: 4);
            _board = new BoardState(slotCount: 3);
            _threatState = MakeThreatState(maxPerBar: 10);
            _opponentClock = new ClockState(maxValue: 4);
            _progression = new ProgressionState(xpCap: 100);
            _playedCardCounters = new PlayedCardCounterState();
            _statusCollection = new StatusCollection();

            _turn = new TurnController(
                _playerDeck, _playerHand,
                _opponentDeck, _board,
                _threatState, _opponentClock, _progression, _playedCardCounters,
                _statusCollection,
                _npcDeck,
                draftValue: 3,
                rng: new System.Random(42));
        }

        [Test]
        public void StartTurn_draws_up_to_draft_value()
        {
            _turn.StartTurn();
            Assert.AreEqual(3, _playerHand.Cards.Count);
        }

        [Test]
        public void StartTurn_does_not_exceed_draft_value_when_hand_already_has_cards()
        {
            _playerHand.Add(new CardInstance(ScriptableObject.CreateInstance<CardDefinition>()));
            _turn.StartTurn();
            Assert.AreEqual(3, _playerHand.Cards.Count);
        }

        [Test]
        public void StartTurn_shuffles_discard_when_draw_pile_empty()
        {
            for (int i = 0; i < 6; i++)
                _playerDeck.Discard(_playerDeck.Draw());

            _turn.StartTurn();
            Assert.AreEqual(3, _playerHand.Cards.Count);
        }

        [Test]
        public void PlayCard_places_card_in_slot()
        {
            _turn.StartTurn();
            var card = _playerHand.Cards[0];
            _turn.PlayCard(card, slotIndex: 0);
            Assert.AreSame(card, _board.Slots[0].PlayerCard);
        }

        [Test]
        public void PlayCard_removes_card_from_hand()
        {
            _turn.StartTurn();
            var card = _playerHand.Cards[0];
            _turn.PlayCard(card, slotIndex: 0);
            Assert.IsFalse(_playerHand.Cards.Contains(card));
        }

        [Test]
        public void PlayCard_marks_player_card_as_exhausted()
        {
            _turn.StartTurn();
            var card = _playerHand.Cards[0];

            _turn.PlayCard(card, slotIndex: 0);

            Assert.IsTrue(_board.Slots[0].PlayerCard.IsExhausted);
        }

        [Test]
        public void PlayCard_to_occupied_slot_deals_full_damage_on_win()
        {
            _turn.StartTurn();
            var opponentCard = new CardInstance(MakeCardDef(CardType.Presence, value: 10));
            _board.Slots[1].OpponentCard = opponentCard;

            var playerCard = _playerHand.Cards.First(c => c.Definition.type == CardType.Force);
            _turn.PlayCard(playerCard, slotIndex: 1);

            Assert.AreEqual(7, opponentCard.CurrentValue);
        }

        [Test]
        public void PlayCard_increments_opponent_clock_when_card_destroyed()
        {
            _turn.StartTurn();
            _board.Slots[0].OpponentCard = new CardInstance(MakeCardDef(CardType.Presence, value: 1));

            var playerCard = _playerHand.Cards.First(c => c.Definition.type == CardType.Force);
            _turn.PlayCard(playerCard, slotIndex: 0);

            Assert.AreEqual(1, _opponentClock.CurrentValue);
        }

        [Test]
        public void PlayCard_applies_counter_damage_to_player_card()
        {
            _turn.StartTurn();
            _board.Slots[0].OpponentCard = new CardInstance(MakeCardDef(CardType.Wit, value: 1));

            var playerCard = _playerHand.Cards.First(c => c.Definition.type == CardType.Force);
            _turn.PlayCard(playerCard, slotIndex: 0);

            Assert.AreEqual(2, _board.Slots[0].PlayerCard?.CurrentValue);
        }

        [Test]
        public void PlayCard_destroying_opponent_card_adds_to_progression()
        {
            _turn.StartTurn();
            _board.Slots[0].OpponentCard = new CardInstance(MakeCardDef(CardType.Presence, value: 1));

            var playerCard = _playerHand.Cards.First(c => c.Definition.type == CardType.Force);
            _turn.PlayCard(playerCard, slotIndex: 0);

            Assert.AreEqual(1, _progression.DefeatedCounters.Presence);
            Assert.AreEqual(1, _progression.CurrentXp);
        }

        [Test]
        public void PlayCard_player_card_destroyed_does_not_affect_threat_bars()
        {
            _turn.StartTurn();
            _board.Slots[0].OpponentCard = new CardInstance(MakeCardDef(CardType.Wit, value: 10));

            var playerCard = _playerHand.Cards.First(c => c.Definition.type == CardType.Force);
            _turn.PlayCard(playerCard, slotIndex: 0);

            Assert.IsFalse(_threatState.AnyAtZero);
            Assert.IsNull(_board.Slots[0].PlayerCard);
        }

        [Test]
        public void PlayCard_player_card_destroyed_moves_card_to_player_discard()
        {
            _turn.StartTurn();
            _board.Slots[0].OpponentCard = new CardInstance(MakeCardDef(CardType.Wit, value: 10));

            var playerCard = _playerHand.Cards.First(c => c.Definition.type == CardType.Force);
            int discardCountBefore = _playerDeck.DiscardCount;

            _turn.PlayCard(playerCard, slotIndex: 0);

            Assert.IsFalse(_playerHand.Cards.Contains(playerCard));
            Assert.IsNull(_board.Slots[0].PlayerCard);
            Assert.AreEqual(discardCountBefore + 1, _playerDeck.DiscardCount);
        }

        [Test]
        public void EndTurn_returns_player_cards_to_hand()
        {
            _turn.StartTurn();
            var card = _playerHand.Cards[0];
            _turn.PlayCard(card, slotIndex: 0);
            _turn.EndTurn();

            Assert.IsTrue(_playerHand.Cards.Contains(card));
        }

        [Test]
        public void EndTurn_preserves_current_value_for_returned_player_cards()
        {
            _turn.StartTurn();
            _board.Slots[0].OpponentCard = new CardInstance(MakeCardDef(CardType.Wit, value: 1));

            var card = _playerHand.Cards.First(c => c.Definition.type == CardType.Force);
            _turn.PlayCard(card, slotIndex: 0);

            Assert.AreEqual(2, card.CurrentValue);

            _turn.EndTurn();

            Assert.That(_playerHand.Cards, Does.Contain(card));
            Assert.AreEqual(2, card.CurrentValue);
        }

        [Test]
        public void EndTurn_clears_player_cards_from_slots()
        {
            _turn.StartTurn();
            _turn.PlayCard(_playerHand.Cards[0], slotIndex: 0);
            _turn.EndTurn();

            Assert.IsNull(_board.Slots[0].PlayerCard);
        }

        [Test]
        public void EndTurn_places_opponent_cards_in_empty_slots()
        {
            _turn.StartTurn();
            _turn.EndTurn();

            Assert.Greater(_board.Slots.Count(s => s.HasOpponentCard), 0);
        }

        [Test]
        public void EndTurn_OnRoundEnd_effects_are_evaluated_when_round_end_is_triggered()
        {
            var endRoundSlotDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnRoundEnd,
                SlotEffectAction.AddToClock,
                actionValue: 2);
            var board = new BoardState(new SlotDefinition[] { endRoundSlotDef, null, null });
            var turn = MakeTurnWithBoard(board);

            turn.EndTurn();

            Assert.AreEqual(2, _opponentClock.CurrentValue);
        }

        [Test]
        public void EndTurn_ProgressSlotClock_increments_slot_clock()
        {
            var def = MakeSlotDefWithClock(
                maxValue: 5,
                completionEffect: SlotClockCompletionEffect.RemoveSlot,
                progressPerTrigger: 2);
            _board.Slots[0].SetDefinition(def);

            _turn.EndTurn();

            Assert.NotNull(_board.Slots[0].ClockState);
            Assert.AreEqual(2, _board.Slots[0].ClockState.CurrentValue);
            Assert.IsTrue(_board.Slots[0].IsActive);
        }

        [Test]
        public void EndTurn_ProgressSlotClock_removes_slot_when_clock_reaches_max()
        {
            var def = MakeSlotDefWithClock(
                maxValue: 1,
                completionEffect: SlotClockCompletionEffect.RemoveSlot,
                progressPerTrigger: 1);
            _board.Slots[0].SetDefinition(def);

            _turn.EndTurn();

            Assert.NotNull(_board.Slots[0].ClockState);
            Assert.AreEqual(1, _board.Slots[0].ClockState.CurrentValue);
            Assert.IsFalse(_board.Slots[0].IsActive);
        }

        private static DeckState MakeDeck(int count, CardType type, int value)
        {
            var deck = new DeckState();
            deck.Initialize(Enumerable.Range(0, count).Select(_ => MakeCardDef(type, value)).ToList());
            return deck;
        }

        private static CardDefinition MakeCardDef(CardType type, int value)
        {
            var def = ScriptableObject.CreateInstance<CardDefinition>();
            def.type = type;
            def.value = value;
            return def;
        }

        private static SlotDefinition MakeSlotDefinition(
            SlotEffectContext context,
            SlotEffectTrigger trigger,
            SlotEffectAction action,
            int actionValue = 1)
        {
            var effect = ScriptableObject.CreateInstance<SlotEffectDefinition>();
            effect.context = context;
            effect.trigger = trigger;
            effect.action = action;
            effect.actionValue = actionValue;

            var def = ScriptableObject.CreateInstance<SlotDefinition>();
            def.effects = new System.Collections.Generic.List<SlotEffectDefinition> { effect };
            return def;
        }

        private static SlotDefinition MakeSlotDefWithClock(
            int maxValue,
            SlotClockCompletionEffect completionEffect,
            int progressPerTrigger)
        {
            var effect = ScriptableObject.CreateInstance<SlotEffectDefinition>();
            effect.context = SlotEffectContext.Passive;
            effect.trigger = SlotEffectTrigger.OnRoundEnd;
            effect.action = SlotEffectAction.ProgressSlotClock;
            effect.actionValue = progressPerTrigger;

            var def = ScriptableObject.CreateInstance<SlotDefinition>();
            def.clock = new SlotClockConfig
            {
                maxValue = maxValue,
                completionEffect = completionEffect,
            };
            def.effects = new List<SlotEffectDefinition> { effect };
            return def;
        }

        private TurnController MakeTurnWithBoard(BoardState board)
        {
            return new TurnController(
                _playerDeck, _playerHand,
                _opponentDeck, board,
                _threatState, _opponentClock, _progression, _playedCardCounters,
                _statusCollection,
                _npcDeck,
                draftValue: 3,
                rng: new System.Random(42));
        }

        [Test]
        public void StartTurn_DrawNpcCard_fires_OnNpcCardDrawn_with_drawn_card()
        {
            var npcCard = ScriptableObject.CreateInstance<NpcCardDefinition>();
            npcCard.characterName = "TestNpc";

            var deckDefinition = ScriptableObject.CreateInstance<NpcDeckDefinition>();
            deckDefinition.cards = new List<NpcCardDefinition> { npcCard };
            deckDefinition.shuffleOnInit = false;
            _npcDeck = new NpcDeckState(deckDefinition, new System.Random(42));

            var slotDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.DrawNpcCard);
            var board = new BoardState(new SlotDefinition[] { slotDef, null, null });
            var turn = MakeTurnWithBoard(board);

            NpcCardDefinition drawn = null;
            turn.OnNpcCardDrawn += card => drawn = card;

            turn.StartTurn();

            Assert.AreSame(npcCard, drawn);
        }

        [Test]
        public void PlayCard_adds_current_value_to_global_counter_when_slot_uses_card_value_mode()
        {
            var def = ScriptableObject.CreateInstance<SlotDefinition>();
            def.contributesToGlobalPlayedCardCounts = true;
            def.playedCardCountMode = PlayedCardCountMode.UseCardValue;
            var board = new BoardState(new[] { def, null, null });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();
            var card = _playerHand.Cards.First();
            card.CurrentValue = 7;

            turn.PlayCard(card, slotIndex: 0);

            Assert.AreEqual(7, _playedCardCounters.Force);
            Assert.AreEqual(0, _playedCardCounters.Presence);
            Assert.AreEqual(0, _playedCardCounters.Wit);
        }

        [Test]
        public void PlayCard_adds_fixed_value_to_matching_global_counter_when_slot_uses_fixed_value_mode()
        {
            var def = ScriptableObject.CreateInstance<SlotDefinition>();
            def.contributesToGlobalPlayedCardCounts = true;
            def.playedCardCountMode = PlayedCardCountMode.UseFixedValue;
            def.fixedPlayedCardCountValue = 3;
            var board = new BoardState(new[] { def, null, null });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();
            var presenceCard = new CardInstance(MakeCardDef(CardType.Presence, 5));
            _playerHand.Add(presenceCard);

            turn.PlayCard(presenceCard, slotIndex: 0);

            Assert.AreEqual(3, _playedCardCounters.Presence);
            Assert.AreEqual(0, _playedCardCounters.Force);
            Assert.AreEqual(0, _playedCardCounters.Wit);
        }

        [Test]
        public void PlayCard_does_not_add_to_global_counter_when_slot_does_not_contribute()
        {
            var def = ScriptableObject.CreateInstance<SlotDefinition>();
            def.contributesToGlobalPlayedCardCounts = false;
            var board = new BoardState(new[] { def, null, null });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();
            var card = _playerHand.Cards.First();

            turn.PlayCard(card, slotIndex: 0);

            Assert.AreEqual(0, _playedCardCounters.Force);
            Assert.AreEqual(0, _playedCardCounters.Presence);
            Assert.AreEqual(0, _playedCardCounters.Wit);
        }

        [Test]
        public void StartTurn_DrawOpponentCard_fills_empty_slot_with_opponent_card()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.NoOpponentCard,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.DrawOpponentCard);
            var board = new BoardState(new SlotDefinition[] { def, null, null });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.IsNotNull(board.Slots[0].OpponentCard);
        }

        [Test]
        public void StartTurn_DrawOpponentCard_does_not_replace_existing_opponent_card()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.NoOpponentCard,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.DrawOpponentCard);
            var board = new BoardState(new SlotDefinition[] { def, null, null });
            var existing = new CardInstance(MakeCardDef(CardType.Presence, 5));
            board.Slots[0].OpponentCard = existing;
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.AreSame(existing, board.Slots[0].OpponentCard);
        }

        [Test]
        public void StartTurn_DrawOpponentCard_does_not_fill_slot_without_opponent_spot()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.NoOpponentCard,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.DrawOpponentCard);
            def.hasOpponentCardSpot = false;
            var board = new BoardState(new SlotDefinition[] { def, null, null });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.IsNull(board.Slots[0].OpponentCard);
        }

        [Test]
        public void StartTurn_DamageToThreat_drains_correct_bar()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.NoPlayerCard,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.DamageToThreat,
                actionValue: 3);
            def.effects[0].targetCardType = SlotEffectTargetCardType.Force;
            var board = new BoardState(new SlotDefinition[] { def, null, null });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.AreEqual(7, _threatState.GetBar(CardType.Force).Segments[0].CurrentValue);
        }

        [Test]
        public void StartTurn_active_DamageToThreat_status_drains_matching_threat_bar()
        {
            var definition = ScriptableObject.CreateInstance<StatusDefinition>();
            definition.durationType = StatusDurationType.Turns;
            definition.durationTurns = 2;
            definition.action = SlotEffectAction.DamageToThreat;
            definition.targetCardType = SlotEffectTargetCardType.Force;
            definition.actionValue = 3;

            _statusCollection.Add(definition);

            _turn.StartTurn();

            var bar = _threatState.GetBar(CardType.Force);
            Assert.AreEqual(7, bar.Segments[0].CurrentValue);
        }

        [Test]
        public void StartTurn_DamageToThreat_can_use_opponent_card_value_from_same_slot()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.DamageToThreat,
                actionValue: 1);
            def.effects[0].targetCardType = SlotEffectTargetCardType.Wit;
            def.effects[0].valueSource = SlotEffectValueSource.CardOnSlotValue;
            def.effects[0].slotCardTarget = SlotEffectSlotCardTarget.OpponentCard;

            var board = new BoardState(new SlotDefinition[] { def, null, null });
            board.Slots[0].OpponentCard = new CardInstance(MakeCardDef(CardType.Presence, 4));
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.AreEqual(6, _threatState.GetBar(CardType.Wit).Segments[0].CurrentValue);
        }

        [Test]
        public void StartTurn_DamageToThreat_can_use_player_card_value_from_same_slot()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.DamageToThreat,
                actionValue: 1);
            def.effects[0].targetCardType = SlotEffectTargetCardType.Presence;
            def.effects[0].valueSource = SlotEffectValueSource.CardOnSlotValue;
            def.effects[0].slotCardTarget = SlotEffectSlotCardTarget.PlayerCard;

            var board = new BoardState(new SlotDefinition[] { def, null, null });
            board.Slots[0].PlayerCard = new CardInstance(MakeCardDef(CardType.Force, 5));
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.AreEqual(5, _threatState.GetBar(CardType.Presence).Segments[0].CurrentValue);
        }

        [Test]
        public void StartTurn_DamageToThreat_can_use_opponent_card_type_from_same_slot()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.DamageToThreat,
                actionValue: 2);
            def.effects[0].targetCardType = SlotEffectTargetCardType.OpponentCard;

            var board = new BoardState(new SlotDefinition[] { def, null, null });
            board.Slots[0].OpponentCard = new CardInstance(MakeCardDef(CardType.Wit, 4));
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.AreEqual(8, _threatState.GetBar(CardType.Wit).Segments[0].CurrentValue);
        }

        [Test]
        public void StartTurn_DamageToThreat_does_not_trigger_when_slot_card_target_is_missing()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.DamageToThreat,
                actionValue: 1);
            def.effects[0].valueSource = SlotEffectValueSource.CardOnSlotValue;
            def.effects[0].slotCardTarget = SlotEffectSlotCardTarget.PlayerCard;
            def.effects[0].targetCardType = SlotEffectTargetCardType.PlayerCard;

            var board = new BoardState(new SlotDefinition[] { def, null, null });
            board.Slots[0].OpponentCard = new CardInstance(MakeCardDef(CardType.Wit, 4));
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.AreEqual(10, _threatState.GetBar(CardType.Force).Segments[0].CurrentValue);
            Assert.AreEqual(10, _threatState.GetBar(CardType.Presence).Segments[0].CurrentValue);
            Assert.AreEqual(10, _threatState.GetBar(CardType.Wit).Segments[0].CurrentValue);
        }

        [Test]
        public void StartTurn_AddToClock_increments_opponent_clock_when_no_player_card()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.NoPlayerCard,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.AddToClock,
                actionValue: 2);
            var board = new BoardState(new SlotDefinition[] { def, null, null });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.AreEqual(2, _opponentClock.CurrentValue);
        }

        [Test]
        public void PlayCard_ReturnCardsFromSlots_returns_all_board_cards_to_hand()
        {
            var drawSlotDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnCardPlayed,
                SlotEffectAction.ReturnCardsFromSlots);
            var board = new BoardState(new SlotDefinition[] { null, null, drawSlotDef });
            // manually place a card in slot 0 (simulating a card already on board)
            board.Slots[0].PlayerCard = new CardInstance(MakeCardDef(CardType.Force, 3));
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn(); // draws 3 cards
            var handCard = _playerHand.Cards[0];
            turn.PlayCard(handCard, slotIndex: 2);

            // slot 0 card returned, slot 2 played card returned
            Assert.IsNull(board.Slots[0].PlayerCard);
            Assert.IsNull(board.Slots[2].PlayerCard);
            // hand: 2 remaining + 1 from slot 0 + 1 played card returned = 4
            Assert.AreEqual(4, _playerHand.Cards.Count);
        }

        [Test]
        public void PlayCard_DrawToHandLimit_draws_up_to_draft_value()
        {
            var drawSlotDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnCardPlayed,
                SlotEffectAction.DrawToHandLimit);
            var board = new BoardState(new SlotDefinition[] { null, null, drawSlotDef });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn(); // draws 3 cards
            // consume 2 cards to lower hand count
            var card0 = _playerHand.Cards[0];
            var card1 = _playerHand.Cards[1];
            turn.PlayCard(card0, slotIndex: 0);
            turn.PlayCard(card1, slotIndex: 1);
            // hand now has 1 card

            var handCard = _playerHand.Cards[0];
            turn.PlayCard(handCard, slotIndex: 2); // plays to draw slot

            // hand had 0 after playing, DrawToHandLimit draws 3
            Assert.AreEqual(3, _playerHand.Cards.Count);
        }

        [Test]
        public void StartTurn_IncreaseOpponentCardsOfTypeValue_increases_only_matching_opponent_cards()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.IncreaseOpponentCardsOfTypeValue,
                actionValue: 2);
            def.effects[0].targetCardType = SlotEffectTargetCardType.Presence;

            var board = new BoardState(new SlotDefinition[] { def, null, null });
            board.Slots[1].OpponentCard = new CardInstance(MakeCardDef(CardType.Presence, 4));
            board.Slots[2].OpponentCard = new CardInstance(MakeCardDef(CardType.Wit, 5));
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.AreEqual(6, board.Slots[1].OpponentCard.CurrentValue);
            Assert.AreEqual(5, board.Slots[2].OpponentCard.CurrentValue);
        }

        [Test]
        public void StartTurn_IncreaseOpponentCardsOfTypeValue_increases_matching_card_on_effect_slot_too()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.IncreaseOpponentCardsOfTypeValue,
                actionValue: 3);
            def.effects[0].targetCardType = SlotEffectTargetCardType.Presence;

            var board = new BoardState(new SlotDefinition[] { def, null, null });
            board.Slots[0].OpponentCard = new CardInstance(MakeCardDef(CardType.Presence, 2));
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.AreEqual(5, board.Slots[0].OpponentCard.CurrentValue);
        }

        [Test]
        public void EndTurn_IncreaseAppearingOpponentCardOfTypeValue_buffs_only_newly_placed_matching_card()
        {
            var effectSlotDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnOpponentCardPlaced,
                SlotEffectAction.IncreaseAppearingOpponentCardOfTypeValue,
                actionValue: 2);
            effectSlotDef.effects[0].targetCardType = SlotEffectTargetCardType.Presence;

            var board = new BoardState(new SlotDefinition[] { effectSlotDef, null, null });
            var existingCard = new CardInstance(MakeCardDef(CardType.Presence, 7));
            board.Slots[0].OpponentCard = existingCard;
            var turn = MakeTurnWithBoard(board);

            turn.EndTurn();

            Assert.AreEqual(7, board.Slots[0].OpponentCard.CurrentValue);
            Assert.AreEqual(6, board.Slots[1].OpponentCard.CurrentValue);
            Assert.AreEqual(6, board.Slots[2].OpponentCard.CurrentValue);
        }

        [Test]
        public void StartTurn_DrawOpponentCard_can_trigger_IncreaseAppearingOpponentCardOfTypeValue_once()
        {
            var drawSlotDef = MakeSlotDefinition(
                SlotEffectContext.NoOpponentCard,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.DrawOpponentCard);
            drawSlotDef.hasOpponentCardSpot = true;

            var passiveBuffSlotDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnOpponentCardPlaced,
                SlotEffectAction.IncreaseAppearingOpponentCardOfTypeValue,
                actionValue: 3);
            passiveBuffSlotDef.effects[0].targetCardType = SlotEffectTargetCardType.Presence;

            var board = new BoardState(new SlotDefinition[] { drawSlotDef, passiveBuffSlotDef, null });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.AreEqual(7, board.Slots[0].OpponentCard.CurrentValue);
        }

        [Test]
        public void EndTurn_IncreaseAppearingOpponentCardOfTypeValue_does_not_buff_non_matching_card()
        {
            var effectSlotDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnOpponentCardPlaced,
                SlotEffectAction.IncreaseAppearingOpponentCardOfTypeValue,
                actionValue: 2);
            effectSlotDef.effects[0].targetCardType = SlotEffectTargetCardType.Force;

            var board = new BoardState(new SlotDefinition[] { effectSlotDef, null, null });
            var turn = MakeTurnWithBoard(board);

            turn.EndTurn();

            Assert.AreEqual(4, board.Slots[1].OpponentCard.CurrentValue);
            Assert.AreEqual(4, board.Slots[2].OpponentCard.CurrentValue);
        }

        [Test]
        public void PlayCard_ModifyPlayerCardsOfTypeValue_changes_matching_player_cards_in_hand_and_on_board()
        {
            var effectSlotDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnCardPlayed,
                SlotEffectAction.ModifyPlayerCardsOfTypeValue,
                actionValue: -1);
            effectSlotDef.effects[0].targetCardType = SlotEffectTargetCardType.Force;

            var board = new BoardState(new SlotDefinition[] { null, effectSlotDef, null });
            var turn = MakeTurnWithBoard(board);

            var boardForceCard = new CardInstance(MakeCardDef(CardType.Force, 6));
            var boardPresenceCard = new CardInstance(MakeCardDef(CardType.Presence, 8));
            board.Slots[0].PlayerCard = boardForceCard;
            board.Slots[2].PlayerCard = boardPresenceCard;

            var triggeringForceCard = new CardInstance(MakeCardDef(CardType.Force, 5));
            var handForceCard = new CardInstance(MakeCardDef(CardType.Force, 4));
            var handWitCard = new CardInstance(MakeCardDef(CardType.Wit, 7));
            _playerHand.Add(triggeringForceCard);
            _playerHand.Add(handForceCard);
            _playerHand.Add(handWitCard);

            turn.PlayCard(triggeringForceCard, slotIndex: 1);

            Assert.AreEqual(5, boardForceCard.CurrentValue);
            Assert.AreEqual(4, board.Slots[1].PlayerCard.CurrentValue);
            Assert.AreEqual(3, handForceCard.CurrentValue);
            Assert.AreEqual(8, boardPresenceCard.CurrentValue);
            Assert.AreEqual(7, handWitCard.CurrentValue);
        }

        [Test]
        public void PlayCard_ModifyPlayerCardsOfTypeValue_discards_matching_player_cards_that_reach_zero()
        {
            var effectSlotDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnCardPlayed,
                SlotEffectAction.ModifyPlayerCardsOfTypeValue,
                actionValue: -2);
            effectSlotDef.effects[0].targetCardType = SlotEffectTargetCardType.Force;

            var board = new BoardState(new SlotDefinition[] { null, effectSlotDef, null });
            var turn = MakeTurnWithBoard(board);

            var boardForceCard = new CardInstance(MakeCardDef(CardType.Force, 2));
            var survivingPresenceCard = new CardInstance(MakeCardDef(CardType.Presence, 5));
            board.Slots[0].PlayerCard = boardForceCard;
            board.Slots[2].PlayerCard = survivingPresenceCard;

            var triggeringForceCard = new CardInstance(MakeCardDef(CardType.Force, 2));
            var handForceCard = new CardInstance(MakeCardDef(CardType.Force, 1));
            var handWitCard = new CardInstance(MakeCardDef(CardType.Wit, 6));
            _playerHand.Add(triggeringForceCard);
            _playerHand.Add(handForceCard);
            _playerHand.Add(handWitCard);

            int discardCountBefore = _playerDeck.DiscardCount;

            turn.PlayCard(triggeringForceCard, slotIndex: 1);

            Assert.AreEqual(discardCountBefore + 3, _playerDeck.DiscardCount);
            Assert.IsNull(board.Slots[0].PlayerCard);
            Assert.IsNull(board.Slots[1].PlayerCard);
            Assert.That(_playerHand.Cards, Has.No.Member(handForceCard));
            Assert.That(_playerHand.Cards, Has.Member(handWitCard));
            Assert.AreSame(survivingPresenceCard, board.Slots[2].PlayerCard);
        }

        [Test]
        public void EndTurn_ModifyPlayerCardsOfTypeValue_changes_matching_player_cards_before_returning_them_to_hand()
        {
            var effectSlotDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnRoundEnd,
                SlotEffectAction.ModifyPlayerCardsOfTypeValue,
                actionValue: 2);
            effectSlotDef.effects[0].targetCardType = SlotEffectTargetCardType.Force;

            var board = new BoardState(new SlotDefinition[] { effectSlotDef, null, null });
            var turn = MakeTurnWithBoard(board);

            var boardForceCard = new CardInstance(MakeCardDef(CardType.Force, 3));
            var boardWitCard = new CardInstance(MakeCardDef(CardType.Wit, 9));
            board.Slots[0].PlayerCard = boardForceCard;
            board.Slots[1].PlayerCard = boardWitCard;

            var handForceCard = new CardInstance(MakeCardDef(CardType.Force, 4));
            var handPresenceCard = new CardInstance(MakeCardDef(CardType.Presence, 6));
            _playerHand.Add(handForceCard);
            _playerHand.Add(handPresenceCard);

            turn.EndTurn();

            Assert.AreEqual(5, boardForceCard.CurrentValue);
            Assert.AreEqual(6, handForceCard.CurrentValue);
            Assert.AreEqual(9, boardWitCard.CurrentValue);
            Assert.AreEqual(6, handPresenceCard.CurrentValue);
            Assert.That(_playerHand.Cards, Does.Contain(boardForceCard));
            Assert.That(_playerHand.Cards, Does.Contain(boardWitCard));
        }

        [Test]
        public void PlayCard_does_not_place_card_on_slot_without_player_spot()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnCardPlayed,
                SlotEffectAction.DrawToHandLimit);
            def.hasPlayerCardSpot = false;
            var board = new BoardState(new SlotDefinition[] { def, null, null });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();
            var card = _playerHand.Cards[0];

            turn.PlayCard(card, slotIndex: 0);

            Assert.That(_playerHand.Cards, Does.Contain(card));
            Assert.IsNull(board.Slots[0].PlayerCard);
        }

        [Test]
        public void PlayCard_TurnEnd_returns_player_cards_and_places_opponent_cards()
        {
            var turnEndSlotDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnCardPlayed,
                SlotEffectAction.TurnEnd);
            var board = new BoardState(new SlotDefinition[] { null, null, turnEndSlotDef });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();
            var firstCard = _playerHand.Cards[0];
            var triggerCard = _playerHand.Cards[1];
            turn.PlayCard(firstCard, slotIndex: 0);

            turn.PlayCard(triggerCard, slotIndex: 2);

            Assert.IsTrue(turn.ConsumeTurnEndedByEffect());
            Assert.IsNull(board.Slots[0].PlayerCard);
            Assert.IsNull(board.Slots[2].PlayerCard);
            Assert.That(_playerHand.Cards, Does.Contain(firstCard));
            Assert.That(_playerHand.Cards, Does.Contain(triggerCard));
            Assert.Greater(board.Slots.Count(s => s.HasOpponentCard), 0);
        }

        [Test]
        public void PlayCard_TurnEnd_also_triggers_OnRoundEnd_effects()
        {
            var endRoundEffectDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnRoundEnd,
                SlotEffectAction.AddToClock,
                actionValue: 1);
            var turnEndSlotDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnCardPlayed,
                SlotEffectAction.TurnEnd);
            var board = new BoardState(new[] { endRoundEffectDef, null, turnEndSlotDef });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();
            turn.PlayCard(_playerHand.Cards[0], slotIndex: 2);

            Assert.AreEqual(1, _opponentClock.CurrentValue);
        }

        [Test]
        public void ConsumeTurnEndedByEffect_returns_false_after_state_is_consumed()
        {
            var turnEndSlotDef = MakeSlotDefinition(
                SlotEffectContext.Passive,
                SlotEffectTrigger.OnCardPlayed,
                SlotEffectAction.TurnEnd);
            var board = new BoardState(new SlotDefinition[] { null, null, turnEndSlotDef });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();
            turn.PlayCard(_playerHand.Cards[0], slotIndex: 2);

            Assert.IsTrue(turn.ConsumeTurnEndedByEffect());
            Assert.IsFalse(turn.ConsumeTurnEndedByEffect());
        }

        private static ThreatState MakeThreatState(int maxPerBar)
        {
            var tier = ScriptableObject.CreateInstance<ThreatTierDefinition>();
            tier.maxValue = maxPerBar;

            var tiers = new List<ThreatTierDefinition> { tier };
            return new ThreatState(tiers, tiers, tiers);
        }
    }
}
