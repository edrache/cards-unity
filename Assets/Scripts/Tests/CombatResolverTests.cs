using System;
using NUnit.Framework;
using UnityEngine;
using CardsUnity;

namespace CardsUnity.Tests
{
    public class CombatResolverTests
    {
        [Test]
        public void Pressure_vs_Appeal_returns_Win()
        {
            Assert.AreEqual(BattleOutcome.Win, CombatResolver.DetermineOutcome(CardType.Pressure, CardType.Appeal));
        }

        [Test]
        public void Appeal_vs_Positioning_returns_Win()
        {
            Assert.AreEqual(BattleOutcome.Win, CombatResolver.DetermineOutcome(CardType.Appeal, CardType.Positioning));
        }

        [Test]
        public void Positioning_vs_Pressure_returns_Win()
        {
            Assert.AreEqual(BattleOutcome.Win, CombatResolver.DetermineOutcome(CardType.Positioning, CardType.Pressure));
        }

        [Test]
        public void Pressure_vs_Positioning_returns_Lose()
        {
            Assert.AreEqual(BattleOutcome.Lose, CombatResolver.DetermineOutcome(CardType.Pressure, CardType.Positioning));
        }

        [Test]
        public void Pressure_vs_Pressure_returns_Draw()
        {
            Assert.AreEqual(BattleOutcome.Draw, CombatResolver.DetermineOutcome(CardType.Pressure, CardType.Pressure));
        }

        [Test]
        public void Win_deals_full_card_value_as_damage()
        {
            var attacker = MakeCard(CardType.Pressure, value: 5);
            var defender = MakeCard(CardType.Appeal, value: 10);
            var result = CombatResolver.ResolveCombat(attacker, defender, new System.Random(0));
            Assert.AreEqual(5, result.DamageDealt);
        }

        [Test]
        public void Lose_deals_damage_between_1_and_card_value()
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var attacker = MakeCard(CardType.Pressure, value: 6);
                var defender = MakeCard(CardType.Positioning, value: 10);
                var result = CombatResolver.ResolveCombat(attacker, defender, new System.Random(seed));
                Assert.GreaterOrEqual(result.DamageDealt, 1);
                Assert.LessOrEqual(result.DamageDealt, 6);
            }
        }

        [Test]
        public void Combat_reduces_defender_CurrentValue()
        {
            var attacker = MakeCard(CardType.Pressure, value: 5);
            var defender = MakeCard(CardType.Appeal, value: 10);
            CombatResolver.ResolveCombat(attacker, defender, new System.Random(0));
            Assert.AreEqual(5, defender.CurrentValue);
        }

        [Test]
        public void Combat_sets_Destroyed_when_defender_value_reaches_zero()
        {
            var result = CombatResolver.ResolveCombat(
                MakeCard(CardType.Pressure, value: 10),
                MakeCard(CardType.Appeal, value: 5),
                new System.Random(0));

            Assert.IsTrue(result.Destroyed);
        }

        [Test]
        public void Combat_does_not_set_Destroyed_when_defender_survives()
        {
            var result = CombatResolver.ResolveCombat(
                MakeCard(CardType.Pressure, value: 3),
                MakeCard(CardType.Appeal, value: 10),
                new System.Random(0));

            Assert.IsFalse(result.Destroyed);
        }

        private static CardInstance MakeCard(CardType type, int value)
        {
            var definition = ScriptableObject.CreateInstance<CardDefinition>();
            definition.type = type;
            definition.value = value;
            return new CardInstance(definition);
        }
    }
}
