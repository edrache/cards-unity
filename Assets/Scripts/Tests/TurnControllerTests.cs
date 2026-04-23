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
    }
}
