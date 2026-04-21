using System;
using CardsUnity.Combat;
using CardsUnity.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace CardsUnity.Tests
{
    public sealed class CombatResolverTests
    {
        // ── RPS matchup ────────────────────────────────────────────────────────

        [Test]
        public void GetRpsResult_PressureVsPositioning_IsAdvantage()
        {
            Assert.AreEqual(RpsResult.Advantage, CombatResolver.GetRpsResult(RpsType.Pressure, RpsType.Positioning));
        }

        [Test]
        public void GetRpsResult_PositioningVsAppeal_IsAdvantage()
        {
            Assert.AreEqual(RpsResult.Advantage, CombatResolver.GetRpsResult(RpsType.Positioning, RpsType.Appeal));
        }

        [Test]
        public void GetRpsResult_AppealVsPressure_IsAdvantage()
        {
            Assert.AreEqual(RpsResult.Advantage, CombatResolver.GetRpsResult(RpsType.Appeal, RpsType.Pressure));
        }

        [Test]
        public void GetRpsResult_PositioningVsPressure_IsDisadvantage()
        {
            Assert.AreEqual(RpsResult.Disadvantage, CombatResolver.GetRpsResult(RpsType.Positioning, RpsType.Pressure));
        }

        [Test]
        public void GetRpsResult_SameType_IsNeutral()
        {
            Assert.AreEqual(RpsResult.Neutral, CombatResolver.GetRpsResult(RpsType.Pressure, RpsType.Pressure));
            Assert.AreEqual(RpsResult.Neutral, CombatResolver.GetRpsResult(RpsType.Appeal, RpsType.Appeal));
            Assert.AreEqual(RpsResult.Neutral, CombatResolver.GetRpsResult(RpsType.Positioning, RpsType.Positioning));
        }

        // ── Support bonus ──────────────────────────────────────────────────────

        [Test]
        public void GetSupportBonus_NoNeighbours_ReturnsZero()
        {
            CardInstance card = MakeCard(RpsType.Pressure, 5, CardRole.None, CardOwner.Player);
            CardInstance[] board = { card, null, null };
            Assert.AreEqual(0, CombatResolver.GetSupportBonus(card, board));
        }

        [Test]
        public void GetSupportBonus_OneAdjacentSupport_ReturnsOne()
        {
            CardInstance support = MakeCard(RpsType.Appeal, 3, CardRole.Support, CardOwner.Player);
            CardInstance card    = MakeCard(RpsType.Pressure, 5, CardRole.None, CardOwner.Player);
            CardInstance[] board = { support, card, null };
            Assert.AreEqual(1, CombatResolver.GetSupportBonus(card, board));
        }

        [Test]
        public void GetSupportBonus_TwoAdjacentSupports_ReturnsTwo()
        {
            CardInstance support1 = MakeCard(RpsType.Appeal, 3, CardRole.Support, CardOwner.Player);
            CardInstance card     = MakeCard(RpsType.Pressure, 5, CardRole.None, CardOwner.Player);
            CardInstance support2 = MakeCard(RpsType.Appeal, 3, CardRole.Support, CardOwner.Player);
            CardInstance[] board  = { support1, card, support2 };
            Assert.AreEqual(2, CombatResolver.GetSupportBonus(card, board));
        }

        [Test]
        public void GetSupportBonus_NonSupportNeighbour_ReturnsZero()
        {
            CardInstance neighbour = MakeCard(RpsType.Appeal, 3, CardRole.Attack, CardOwner.Player);
            CardInstance card      = MakeCard(RpsType.Pressure, 5, CardRole.None, CardOwner.Player);
            CardInstance[] board   = { neighbour, card, null };
            Assert.AreEqual(0, CombatResolver.GetSupportBonus(card, board));
        }

        [Test]
        public void GetSupportBonus_CardNotOnBoard_ReturnsZero()
        {
            CardInstance card  = MakeCard(RpsType.Pressure, 5, CardRole.None, CardOwner.Player);
            CardInstance other = MakeCard(RpsType.Appeal, 3, CardRole.Support, CardOwner.Player);
            CardInstance[] board = { other, null, null };
            Assert.AreEqual(0, CombatResolver.GetSupportBonus(card, board));
        }

        // ── Effective range ────────────────────────────────────────────────────

        [Test]
        public void GetEffectiveRange_WithSupportNeighbour_EqualsValuePlusOne()
        {
            CardInstance support = MakeCard(RpsType.Appeal, 3, CardRole.Support, CardOwner.Player);
            CardInstance card    = MakeCard(RpsType.Pressure, 5, CardRole.None, CardOwner.Player);
            CardInstance[] board = { support, card, null };
            Assert.AreEqual(6, CombatResolver.GetEffectiveRange(card, board));
        }

        // ── ResolvePair – roles ────────────────────────────────────────────────

        [Test]
        public void ResolvePair_AttackRole_AddsPlusOneToOutgoingDamage()
        {
            // Player (attack) vs enemy (none), player has advantage.
            // All player rolls will be max (seeded so roll = effectiveRange).
            // We can't guarantee a specific seed, so we run many trials and
            // check that actual damage is roll + 1.
            CardInstance player = MakeCard(RpsType.Pressure, 5, CardRole.Attack, CardOwner.Player);
            CardInstance enemy  = MakeCard(RpsType.Positioning, 5, CardRole.None, CardOwner.Enemy);
            CardInstance[] pb   = { player, null, null };
            CardInstance[] eb   = { enemy, null, null };

            bool seenBonus = false;
            for (int seed = 0; seed < 100; seed++)
            {
                CardInstance p = MakeCard(RpsType.Pressure, 5, CardRole.Attack, CardOwner.Player);
                CardInstance e = MakeCard(RpsType.Positioning, 5, CardRole.None, CardOwner.Enemy);
                CardInstance[] pBoard = { p, null, null };
                CardInstance[] eBoard = { e, null, null };

                CombatResult result = CombatResolver.ResolvePair(p, e, pBoard, eBoard, 0, new Random(seed));
                // Player damage dealt = chosenRoll + 1 (attack bonus)
                Assert.AreEqual(result.ChosenPlayerRoll + 1, result.PlayerDamageDealt,
                    $"seed={seed}: expected PlayerDamageDealt = ChosenPlayerRoll+1");
                if (result.PlayerDamageDealt > result.ChosenPlayerRoll) seenBonus = true;
            }
            Assert.IsTrue(seenBonus);
        }

        [Test]
        public void ResolvePair_DefenseRole_SubtractsOneFromIncomingDamage()
        {
            // Player (defense) vs enemy (none), enemy has advantage (Positioning beats Pressure).
            for (int seed = 0; seed < 100; seed++)
            {
                CardInstance p = MakeCard(RpsType.Pressure, 5, CardRole.Defense, CardOwner.Player);
                CardInstance e = MakeCard(RpsType.Positioning, 5, CardRole.None, CardOwner.Enemy);
                CardInstance[] pBoard = { p, null, null };
                CardInstance[] eBoard = { e, null, null };

                CombatResult result = CombatResolver.ResolvePair(p, e, pBoard, eBoard, 0, new Random(seed));
                int expected = Math.Max(0, result.ChosenEnemyRoll - 1);
                Assert.AreEqual(expected, result.EnemyDamageDealt,
                    $"seed={seed}: expected EnemyDamageDealt = max(0, chosenEnemyRoll-1)");
            }
        }

        // ── ResolvePair – neutral ──────────────────────────────────────────────

        [Test]
        public void ResolvePair_Neutral_BothSideTakeSharedMinRoll()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                CardInstance p = MakeCard(RpsType.Pressure, 5, CardRole.None, CardOwner.Player);
                CardInstance e = MakeCard(RpsType.Pressure, 5, CardRole.None, CardOwner.Enemy);
                CardInstance[] pBoard = { p, null, null };
                CardInstance[] eBoard = { e, null, null };

                CombatResult result = CombatResolver.ResolvePair(p, e, pBoard, eBoard, 0, new Random(seed));
                Assert.AreEqual(RpsResult.Neutral, result.RpsResult);

                int sharedBase = Math.Min(result.ChosenPlayerRoll, result.ChosenEnemyRoll);
                Assert.AreEqual(sharedBase, result.PlayerDamageDealt,
                    $"seed={seed}: PlayerDamageDealt should equal shared min roll");
                Assert.AreEqual(sharedBase, result.EnemyDamageDealt,
                    $"seed={seed}: EnemyDamageDealt should equal shared min roll");
            }
        }

        // ── ResolvePair – simultaneous damage ─────────────────────────────────

        [Test]
        public void ResolvePair_SimultaneousDeath_BothCardsDie()
        {
            // Force a scenario where both cards die: low values + roll will definitely be lethal.
            // Player value 1, enemy value 1, advantage for player → player rolls 2x[1..1]=1,
            // enemy rolls 1x[1..1]=1, playerDealt=1, enemyDealt=1.
            CardInstance p = MakeCard(RpsType.Pressure, 1, CardRole.None, CardOwner.Player);
            CardInstance e = MakeCard(RpsType.Positioning, 1, CardRole.None, CardOwner.Enemy);
            CardInstance[] pBoard = { p, null, null };
            CardInstance[] eBoard = { e, null, null };

            CombatResult result = CombatResolver.ResolvePair(p, e, pBoard, eBoard, 0, new Random(0));
            Assert.AreEqual(RpsResult.Advantage, result.RpsResult);
            Assert.IsTrue(result.PlayerDied, "Player should die when value reaches 0");
            Assert.IsTrue(result.EnemyDied,  "Enemy should die when value reaches 0");
            Assert.LessOrEqual(result.PlayerValueAfter, 0);
            Assert.LessOrEqual(result.EnemyValueAfter,  0);
        }

        [Test]
        public void ResolvePair_MutatesCardValues()
        {
            CardInstance p = MakeCard(RpsType.Pressure, 5, CardRole.None, CardOwner.Player);
            CardInstance e = MakeCard(RpsType.Positioning, 5, CardRole.None, CardOwner.Enemy);
            CardInstance[] pBoard = { p, null, null };
            CardInstance[] eBoard = { e, null, null };

            CombatResolver.ResolvePair(p, e, pBoard, eBoard, 0, new Random(42));
            Assert.Less(e.Value, 5, "Enemy value should have decreased");
        }

        [Test]
        public void ResolvePair_ValueBeforeAndAfterMatchCardMutation()
        {
            CardInstance p = MakeCard(RpsType.Pressure, 5, CardRole.None, CardOwner.Player);
            CardInstance e = MakeCard(RpsType.Positioning, 5, CardRole.None, CardOwner.Enemy);
            CardInstance[] pBoard = { p, null, null };
            CardInstance[] eBoard = { e, null, null };

            CombatResult result = CombatResolver.ResolvePair(p, e, pBoard, eBoard, 0, new Random(7));
            Assert.AreEqual(result.PlayerValueAfter, p.Value);
            Assert.AreEqual(result.EnemyValueAfter,  e.Value);
        }

        // ── ResolvePair – advantage rolls two dice ─────────────────────────────

        [Test]
        public void ResolvePair_Advantage_ChosenPlayerRollIsMaxOfTwoRolls()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                CardInstance p = MakeCard(RpsType.Pressure, 6, CardRole.None, CardOwner.Player);
                CardInstance e = MakeCard(RpsType.Positioning, 6, CardRole.None, CardOwner.Enemy);
                CardInstance[] pBoard = { p, null, null };
                CardInstance[] eBoard = { e, null, null };

                CombatResult result = CombatResolver.ResolvePair(p, e, pBoard, eBoard, 0, new Random(seed));
                Assert.AreEqual(RpsResult.Advantage, result.RpsResult);
                Assert.AreEqual(Math.Max(result.PlayerRoll1, result.PlayerRoll2), result.ChosenPlayerRoll,
                    $"seed={seed}");
            }
        }

        [Test]
        public void ResolvePair_Disadvantage_ChosenEnemyRollIsMaxOfTwoRolls()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                CardInstance p = MakeCard(RpsType.Positioning, 6, CardRole.None, CardOwner.Player);
                CardInstance e = MakeCard(RpsType.Pressure, 6, CardRole.None, CardOwner.Enemy);
                CardInstance[] pBoard = { p, null, null };
                CardInstance[] eBoard = { e, null, null };

                CombatResult result = CombatResolver.ResolvePair(p, e, pBoard, eBoard, 0, new Random(seed));
                Assert.AreEqual(RpsResult.Disadvantage, result.RpsResult);
                Assert.AreEqual(Math.Max(result.EnemyRoll1, result.EnemyRoll2), result.ChosenEnemyRoll,
                    $"seed={seed}");
            }
        }

        // ── GetCombatPreview – read-only ───────────────────────────────────────

        [Test]
        public void GetCombatPreview_DoesNotMutateCardValues()
        {
            CardInstance p = MakeCard(RpsType.Pressure, 5, CardRole.None, CardOwner.Player);
            CardInstance e = MakeCard(RpsType.Positioning, 5, CardRole.None, CardOwner.Enemy);
            CardInstance[] pBoard = { p, null, null };
            CardInstance[] eBoard = { e, null, null };

            CombatResolver.GetCombatPreview(p, e, pBoard, eBoard, 0);
            Assert.AreEqual(5, p.Value);
            Assert.AreEqual(5, e.Value);
        }

        [Test]
        public void GetCombatPreview_MaxDamageCoversActualDamage()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                CardInstance p = MakeCard(RpsType.Pressure, 5, CardRole.None, CardOwner.Player);
                CardInstance e = MakeCard(RpsType.Positioning, 5, CardRole.None, CardOwner.Enemy);
                CardInstance[] pBoard = { p, null, null };
                CardInstance[] eBoard = { e, null, null };

                CombatPreview preview = CombatResolver.GetCombatPreview(p, e, pBoard, eBoard, 0);

                CardInstance p2 = MakeCard(RpsType.Pressure, 5, CardRole.None, CardOwner.Player);
                CardInstance e2 = MakeCard(RpsType.Positioning, 5, CardRole.None, CardOwner.Enemy);
                CardInstance[] pBoard2 = { p2, null, null };
                CardInstance[] eBoard2 = { e2, null, null };

                CombatResult result = CombatResolver.ResolvePair(p2, e2, pBoard2, eBoard2, 0, new Random(seed));
                Assert.LessOrEqual(result.PlayerDamageDealt, preview.PlayerMaxDamageDealt,
                    $"seed={seed}: actual player damage exceeded preview max");
                Assert.LessOrEqual(result.EnemyDamageDealt, preview.EnemyMaxDamageDealt,
                    $"seed={seed}: actual enemy damage exceeded preview max");
            }
        }

        [Test]
        public void GetCombatPreview_PlayerCanDie_TrueWhenMaxEnemyDamageIsLethal()
        {
            // Enemy range 5, no defense on player (value 3) → max enemy damage = 5. 3 - 5 <= 0 → can die.
            CardInstance p = MakeCard(RpsType.Positioning, 3, CardRole.None, CardOwner.Player);
            CardInstance e = MakeCard(RpsType.Pressure, 5, CardRole.None, CardOwner.Enemy);
            CardInstance[] pBoard = { p, null, null };
            CardInstance[] eBoard = { e, null, null };

            CombatPreview preview = CombatResolver.GetCombatPreview(p, e, pBoard, eBoard, 0);
            Assert.IsTrue(preview.PlayerCanDie);
        }

        [Test]
        public void GetCombatPreview_SupportBonus_IncludedInEffectiveRange()
        {
            CardInstance support = MakeCard(RpsType.Appeal, 3, CardRole.Support, CardOwner.Player);
            CardInstance p       = MakeCard(RpsType.Pressure, 5, CardRole.None, CardOwner.Player);
            CardInstance e       = MakeCard(RpsType.Positioning, 5, CardRole.None, CardOwner.Enemy);
            CardInstance[] pBoard = { support, p, null };
            CardInstance[] eBoard = { e, null, null };

            CombatPreview preview = CombatResolver.GetCombatPreview(p, e, pBoard, eBoard, 1);
            Assert.AreEqual(1, preview.PlayerSupportBonus);
            Assert.AreEqual(6, preview.PlayerEffectiveRange);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static CardInstance MakeCard(RpsType rps, int value, CardRole role, CardOwner owner)
        {
            CardData data = ScriptableObject.CreateInstance<CardData>();
            var so = new UnityEditor.SerializedObject(data);
            so.FindProperty("id").stringValue        = System.Guid.NewGuid().ToString();
            so.FindProperty("cardName").stringValue  = "Test";
            so.FindProperty("rps").enumValueIndex    = (int)rps;
            so.FindProperty("value").intValue        = value;
            so.FindProperty("role").enumValueIndex   = (int)role;
            so.FindProperty("owner").enumValueIndex  = (int)owner;
            so.ApplyModifiedPropertiesWithoutUndo();
            return new CardInstance(data);
        }
    }
}
