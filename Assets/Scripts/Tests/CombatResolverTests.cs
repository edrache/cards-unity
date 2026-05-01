using System;
using NUnit.Framework;
using UnityEngine;
using CardsUnity;

namespace CardsUnity.Tests
{
    public class CombatResolverTests
    {
        [Test]
        public void Force_vs_Presence_returns_Win()
        {
            Assert.AreEqual(BattleOutcome.Win, CombatResolver.DetermineOutcome(CardType.Force, CardType.Presence));
        }

        [Test]
        public void Presence_vs_Wit_returns_Win()
        {
            Assert.AreEqual(BattleOutcome.Win, CombatResolver.DetermineOutcome(CardType.Presence, CardType.Wit));
        }

        [Test]
        public void Wit_vs_Force_returns_Win()
        {
            Assert.AreEqual(BattleOutcome.Win, CombatResolver.DetermineOutcome(CardType.Wit, CardType.Force));
        }

        [Test]
        public void Force_vs_Wit_returns_Lose()
        {
            Assert.AreEqual(BattleOutcome.Lose, CombatResolver.DetermineOutcome(CardType.Force, CardType.Wit));
        }

        [Test]
        public void Force_vs_Force_returns_Draw()
        {
            Assert.AreEqual(BattleOutcome.Draw, CombatResolver.DetermineOutcome(CardType.Force, CardType.Force));
        }

        [Test]
        public void Win_deals_full_card_value_as_damage()
        {
            var attacker = MakeCard(CardType.Force, value: 5);
            var defender = MakeCard(CardType.Presence, value: 10);
            var result = CombatResolver.ResolveCombat(attacker, defender, new System.Random(0));
            Assert.AreEqual(5, result.DamageDealt);
        }

        [Test]
        public void Lose_deals_damage_between_1_and_card_value()
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var attacker = MakeCard(CardType.Force, value: 6);
                var defender = MakeCard(CardType.Wit, value: 10);
                var result = CombatResolver.ResolveCombat(attacker, defender, new System.Random(seed));
                Assert.GreaterOrEqual(result.DamageDealt, 1);
                Assert.LessOrEqual(result.DamageDealt, 6);
            }
        }

        [Test]
        public void Combat_reduces_defender_CurrentValue()
        {
            var attacker = MakeCard(CardType.Force, value: 5);
            var defender = MakeCard(CardType.Presence, value: 10);
            CombatResolver.ResolveCombat(attacker, defender, new System.Random(0));
            Assert.AreEqual(5, defender.CurrentValue);
        }

        [Test]
        public void Combat_sets_Destroyed_when_defender_value_reaches_zero()
        {
            var result = CombatResolver.ResolveCombat(
                MakeCard(CardType.Force, value: 10),
                MakeCard(CardType.Presence, value: 5),
                new System.Random(0));

            Assert.IsTrue(result.Destroyed);
        }

        [Test]
        public void Combat_does_not_set_Destroyed_when_defender_survives()
        {
            var result = CombatResolver.ResolveCombat(
                MakeCard(CardType.Force, value: 3),
                MakeCard(CardType.Presence, value: 10),
                new System.Random(0));

            Assert.IsFalse(result.Destroyed);
        }

        [Test]
        public void ResolveExchange_applies_damage_to_both_cards()
        {
            var player = MakeCard(CardType.Force, value: 5);
            var opponent = MakeCard(CardType.Wit, value: 4);

            var result = CombatResolver.ResolveExchange(player, opponent, new System.Random(0));

            Assert.Less(player.CurrentValue, 5);
            Assert.Less(opponent.CurrentValue, 4);
            Assert.Greater(result.FirstResult.DamageDealt, 0);
            Assert.Greater(result.SecondResult.DamageDealt, 0);
        }

        [Test]
        public void ResolveExchange_uses_simultaneous_damage_when_both_cards_destroy_each_other()
        {
            var player = MakeCard(CardType.Force, value: 1);
            var opponent = MakeCard(CardType.Force, value: 1);

            var result = CombatResolver.ResolveExchange(player, opponent, new System.Random(0));

            Assert.AreEqual(0, opponent.CurrentValue);
            Assert.AreEqual(0, player.CurrentValue);
            Assert.IsTrue(result.FirstResult.Destroyed);
            Assert.IsTrue(result.SecondResult.Destroyed);
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
