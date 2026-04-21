using CardsUnity.Config;
using CardsUnity.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CardsUnity.Tests
{
    public sealed class DataModelTests
    {
        [Test]
        public void CardInstance_CopiesDefinitionAndKeepsRuntimeValueMutable()
        {
            CardData definition = CreateCardData(
                "brawler",
                "Break Will",
                RpsType.Pressure,
                5,
                CardRole.Attack,
                CardOwner.Player,
                "!",
                "Test flavor.");

            CardInstance instance = new CardInstance(definition);
            instance.Value -= 2;

            Assert.AreSame(definition, instance.Definition);
            Assert.AreEqual("brawler", instance.Id);
            Assert.AreEqual("Break Will", instance.CardName);
            Assert.AreEqual(RpsType.Pressure, instance.Rps);
            Assert.AreEqual(5, instance.BaseValue);
            Assert.AreEqual(3, instance.Value);
            Assert.AreEqual(CardRole.Attack, instance.Role);
            Assert.AreEqual(CardOwner.Player, instance.Owner);
            Assert.AreEqual("!", instance.Emoji);
            Assert.AreEqual("Test flavor.", instance.Flavor);
            Assert.AreEqual(5, definition.Value);
        }

        [Test]
        public void CardInstance_IsDestroyedWhenValueIsZeroOrLower()
        {
            CardData definition = CreateCardData("buckler", "Slip the Blow", RpsType.Positioning, 4, CardRole.None, CardOwner.Player);
            CardInstance instance = new CardInstance(definition);

            instance.Value = 0;

            Assert.IsTrue(instance.IsDestroyed);
        }

        [Test]
        public void GameState_UsesConfigDrivenBoardSizesAndInitialState()
        {
            GameConfig config = ScriptableObject.CreateInstance<GameConfig>();
            GameState state = new GameState(config);

            Assert.AreEqual(GamePhase.Draw, state.Phase);
            Assert.AreEqual(1, state.Round);
            Assert.AreEqual(config.SlotCount, state.PlayerBoard.Length);
            Assert.AreEqual(config.SlotCount, state.EnemyBoard.Length);
            Assert.IsEmpty(state.PlayerDeck);
            Assert.IsEmpty(state.PlayerHand);
            Assert.IsEmpty(state.PlayerCemetery);
            Assert.IsEmpty(state.EnemyDeck);
            Assert.IsEmpty(state.EnemyCemetery);
            Assert.IsEmpty(state.PlacementOrder);
            Assert.IsEmpty(state.RewardChoices);
        }

        [Test]
        public void GameState_RejectsInvalidSlotCount()
        {
            Assert.That(() => new GameState(0), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void Enums_ContainMigrationValues()
        {
            Assert.AreEqual(new[] { "Pressure", "Appeal", "Positioning" }, System.Enum.GetNames(typeof(RpsType)));
            Assert.AreEqual(new[] { "Attack", "Defense", "Support", "None" }, System.Enum.GetNames(typeof(CardRole)));
            Assert.AreEqual(new[] { "Player", "Enemy" }, System.Enum.GetNames(typeof(CardOwner)));
            Assert.AreEqual(new[] { "Draw", "Placement", "Combat", "Reward", "End" }, System.Enum.GetNames(typeof(GamePhase)));
        }

        private static CardData CreateCardData(
            string id,
            string cardName,
            RpsType rps,
            int value,
            CardRole role,
            CardOwner owner,
            string emoji = "",
            string flavor = "")
        {
            CardData data = ScriptableObject.CreateInstance<CardData>();
            SerializedObject serialized = new SerializedObject(data);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("cardName").stringValue = cardName;
            serialized.FindProperty("rps").enumValueIndex = (int)rps;
            serialized.FindProperty("value").intValue = value;
            serialized.FindProperty("role").enumValueIndex = (int)role;
            serialized.FindProperty("owner").enumValueIndex = (int)owner;
            serialized.FindProperty("emoji").stringValue = emoji;
            serialized.FindProperty("flavor").stringValue = flavor;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }
    }
}
