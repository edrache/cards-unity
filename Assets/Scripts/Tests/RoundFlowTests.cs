using System.Collections.Generic;
using CardsUnity.Data;
using CardsUnity.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CardsUnity.Tests
{
    public sealed class RoundFlowTests
    {
        // ── DrawCards ──────────────────────────────────────────────────────────

        [Test]
        public void DrawCards_FillsHandToEmptyBoardSlotCount()
        {
            GameState state = MakeState(slotCount: 3);
            AddToDeck(state, 5);

            RoundFlow.DrawCards(state);

            Assert.AreEqual(3, state.PlayerHand.Count);
            Assert.AreEqual(2, state.PlayerDeck.Count);
        }

        [Test]
        public void DrawCards_PartialDeck_DrawsOnlyAvailable()
        {
            GameState state = MakeState(slotCount: 3);
            AddToDeck(state, 2);

            RoundFlow.DrawCards(state);

            Assert.AreEqual(2, state.PlayerHand.Count);
            Assert.AreEqual(0, state.PlayerDeck.Count);
        }

        [Test]
        public void DrawCards_DoesNotExceedEmptySlotTarget()
        {
            // Board has 3 slots. Fill 1 slot so only 2 are empty.
            GameState state = MakeState(slotCount: 3);
            AddToDeck(state, 5);
            CardInstance occupier = MakeCard();
            state.PlayerBoard[0] = occupier;

            RoundFlow.DrawCards(state);

            // Target = 2 empty slots → draw 2 cards into hand.
            Assert.AreEqual(2, state.PlayerHand.Count);
        }

        [Test]
        public void DrawCards_ClearsPlacementOrder()
        {
            GameState state = MakeState(slotCount: 3);
            AddToDeck(state, 3);
            state.PlacementOrder.Add(MakeCard());

            RoundFlow.DrawCards(state);

            Assert.IsEmpty(state.PlacementOrder);
        }

        [Test]
        public void DrawCards_SetsPhaseToPlacement()
        {
            GameState state = MakeState(slotCount: 3);
            AddToDeck(state, 1);

            RoundFlow.DrawCards(state);

            Assert.AreEqual(GamePhase.Placement, state.Phase);
        }

        // ── PlaceCard ──────────────────────────────────────────────────────────

        [Test]
        public void PlaceCard_MovesCardFromHandToBoard()
        {
            GameState state = MakeState(slotCount: 3);
            CardInstance card = MakeCard();
            state.PlayerHand.Add(card);

            bool result = RoundFlow.PlaceCard(state, card, 1);

            Assert.IsTrue(result);
            Assert.IsFalse(state.PlayerHand.Contains(card));
            Assert.AreSame(card, state.PlayerBoard[1]);
        }

        [Test]
        public void PlaceCard_AppendsToPlacementOrder()
        {
            GameState state = MakeState(slotCount: 3);
            CardInstance cardA = MakeCard();
            CardInstance cardB = MakeCard();
            state.PlayerHand.Add(cardA);
            state.PlayerHand.Add(cardB);

            RoundFlow.PlaceCard(state, cardA, 0);
            RoundFlow.PlaceCard(state, cardB, 2);

            Assert.AreEqual(new[] { cardA, cardB }, state.PlacementOrder);
        }

        [Test]
        public void PlaceCard_OccupiedSlot_ReturnsFalse()
        {
            GameState state = MakeState(slotCount: 3);
            CardInstance cardA = MakeCard();
            CardInstance cardB = MakeCard();
            state.PlayerHand.Add(cardA);
            state.PlayerHand.Add(cardB);
            state.PlayerBoard[0] = cardA;

            bool result = RoundFlow.PlaceCard(state, cardB, 0);

            Assert.IsFalse(result);
            Assert.IsTrue(state.PlayerHand.Contains(cardB));
        }

        [Test]
        public void PlaceCard_CardNotInHand_ReturnsFalse()
        {
            GameState state = MakeState(slotCount: 3);
            CardInstance card = MakeCard();

            bool result = RoundFlow.PlaceCard(state, card, 0);

            Assert.IsFalse(result);
            Assert.IsNull(state.PlayerBoard[0]);
        }

        // ── UnplaceCard ────────────────────────────────────────────────────────

        [Test]
        public void UnplaceCard_ReturnsCardToHand()
        {
            GameState state = MakeState(slotCount: 3);
            CardInstance card = MakeCard();
            state.PlayerHand.Add(card);
            RoundFlow.PlaceCard(state, card, 1);

            bool result = RoundFlow.UnplaceCard(state, 1);

            Assert.IsTrue(result);
            Assert.IsNull(state.PlayerBoard[1]);
            Assert.IsTrue(state.PlayerHand.Contains(card));
        }

        [Test]
        public void UnplaceCard_RemovesFromPlacementOrder()
        {
            GameState state = MakeState(slotCount: 3);
            CardInstance cardA = MakeCard();
            CardInstance cardB = MakeCard();
            state.PlayerHand.Add(cardA);
            state.PlayerHand.Add(cardB);
            RoundFlow.PlaceCard(state, cardA, 0);
            RoundFlow.PlaceCard(state, cardB, 2);

            RoundFlow.UnplaceCard(state, 0);

            CollectionAssert.DoesNotContain(state.PlacementOrder, cardA);
            CollectionAssert.Contains(state.PlacementOrder, cardB);
        }

        [Test]
        public void UnplaceCard_EmptySlot_ReturnsFalse()
        {
            GameState state = MakeState(slotCount: 3);

            bool result = RoundFlow.UnplaceCard(state, 0);

            Assert.IsFalse(result);
        }

        // ── CanResolve ─────────────────────────────────────────────────────────

        [Test]
        public void CanResolve_AllSlotsFilled_ReturnsTrue()
        {
            GameState state = MakeState(slotCount: 3);
            state.PlayerBoard[0] = MakeCard();
            state.PlayerBoard[1] = MakeCard();
            state.PlayerBoard[2] = MakeCard();
            state.PlayerHand.Add(MakeCard());

            Assert.IsTrue(RoundFlow.CanResolve(state));
        }

        [Test]
        public void CanResolve_HandEmpty_ReturnsTrue()
        {
            GameState state = MakeState(slotCount: 3);
            // Hand is empty, board partially filled — still allowed when deck runs dry.
            state.PlayerBoard[0] = MakeCard();

            Assert.IsTrue(RoundFlow.CanResolve(state));
        }

        [Test]
        public void CanResolve_HandNotEmptyAndBoardHasEmptySlot_ReturnsFalse()
        {
            GameState state = MakeState(slotCount: 3);
            state.PlayerBoard[0] = MakeCard();
            state.PlayerBoard[1] = MakeCard();
            state.PlayerHand.Add(MakeCard());  // hand not empty, slot 2 open

            Assert.IsFalse(RoundFlow.CanResolve(state));
        }

        // ── GetResolutionOrder ─────────────────────────────────────────────────

        [Test]
        public void GetResolutionOrder_FollowsPlacementOrder()
        {
            GameState state = MakeState(slotCount: 3);
            CardInstance cardA = PlaceOnBoth(state, 2);
            CardInstance cardB = PlaceOnBoth(state, 0);
            CardInstance cardC = PlaceOnBoth(state, 1);
            state.PlacementOrder.AddRange(new[] { cardA, cardB, cardC });

            List<int> order = RoundFlow.GetResolutionOrder(state);

            Assert.AreEqual(new[] { 2, 0, 1 }, order);
        }

        [Test]
        public void GetResolutionOrder_SkipsEmptyEnemySlots()
        {
            GameState state = MakeState(slotCount: 3);
            CardInstance cardA = MakeCard();
            CardInstance cardB = MakeCard();
            state.PlayerBoard[0] = cardA;
            state.PlayerBoard[2] = cardB;
            state.EnemyBoard[2] = MakeCard();   // only slot 2 has enemy
            state.PlacementOrder.AddRange(new[] { cardA, cardB });

            List<int> order = RoundFlow.GetResolutionOrder(state);

            Assert.AreEqual(new[] { 2 }, order);
        }

        [Test]
        public void GetResolutionOrder_AppendsRemainingContestedSlotsInIndexOrder()
        {
            GameState state = MakeState(slotCount: 3);
            CardInstance cardA = PlaceOnBoth(state, 2);
            CardInstance cardB = PlaceOnBoth(state, 0);
            // cardB at slot 0 placed first but only cardA in placementOrder
            state.PlacementOrder.Add(cardA);

            List<int> order = RoundFlow.GetResolutionOrder(state);

            // cardA (slot 2) from placement order first, then slot 0 appended
            Assert.AreEqual(new[] { 2, 0 }, order);
        }

        [Test]
        public void GetResolutionOrder_EmptyBoards_ReturnsEmptyList()
        {
            GameState state = MakeState(slotCount: 3);

            List<int> order = RoundFlow.GetResolutionOrder(state);

            Assert.IsEmpty(order);
        }

        // ── FillEnemyBoard ─────────────────────────────────────────────────────

        [Test]
        public void FillEnemyBoard_FillsFromDeckLeftToRight()
        {
            GameState state = MakeState(slotCount: 3);
            CardInstance a = MakeCard(), b = MakeCard(), c = MakeCard();
            state.EnemyDeck.AddRange(new[] { a, b, c });

            RoundFlow.FillEnemyBoard(state);

            Assert.AreSame(a, state.EnemyBoard[0]);
            Assert.AreSame(b, state.EnemyBoard[1]);
            Assert.AreSame(c, state.EnemyBoard[2]);
            Assert.IsEmpty(state.EnemyDeck);
        }

        [Test]
        public void FillEnemyBoard_DoesNotOverwriteOccupiedSlots()
        {
            GameState state = MakeState(slotCount: 3);
            CardInstance existing = MakeCard();
            state.EnemyBoard[1] = existing;
            CardInstance newCard = MakeCard();
            state.EnemyDeck.Add(newCard);

            RoundFlow.FillEnemyBoard(state);

            Assert.AreSame(newCard, state.EnemyBoard[0]);
            Assert.AreSame(existing, state.EnemyBoard[1]);
            Assert.IsNull(state.EnemyBoard[2]);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static GameState MakeState(int slotCount) => new GameState(slotCount);

        private static CardInstance MakeCard()
        {
            CardData data = ScriptableObject.CreateInstance<CardData>();
            var so = new SerializedObject(data);
            so.FindProperty("id").stringValue       = System.Guid.NewGuid().ToString();
            so.FindProperty("cardName").stringValue = "Test";
            so.FindProperty("rps").enumValueIndex   = 0;
            so.FindProperty("value").intValue       = 5;
            so.FindProperty("role").enumValueIndex  = 0;
            so.FindProperty("owner").enumValueIndex = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            return new CardInstance(data);
        }

        private static void AddToDeck(GameState state, int count)
        {
            for (int i = 0; i < count; i++)
                state.PlayerDeck.Add(MakeCard());
        }

        // Places a card on both boards at the given index (for contested slots).
        private static CardInstance PlaceOnBoth(GameState state, int slotIndex)
        {
            CardInstance player = MakeCard();
            CardInstance enemy  = MakeCard();
            state.PlayerBoard[slotIndex] = player;
            state.EnemyBoard[slotIndex]  = enemy;
            return player;
        }
    }
}
