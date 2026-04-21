using System;
using System.Collections.Generic;
using CardsUnity.Combat;
using CardsUnity.Data;
using CardsUnity.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace CardsUnity.Tests
{
    public sealed class CombatFlowTests
    {
        // ── BuildQueue ─────────────────────────────────────────────────────────

        [Test]
        public void BuildQueue_ReturnsContestedSlotsInPlacementOrder()
        {
            GameState state = MakeState(3);
            CardInstance pA = PlaceOnBoth(state, 2);
            CardInstance pB = PlaceOnBoth(state, 0);
            state.PlacementOrder.AddRange(new[] { pA, pB });

            Queue<int> queue = CombatFlow.BuildQueue(state);

            Assert.AreEqual(2, queue.Dequeue());
            Assert.AreEqual(0, queue.Dequeue());
            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void BuildQueue_EmptyBoards_ReturnsEmptyQueue()
        {
            GameState state = MakeState(3);
            Assert.AreEqual(0, CombatFlow.BuildQueue(state).Count);
        }

        // ── ResolveStep ────────────────────────────────────────────────────────

        [Test]
        public void ResolveStep_ReturnsResultForContested()
        {
            GameState state = MakeState(3);
            PlaceOnBoth(state, 1);
            var queue = new Queue<int>(new[] { 1 });

            CombatResult result = CombatFlow.ResolveStep(state, queue, new Random(0));

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.SlotIndex);
        }

        [Test]
        public void ResolveStep_EmptyQueue_ReturnsNull()
        {
            GameState state = MakeState(3);
            var queue = new Queue<int>();

            Assert.IsNull(CombatFlow.ResolveStep(state, queue, new Random(0)));
        }

        [Test]
        public void ResolveStep_MutatesCardValues()
        {
            GameState state = MakeState(3);
            CardInstance player = MakeCard(value: 5, rps: RpsType.Pressure);
            CardInstance enemy  = MakeCard(value: 5, rps: RpsType.Positioning); // Pressure wins
            state.PlayerBoard[0] = player;
            state.EnemyBoard[0]  = enemy;
            var queue = new Queue<int>(new[] { 0 });

            CombatFlow.ResolveStep(state, queue, new Random(42));

            // At least one card took damage (advantage roll for player).
            Assert.IsTrue(player.Value < 5 || enemy.Value < 5);
        }

        [Test]
        public void ResolveStep_SkipsSlotWithMissingCard()
        {
            GameState state = MakeState(3);
            PlaceOnBoth(state, 2);
            // Slot 0 has no player card, slot 2 has both.
            var queue = new Queue<int>(new[] { 0, 2 });

            CombatResult result = CombatFlow.ResolveStep(state, queue, new Random(0));

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.SlotIndex);
        }

        // ── FinalizeRound ──────────────────────────────────────────────────────

        [Test]
        public void FinalizeRound_DeadPlayerCard_MovesToCemetery()
        {
            GameState state = MakeState(3);
            CardInstance dead = MakeCard(value: 1);
            dead.Value = 0;
            state.PlayerBoard[0] = dead;

            CombatFlow.FinalizeRound(state);

            Assert.IsNull(state.PlayerBoard[0]);
            CollectionAssert.Contains(state.PlayerCemetery, dead);
            CollectionAssert.DoesNotContain(state.PlayerHand, dead);
        }

        [Test]
        public void FinalizeRound_SurvivingPlayerCard_ReturnsToHand()
        {
            GameState state = MakeState(3);
            CardInstance alive = MakeCard(value: 5);
            state.PlayerBoard[1] = alive;

            CombatFlow.FinalizeRound(state);

            Assert.IsNull(state.PlayerBoard[1]);
            CollectionAssert.Contains(state.PlayerHand, alive);
            CollectionAssert.DoesNotContain(state.PlayerCemetery, alive);
        }

        [Test]
        public void FinalizeRound_DeadEnemyCard_MovesToCemetery()
        {
            GameState state = MakeState(3);
            CardInstance dead = MakeCard(value: 1);
            dead.Value = 0;
            state.EnemyBoard[0] = dead;

            CombatFlow.FinalizeRound(state);

            Assert.IsNull(state.EnemyBoard[0]);
            CollectionAssert.Contains(state.EnemyCemetery, dead);
        }

        [Test]
        public void FinalizeRound_SurvivingEnemyCard_StaysOnBoard()
        {
            GameState state = MakeState(3);
            CardInstance alive = MakeCard(value: 5);
            state.EnemyBoard[2] = alive;

            CombatFlow.FinalizeRound(state);

            Assert.AreSame(alive, state.EnemyBoard[2]);
        }

        [Test]
        public void FinalizeRound_RefillsEnemyBoardFromDeck()
        {
            GameState state = MakeState(3);
            CardInstance refill = MakeCard();
            state.EnemyDeck.Add(refill);

            CombatFlow.FinalizeRound(state);

            Assert.AreSame(refill, state.EnemyBoard[0]);
        }

        [Test]
        public void FinalizeRound_IncrementsRound()
        {
            GameState state = MakeState(3);
            int before = state.Round;

            CombatFlow.FinalizeRound(state);

            Assert.AreEqual(before + 1, state.Round);
        }

        [Test]
        public void FinalizeRound_DoesNotSetPhase()
        {
            GameState state = MakeState(3);
            state.Phase = GamePhase.Combat;

            CombatFlow.FinalizeRound(state);

            Assert.AreEqual(GamePhase.Combat, state.Phase);
        }

        // ── DetermineNextPhase ─────────────────────────────────────────────────

        [Test]
        public void DetermineNextPhase_PlayerHasNoCards_ReturnsEnd()
        {
            GameState state = MakeState(3);
            // player hand, deck, board all empty after FinalizeRound clears board
            state.EnemyDeck.Add(MakeCard());

            Assert.AreEqual(GamePhase.End, CombatFlow.DetermineNextPhase(state));
        }

        [Test]
        public void DetermineNextPhase_EnemyHasNoCards_ReturnsReward()
        {
            GameState state = MakeState(3);
            state.PlayerHand.Add(MakeCard());
            // enemy deck empty and board empty

            Assert.AreEqual(GamePhase.Reward, CombatFlow.DetermineNextPhase(state));
        }

        [Test]
        public void DetermineNextPhase_EnemyHasCardsOnBoard_ReturnsDraw()
        {
            GameState state = MakeState(3);
            state.PlayerHand.Add(MakeCard());
            state.EnemyBoard[0] = MakeCard();

            Assert.AreEqual(GamePhase.Draw, CombatFlow.DetermineNextPhase(state));
        }

        [Test]
        public void DetermineNextPhase_EnemyHasCardsInDeck_ReturnsDraw()
        {
            GameState state = MakeState(3);
            state.PlayerHand.Add(MakeCard());
            state.EnemyDeck.Add(MakeCard());

            Assert.AreEqual(GamePhase.Draw, CombatFlow.DetermineNextPhase(state));
        }

        [Test]
        public void DetermineNextPhase_PlayerLossBeatsEnemyEmpty()
        {
            // Player has no cards — loss takes priority over empty enemy.
            GameState state = MakeState(3);

            Assert.AreEqual(GamePhase.End, CombatFlow.DetermineNextPhase(state));
        }

        // ── Cleanup integration ────────────────────────────────────────────────

        [Test]
        public void FinalizeRound_SimultaneousDeath_BothGoToCemetery()
        {
            GameState state = MakeState(3);
            CardInstance player = MakeCard(value: 1);
            CardInstance enemy  = MakeCard(value: 1);
            player.Value = 0;
            enemy.Value  = 0;
            state.PlayerBoard[0] = player;
            state.EnemyBoard[0]  = enemy;

            CombatFlow.FinalizeRound(state);

            CollectionAssert.Contains(state.PlayerCemetery, player);
            CollectionAssert.Contains(state.EnemyCemetery, enemy);
            Assert.IsNull(state.PlayerBoard[0]);
            Assert.IsNull(state.EnemyBoard[0]);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static GameState MakeState(int slotCount) => new GameState(slotCount);

        private static CardInstance MakeCard(int value = 5, RpsType rps = RpsType.Pressure)
        {
            CardData data = ScriptableObject.CreateInstance<CardData>();
            var so = new SerializedObject(data);
            so.FindProperty("id").stringValue       = Guid.NewGuid().ToString();
            so.FindProperty("cardName").stringValue = "Test";
            so.FindProperty("rps").enumValueIndex   = (int)rps;
            so.FindProperty("value").intValue       = value;
            so.FindProperty("role").enumValueIndex  = 0;
            so.FindProperty("owner").enumValueIndex = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            return new CardInstance(data);
        }

        // Places a card on both boards at slotIndex and returns the player card.
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
