namespace CardsUnity.Combat
{
    public sealed class CombatResult
    {
        public int SlotIndex { get; }
        public RpsResult RpsResult { get; }

        public int PlayerRoll1 { get; }
        public int PlayerRoll2 { get; }
        public int EnemyRoll1 { get; }
        public int EnemyRoll2 { get; }
        public int ChosenPlayerRoll { get; }
        public int ChosenEnemyRoll { get; }

        public int PlayerSupportBonus { get; }
        public int EnemySupportBonus { get; }
        public int PlayerEffectiveRange { get; }
        public int EnemyEffectiveRange { get; }

        public int PlayerDamageDealt { get; }
        public int EnemyDamageDealt { get; }

        public int PlayerValueBefore { get; }
        public int PlayerValueAfter { get; }
        public int EnemyValueBefore { get; }
        public int EnemyValueAfter { get; }

        public bool PlayerDied { get; }
        public bool EnemyDied { get; }

        public CombatResult(
            int slotIndex,
            RpsResult rpsResult,
            int playerRoll1,
            int playerRoll2,
            int enemyRoll1,
            int enemyRoll2,
            int chosenPlayerRoll,
            int chosenEnemyRoll,
            int playerSupportBonus,
            int enemySupportBonus,
            int playerEffectiveRange,
            int enemyEffectiveRange,
            int playerDamageDealt,
            int enemyDamageDealt,
            int playerValueBefore,
            int playerValueAfter,
            int enemyValueBefore,
            int enemyValueAfter)
        {
            SlotIndex = slotIndex;
            RpsResult = rpsResult;
            PlayerRoll1 = playerRoll1;
            PlayerRoll2 = playerRoll2;
            EnemyRoll1 = enemyRoll1;
            EnemyRoll2 = enemyRoll2;
            ChosenPlayerRoll = chosenPlayerRoll;
            ChosenEnemyRoll = chosenEnemyRoll;
            PlayerSupportBonus = playerSupportBonus;
            EnemySupportBonus = enemySupportBonus;
            PlayerEffectiveRange = playerEffectiveRange;
            EnemyEffectiveRange = enemyEffectiveRange;
            PlayerDamageDealt = playerDamageDealt;
            EnemyDamageDealt = enemyDamageDealt;
            PlayerValueBefore = playerValueBefore;
            PlayerValueAfter = playerValueAfter;
            EnemyValueBefore = enemyValueBefore;
            EnemyValueAfter = enemyValueAfter;
            PlayerDied = playerValueAfter <= 0;
            EnemyDied = enemyValueAfter <= 0;
        }
    }
}
