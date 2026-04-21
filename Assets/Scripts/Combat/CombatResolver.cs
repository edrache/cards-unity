using System;
using CardsUnity.Data;

namespace CardsUnity.Combat
{
    public static class CombatResolver
    {
        // Pressure beats Positioning, Positioning beats Appeal, Appeal beats Pressure
        public static RpsResult GetRpsResult(RpsType attacker, RpsType defender)
        {
            if (attacker == defender)
                return RpsResult.Neutral;

            bool advantage =
                (attacker == RpsType.Pressure   && defender == RpsType.Positioning) ||
                (attacker == RpsType.Positioning && defender == RpsType.Appeal)      ||
                (attacker == RpsType.Appeal      && defender == RpsType.Pressure);

            return advantage ? RpsResult.Advantage : RpsResult.Disadvantage;
        }

        // Count immediately adjacent allies on the same board whose role is Support.
        public static int GetSupportBonus(CardInstance card, CardInstance[] board)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            if (board == null) throw new ArgumentNullException(nameof(board));

            int idx = Array.IndexOf(board, card);
            if (idx < 0) return 0;

            int bonus = 0;
            if (idx > 0 && board[idx - 1] != null && board[idx - 1].Role == CardRole.Support)
                bonus++;
            if (idx < board.Length - 1 && board[idx + 1] != null && board[idx + 1].Role == CardRole.Support)
                bonus++;
            return bonus;
        }

        public static int GetEffectiveRange(CardInstance card, CardInstance[] board)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            if (board == null) throw new ArgumentNullException(nameof(board));
            return card.Value + GetSupportBonus(card, board);
        }

        // Resolves one column. Mutates card Values. Returns the combat result.
        public static CombatResult ResolvePair(
            CardInstance playerCard,
            CardInstance enemyCard,
            CardInstance[] playerBoard,
            CardInstance[] enemyBoard,
            int slotIndex,
            Random rng)
        {
            if (playerCard == null) throw new ArgumentNullException(nameof(playerCard));
            if (enemyCard  == null) throw new ArgumentNullException(nameof(enemyCard));
            if (playerBoard == null) throw new ArgumentNullException(nameof(playerBoard));
            if (enemyBoard  == null) throw new ArgumentNullException(nameof(enemyBoard));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            RpsResult rpsResult = GetRpsResult(playerCard.Rps, enemyCard.Rps);

            int playerSupportBonus = GetSupportBonus(playerCard, playerBoard);
            int enemySupportBonus  = GetSupportBonus(enemyCard,  enemyBoard);
            int playerRange = playerCard.Value + playerSupportBonus;
            int enemyRange  = enemyCard.Value  + enemySupportBonus;

            int playerValueBefore = playerCard.Value;
            int enemyValueBefore  = enemyCard.Value;

            int playerRoll1 = 0, playerRoll2 = 0;
            int enemyRoll1  = 0, enemyRoll2  = 0;
            int chosenPlayerRoll, chosenEnemyRoll;
            int playerDamageDealt, enemyDamageDealt;

            if (rpsResult == RpsResult.Neutral)
            {
                playerRoll1 = RollDie(rng, playerRange);
                enemyRoll1  = RollDie(rng, enemyRange);
                chosenPlayerRoll = playerRoll1;
                chosenEnemyRoll  = enemyRoll1;

                // Both sides take the lower roll as base damage, then subtract own defense.
                int sharedBase  = Math.Min(playerRoll1, enemyRoll1);
                int playerTaken = Math.Max(0, sharedBase - (playerCard.Role == CardRole.Defense ? 1 : 0));
                int enemyTaken  = Math.Max(0, sharedBase - (enemyCard.Role  == CardRole.Defense ? 1 : 0));

                playerDamageDealt = enemyTaken;
                enemyDamageDealt  = playerTaken;
            }
            else if (rpsResult == RpsResult.Advantage)
            {
                // Player rolls twice and keeps the highest.
                playerRoll1 = RollDie(rng, playerRange);
                playerRoll2 = RollDie(rng, playerRange);
                chosenPlayerRoll = Math.Max(playerRoll1, playerRoll2);

                enemyRoll1      = RollDie(rng, enemyRange);
                chosenEnemyRoll = enemyRoll1;

                playerDamageDealt = CalcDealt(chosenPlayerRoll, playerCard.Role, enemyCard.Role);
                enemyDamageDealt  = CalcDealt(chosenEnemyRoll,  enemyCard.Role,  playerCard.Role);
            }
            else // Disadvantage
            {
                playerRoll1      = RollDie(rng, playerRange);
                chosenPlayerRoll = playerRoll1;

                // Enemy rolls twice and keeps the highest.
                enemyRoll1 = RollDie(rng, enemyRange);
                enemyRoll2 = RollDie(rng, enemyRange);
                chosenEnemyRoll = Math.Max(enemyRoll1, enemyRoll2);

                playerDamageDealt = CalcDealt(chosenPlayerRoll, playerCard.Role, enemyCard.Role);
                enemyDamageDealt  = CalcDealt(chosenEnemyRoll,  enemyCard.Role,  playerCard.Role);
            }

            // Apply damage simultaneously.
            playerCard.Value -= enemyDamageDealt;
            enemyCard.Value  -= playerDamageDealt;

            return new CombatResult(
                slotIndex, rpsResult,
                playerRoll1, playerRoll2, enemyRoll1, enemyRoll2,
                chosenPlayerRoll, chosenEnemyRoll,
                playerSupportBonus, enemySupportBonus,
                playerRange, enemyRange,
                playerDamageDealt, enemyDamageDealt,
                playerValueBefore, playerCard.Value,
                enemyValueBefore,  enemyCard.Value);
        }

        // Returns a read-only preview without mutating any card values.
        public static CombatPreview GetCombatPreview(
            CardInstance playerCard,
            CardInstance enemyCard,
            CardInstance[] playerBoard,
            CardInstance[] enemyBoard,
            int slotIndex)
        {
            if (playerCard == null) throw new ArgumentNullException(nameof(playerCard));
            if (enemyCard  == null) throw new ArgumentNullException(nameof(enemyCard));
            if (playerBoard == null) throw new ArgumentNullException(nameof(playerBoard));
            if (enemyBoard  == null) throw new ArgumentNullException(nameof(enemyBoard));

            RpsResult rpsResult = GetRpsResult(playerCard.Rps, enemyCard.Rps);

            int playerSupportBonus = GetSupportBonus(playerCard, playerBoard);
            int enemySupportBonus  = GetSupportBonus(enemyCard,  enemyBoard);
            int playerRange = playerCard.Value + playerSupportBonus;
            int enemyRange  = enemyCard.Value  + enemySupportBonus;

            int playerMinDealt, playerMaxDealt;
            int enemyMinDealt,  enemyMaxDealt;

            if (rpsResult == RpsResult.Neutral)
            {
                // Shared base = min(playerRoll, enemyRoll); range [1, min(playerRange, enemyRange)].
                int sharedMax = Math.Min(playerRange, enemyRange);
                // Player deals to enemy: max(0, sharedBase - enemyDefense)
                playerMinDealt = Math.Max(0, 1         - (enemyCard.Role  == CardRole.Defense ? 1 : 0));
                playerMaxDealt = Math.Max(0, sharedMax - (enemyCard.Role  == CardRole.Defense ? 1 : 0));
                enemyMinDealt  = Math.Max(0, 1         - (playerCard.Role == CardRole.Defense ? 1 : 0));
                enemyMaxDealt  = Math.Max(0, sharedMax - (playerCard.Role == CardRole.Defense ? 1 : 0));
            }
            else
            {
                playerMinDealt = Math.Max(0, 1           + (playerCard.Role == CardRole.Attack  ? 1 : 0) - (enemyCard.Role  == CardRole.Defense ? 1 : 0));
                playerMaxDealt = Math.Max(0, playerRange + (playerCard.Role == CardRole.Attack  ? 1 : 0) - (enemyCard.Role  == CardRole.Defense ? 1 : 0));
                enemyMinDealt  = Math.Max(0, 1           + (enemyCard.Role  == CardRole.Attack  ? 1 : 0) - (playerCard.Role == CardRole.Defense ? 1 : 0));
                enemyMaxDealt  = Math.Max(0, enemyRange  + (enemyCard.Role  == CardRole.Attack  ? 1 : 0) - (playerCard.Role == CardRole.Defense ? 1 : 0));
            }

            bool playerCanDie = playerCard.Value - enemyMaxDealt <= 0;
            bool enemyCanDie  = enemyCard.Value  - playerMaxDealt <= 0;

            return new CombatPreview(
                rpsResult,
                playerSupportBonus, enemySupportBonus,
                playerRange, enemyRange,
                playerMinDealt, playerMaxDealt,
                enemyMinDealt,  enemyMaxDealt,
                playerCanDie, enemyCanDie);
        }

        private static int RollDie(Random rng, int max)
        {
            return max <= 0 ? 0 : rng.Next(1, max + 1);
        }

        // Damage one side deals given roles of attacker and defender.
        private static int CalcDealt(int chosenRoll, CardRole attackerRole, CardRole defenderRole)
        {
            int dealt = chosenRoll;
            if (attackerRole == CardRole.Attack)  dealt += 1;
            if (defenderRole == CardRole.Defense) dealt  = Math.Max(0, dealt - 1);
            return dealt;
        }
    }
}
