using System;
using System.Collections.Generic;
using System.Linq;
using CardsUnity.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace CardsUnity.Tests
{
    public sealed class DeckFactoryTests
    {
        [Test]
        public void CreatePlayerDeck_ClonesEveryDefinitionAndShufflesDeterministically()
        {
            CardData[] definitions =
            {
                CreateCardData("one", "One", RpsType.Pressure, 3, CardRole.None, CardOwner.Player),
                CreateCardData("two", "Two", RpsType.Appeal, 4, CardRole.None, CardOwner.Player),
                CreateCardData("three", "Three", RpsType.Positioning, 5, CardRole.None, CardOwner.Player),
                CreateCardData("four", "Four", RpsType.Pressure, 6, CardRole.None, CardOwner.Player),
                CreateCardData("five", "Five", RpsType.Appeal, 5, CardRole.None, CardOwner.Player),
                CreateCardData("six", "Six", RpsType.Positioning, 4, CardRole.None, CardOwner.Player),
            };

            List<CardInstance> first = DeckFactory.CreatePlayerDeck(definitions, new Random(17));
            List<CardInstance> second = DeckFactory.CreatePlayerDeck(definitions, new Random(17));

            Assert.AreEqual(definitions.Length, first.Count);
            Assert.AreEqual(first.Select(card => card.Id), second.Select(card => card.Id));
            Assert.AreNotEqual(definitions.Select(card => card.Id), first.Select(card => card.Id));
            Assert.IsTrue(first.All(card => card.Owner == CardOwner.Player));
            Assert.IsTrue(first.All(card => definitions.Contains(card.Definition)));
            Assert.IsTrue(first.Zip(second, (left, right) => !ReferenceEquals(left, right)).All(isDifferent => isDifferent));
        }

        [Test]
        public void CreateEnemyDeck_ClonesEveryDefinitionWithEnemyOwnership()
        {
            CardData[] definitions =
            {
                CreateCardData("brawler", "Kick In the Door", RpsType.Pressure, 5, CardRole.None, CardOwner.Enemy),
                CreateCardData("buckler", "Duck Behind Crates", RpsType.Positioning, 4, CardRole.None, CardOwner.Enemy),
            };

            List<CardInstance> deck = DeckFactory.CreateEnemyDeck(definitions, new Random(5));

            Assert.AreEqual(2, deck.Count);
            Assert.IsTrue(deck.All(card => card.Owner == CardOwner.Enemy));
            Assert.IsTrue(deck.All(card => card.Role == CardRole.None));
        }

        [Test]
        public void CreateDeck_MutatingCloneDoesNotMutateAnotherCloneOrDefinition()
        {
            CardData definition = CreateCardData("brawler", "Break Will", RpsType.Pressure, 5, CardRole.Attack, CardOwner.Player);

            List<CardInstance> deck = DeckFactory.CreatePlayerDeck(new[] { definition, definition }, new Random(1));
            deck[0].Value = 1;

            Assert.AreEqual(5, definition.Value);
            Assert.AreEqual(1, deck[0].Value);
            Assert.AreEqual(5, deck[1].Value);
            Assert.AreNotSame(deck[0], deck[1]);
        }

        [Test]
        public void CreatePlayerDeck_RejectsEnemyDefinitions()
        {
            CardData enemyDefinition = CreateCardData("brawler", "Kick In the Door", RpsType.Pressure, 5, CardRole.None, CardOwner.Enemy);

            Assert.That(
                () => DeckFactory.CreatePlayerDeck(new[] { enemyDefinition }, new Random(1)),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void CardDefinitionAssets_MatchMigrationDeckCountsAndRanges()
        {
            CardData[] playerDefinitions = Resources.LoadAll<CardData>("CardDefinitions/Player");
            CardData[] enemyDefinitions = Resources.LoadAll<CardData>("CardDefinitions/Enemy");

            Assert.AreEqual(12, playerDefinitions.Length);
            Assert.AreEqual(6, enemyDefinitions.Length);
            Assert.IsTrue(playerDefinitions.All(card => card.Owner == CardOwner.Player));
            Assert.IsTrue(enemyDefinitions.All(card => card.Owner == CardOwner.Enemy));
            Assert.IsTrue(playerDefinitions.All(card => card.Value >= 3 && card.Value <= 6));
            Assert.IsTrue(enemyDefinitions.All(card => card.Value >= 4 && card.Value <= 5));
            Assert.IsTrue(enemyDefinitions.All(card => card.Role == CardRole.None));
            Assert.NotNull(playerDefinitions.SingleOrDefault(card => card.Id == "tactician" && card.Role == CardRole.Support));
            Assert.NotNull(playerDefinitions.SingleOrDefault(card => card.Id == "brawler" && card.Role == CardRole.Attack));
            Assert.NotNull(playerDefinitions.SingleOrDefault(card => card.Id == "bulwark" && card.Role == CardRole.Defense));
        }

        [Test]
        public void CardDefinitionAssets_CreateExpectedRuntimeDeckSizes()
        {
            CardData[] playerDefinitions = Resources.LoadAll<CardData>("CardDefinitions/Player");
            CardData[] enemyDefinitions = Resources.LoadAll<CardData>("CardDefinitions/Enemy");

            List<CardInstance> playerDeck = DeckFactory.CreatePlayerDeck(playerDefinitions, new Random(3));
            List<CardInstance> enemyDeck = DeckFactory.CreateEnemyDeck(enemyDefinitions, new Random(3));

            Assert.AreEqual(12, playerDeck.Count);
            Assert.AreEqual(6, enemyDeck.Count);
            Assert.IsTrue(playerDeck.All(card => card.Owner == CardOwner.Player));
            Assert.IsTrue(enemyDeck.All(card => card.Owner == CardOwner.Enemy));
        }

        private static CardData CreateCardData(
            string id,
            string cardName,
            RpsType rps,
            int value,
            CardRole role,
            CardOwner owner)
        {
            CardData data = ScriptableObject.CreateInstance<CardData>();
            SerializedObject serialized = new SerializedObject(data);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("cardName").stringValue = cardName;
            serialized.FindProperty("rps").enumValueIndex = (int)rps;
            serialized.FindProperty("value").intValue = value;
            serialized.FindProperty("role").enumValueIndex = (int)role;
            serialized.FindProperty("owner").enumValueIndex = (int)owner;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }
    }
}
