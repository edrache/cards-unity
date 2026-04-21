using System;
using System.Collections.Generic;
using CardsUnity.Data;
using CardsUnity.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace CardsUnity.Tests
{
    public sealed class RewardFlowTests
    {
        // ── GenerateRewardChoices ──────────────────────────────────────────────

        [Test]
        public void GenerateRewardChoices_ProducesThreeChoices()
        {
            GameState state = MakeState(3);
            AddPlayerCards(state, 5);

            RewardFlow.GenerateRewardChoices(state, new Random(0));

            Assert.AreEqual(3, state.RewardChoices.Count);
        }

        [Test]
        public void GenerateRewardChoices_FewerThanThreePlayerCards_ProducesAvailableCount()
        {
            GameState state = MakeState(3);
            AddPlayerCards(state, 2);

            RewardFlow.GenerateRewardChoices(state, new Random(0));

            Assert.AreEqual(2, state.RewardChoices.Count);
        }

        [Test]
        public void GenerateRewardChoices_ChoicesArePlayerOwned()
        {
            GameState state = MakeState(3);
            AddPlayerCards(state, 5);

            RewardFlow.GenerateRewardChoices(state, new Random(0));

            foreach (CardInstance choice in state.RewardChoices)
                Assert.AreEqual(CardOwner.Player, choice.Owner);
        }

        [Test]
        public void GenerateRewardChoices_ChoicesAreFreshClones_NotOriginals()
        {
            GameState state = MakeState(3);
            AddPlayerCards(state, 3);

            RewardFlow.GenerateRewardChoices(state, new Random(0));

            // Choices must be new instances (not references from playerDeck).
            foreach (CardInstance choice in state.RewardChoices)
                CollectionAssert.DoesNotContain(state.PlayerDeck, choice);
        }

        [Test]
        public void GenerateRewardChoices_IncludesCardsFromAllZones()
        {
            GameState state = MakeState(3);
            // 1 card in deck, 1 in hand, 1 in cemetery, 1 on board.
            state.PlayerDeck.Add(MakeCard());
            state.PlayerHand.Add(MakeCard());
            state.PlayerCemetery.Add(MakeCard());
            state.PlayerBoard[0] = MakeCard();

            RewardFlow.GenerateRewardChoices(state, new Random(0));

            Assert.AreEqual(3, state.RewardChoices.Count);
        }

        [Test]
        public void GenerateRewardChoices_ClearsPreviousChoices()
        {
            GameState state = MakeState(3);
            AddPlayerCards(state, 5);
            state.RewardChoices.Add(MakeCard());

            RewardFlow.GenerateRewardChoices(state, new Random(0));

            Assert.AreEqual(3, state.RewardChoices.Count);
        }

        [Test]
        public void GenerateRewardChoices_IsDeterministicWithSeed()
        {
            GameState stateA = MakeState(3);
            GameState stateB = MakeState(3);
            AddPlayerCards(stateA, 6);
            AddPlayerCards(stateB, 6);

            RewardFlow.GenerateRewardChoices(stateA, new Random(42));
            RewardFlow.GenerateRewardChoices(stateB, new Random(42));

            for (int i = 0; i < 3; i++)
                Assert.AreEqual(stateA.RewardChoices[i].CardName, stateB.RewardChoices[i].CardName);
        }

        // ── SelectReward ───────────────────────────────────────────────────────

        [Test]
        public void SelectReward_AddsChosenCardToPlayerDeck()
        {
            GameState state = MakeState(3);
            AddPlayerCards(state, 3);
            RewardFlow.GenerateRewardChoices(state, new Random(0));
            CardInstance chosen = state.RewardChoices[0];

            RewardFlow.SelectReward(state, chosen);

            CollectionAssert.Contains(state.PlayerDeck, chosen);
        }

        [Test]
        public void SelectReward_AppendsToEndOfDeck()
        {
            GameState state = MakeState(3);
            AddPlayerCards(state, 3);
            int deckSizeBefore = state.PlayerDeck.Count;
            RewardFlow.GenerateRewardChoices(state, new Random(0));
            CardInstance chosen = state.RewardChoices[0];

            RewardFlow.SelectReward(state, chosen);

            Assert.AreEqual(deckSizeBefore + 1, state.PlayerDeck.Count);
            Assert.AreSame(chosen, state.PlayerDeck[state.PlayerDeck.Count - 1]);
        }

        [Test]
        public void SelectReward_ClearsRewardChoices()
        {
            GameState state = MakeState(3);
            AddPlayerCards(state, 3);
            RewardFlow.GenerateRewardChoices(state, new Random(0));
            CardInstance chosen = state.RewardChoices[0];

            RewardFlow.SelectReward(state, chosen);

            Assert.IsEmpty(state.RewardChoices);
        }

        [Test]
        public void SelectReward_InvalidCard_Throws()
        {
            GameState state = MakeState(3);
            AddPlayerCards(state, 3);
            RewardFlow.GenerateRewardChoices(state, new Random(0));

            Assert.Throws<ArgumentException>(() => RewardFlow.SelectReward(state, MakeCard()));
        }

        // ── SetupNewEncounter ──────────────────────────────────────────────────

        [Test]
        public void SetupNewEncounter_ResetsRoundToOne()
        {
            GameState state = MakeState(3);
            state.Round = 5;

            RewardFlow.SetupNewEncounter(state, MakeEnemyDefs(6), new Random(0));

            Assert.AreEqual(1, state.Round);
        }

        [Test]
        public void SetupNewEncounter_SetsPhaseToDrawDirectly()
        {
            GameState state = MakeState(3);

            RewardFlow.SetupNewEncounter(state, MakeEnemyDefs(6), new Random(0));

            Assert.AreEqual(GamePhase.Draw, state.Phase);
        }

        [Test]
        public void SetupNewEncounter_ClearsEnemyCemetery()
        {
            GameState state = MakeState(3);
            state.EnemyCemetery.Add(MakeEnemyCard());

            RewardFlow.SetupNewEncounter(state, MakeEnemyDefs(6), new Random(0));

            Assert.IsEmpty(state.EnemyCemetery);
        }

        [Test]
        public void SetupNewEncounter_FillsEnemyBoardFromNewDeck()
        {
            GameState state = MakeState(3);

            RewardFlow.SetupNewEncounter(state, MakeEnemyDefs(6), new Random(0));

            int filledSlots = 0;
            foreach (CardInstance card in state.EnemyBoard)
                if (card != null) filledSlots++;

            Assert.AreEqual(3, filledSlots);
        }

        [Test]
        public void SetupNewEncounter_PlayerDeckUnchanged()
        {
            GameState state = MakeState(3);
            AddPlayerCards(state, 4);
            int deckBefore = state.PlayerDeck.Count;

            RewardFlow.SetupNewEncounter(state, MakeEnemyDefs(6), new Random(0));

            Assert.AreEqual(deckBefore, state.PlayerDeck.Count);
        }

        [Test]
        public void SetupNewEncounter_PlayerHandUnchanged()
        {
            GameState state = MakeState(3);
            state.PlayerHand.Add(MakeCard());

            RewardFlow.SetupNewEncounter(state, MakeEnemyDefs(6), new Random(0));

            Assert.AreEqual(1, state.PlayerHand.Count);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static GameState MakeState(int slotCount) => new GameState(slotCount);

        private static CardInstance MakeCard(CardOwner owner = CardOwner.Player)
        {
            CardData data = ScriptableObject.CreateInstance<CardData>();
            var so = new SerializedObject(data);
            so.FindProperty("id").stringValue       = Guid.NewGuid().ToString();
            so.FindProperty("cardName").stringValue = "Test";
            so.FindProperty("rps").enumValueIndex   = 0;
            so.FindProperty("value").intValue       = 5;
            so.FindProperty("role").enumValueIndex  = 0;
            so.FindProperty("owner").enumValueIndex = (int)owner;
            so.ApplyModifiedPropertiesWithoutUndo();
            return new CardInstance(data);
        }

        private static CardInstance MakeEnemyCard() => MakeCard(CardOwner.Enemy);

        private static void AddPlayerCards(GameState state, int count)
        {
            for (int i = 0; i < count; i++)
                state.PlayerDeck.Add(MakeCard());
        }

        private static List<CardData> MakeEnemyDefs(int count)
        {
            var defs = new List<CardData>(count);
            for (int i = 0; i < count; i++)
            {
                CardData data = ScriptableObject.CreateInstance<CardData>();
                var so = new SerializedObject(data);
                so.FindProperty("id").stringValue       = Guid.NewGuid().ToString();
                so.FindProperty("cardName").stringValue = $"Enemy{i}";
                so.FindProperty("rps").enumValueIndex   = i % 3;
                so.FindProperty("value").intValue       = 3;
                so.FindProperty("role").enumValueIndex  = 0;
                so.FindProperty("owner").enumValueIndex = (int)CardOwner.Enemy;
                so.ApplyModifiedPropertiesWithoutUndo();
                defs.Add(data);
            }
            return defs;
        }
    }
}
