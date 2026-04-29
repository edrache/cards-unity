using System.Linq;
using NUnit.Framework;
using CardsUnity;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class TurnControllerTests
    {
        private TurnController _turn;
        private DeckState _playerDeck;
        private HandState _playerHand;
        private DeckState _opponentDeck;
        private BoardState _board;
        private ClockState _playerClock;
        private ClockState _opponentClock;

        [SetUp]
        public void SetUp()
        {
            _playerDeck = MakeDeck(6, CardType.Pressure, value: 3);
            _playerHand = new HandState();
            _opponentDeck = MakeDeck(4, CardType.Appeal, value: 4);
            _board = new BoardState(slotCount: 3);
            _playerClock = new ClockState(maxValue: 6);
            _opponentClock = new ClockState(maxValue: 4);

            _turn = new TurnController(
                _playerDeck, _playerHand,
                _opponentDeck, _board,
                _playerClock, _opponentClock,
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
            _playerHand.Add(ScriptableObject.CreateInstance<CardDefinition>());
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
            Assert.AreEqual(card, _board.Slots[0].PlayerCard?.Definition);
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
            var opponentCard = new CardInstance(MakeCardDef(CardType.Appeal, value: 10));
            _board.Slots[1].OpponentCard = opponentCard;

            var playerCard = _playerHand.Cards.First(c => c.type == CardType.Pressure);
            _turn.PlayCard(playerCard, slotIndex: 1);

            Assert.AreEqual(7, opponentCard.CurrentValue);
        }

        [Test]
        public void PlayCard_increments_opponent_clock_when_card_destroyed()
        {
            _turn.StartTurn();
            _board.Slots[0].OpponentCard = new CardInstance(MakeCardDef(CardType.Appeal, value: 1));

            var playerCard = _playerHand.Cards.First(c => c.type == CardType.Pressure);
            _turn.PlayCard(playerCard, slotIndex: 0);

            Assert.AreEqual(1, _opponentClock.CurrentValue);
        }

        [Test]
        public void PlayCard_applies_counter_damage_to_player_card()
        {
            _turn.StartTurn();
            _board.Slots[0].OpponentCard = new CardInstance(MakeCardDef(CardType.Positioning, value: 1));

            var playerCard = _playerHand.Cards.First(c => c.type == CardType.Pressure);
            _turn.PlayCard(playerCard, slotIndex: 0);

            Assert.AreEqual(2, _board.Slots[0].PlayerCard?.CurrentValue);
        }

        [Test]
        public void PlayCard_increments_player_clock_when_player_card_is_destroyed_by_counterattack()
        {
            _turn.StartTurn();
            _board.Slots[0].OpponentCard = new CardInstance(MakeCardDef(CardType.Positioning, value: 10));

            var playerCard = _playerHand.Cards.First(c => c.type == CardType.Pressure);
            _turn.PlayCard(playerCard, slotIndex: 0);

            Assert.AreEqual(1, _playerClock.CurrentValue);
            Assert.IsNull(_board.Slots[0].PlayerCard);
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
            int actionValue = 1,
            ClockTarget clockTarget = ClockTarget.PlayerClock)
        {
            var effect = ScriptableObject.CreateInstance<SlotEffectDefinition>();
            effect.context = context;
            effect.trigger = trigger;
            effect.action = action;
            effect.actionValue = actionValue;
            effect.clockTarget = clockTarget;

            var def = ScriptableObject.CreateInstance<SlotDefinition>();
            def.effects = new System.Collections.Generic.List<SlotEffectDefinition> { effect };
            return def;
        }

        private TurnController MakeTurnWithBoard(BoardState board)
        {
            return new TurnController(
                _playerDeck, _playerHand,
                _opponentDeck, board,
                _playerClock, _opponentClock,
                draftValue: 3,
                rng: new System.Random(42));
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
            var existing = new CardInstance(MakeCardDef(CardType.Appeal, 5));
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
        public void StartTurn_AddToClock_increments_player_clock_when_no_player_card()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.NoPlayerCard,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.AddToClock,
                actionValue: 2,
                clockTarget: ClockTarget.PlayerClock);
            var board = new BoardState(new SlotDefinition[] { def, null, null });
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.AreEqual(2, _playerClock.CurrentValue);
        }

        [Test]
        public void StartTurn_AddToClock_does_not_fire_when_player_card_present()
        {
            var def = MakeSlotDefinition(
                SlotEffectContext.NoPlayerCard,
                SlotEffectTrigger.OnPlayerTurnStart,
                SlotEffectAction.AddToClock,
                actionValue: 2,
                clockTarget: ClockTarget.PlayerClock);
            var board = new BoardState(new SlotDefinition[] { def, null, null });
            board.Slots[0].PlayerCard = new CardInstance(MakeCardDef(CardType.Pressure, 3));
            var turn = MakeTurnWithBoard(board);

            turn.StartTurn();

            Assert.AreEqual(0, _playerClock.CurrentValue);
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
            board.Slots[0].PlayerCard = new CardInstance(MakeCardDef(CardType.Pressure, 3));
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
    }
}
