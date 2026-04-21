namespace CardsUnity.Combat
{
    public sealed class CombatPreview
    {
        public RpsResult RpsResult { get; }

        public int PlayerSupportBonus { get; }
        public int EnemySupportBonus { get; }
        public int PlayerEffectiveRange { get; }
        public int EnemyEffectiveRange { get; }

        public int PlayerMinDamageDealt { get; }
        public int PlayerMaxDamageDealt { get; }
        public int EnemyMinDamageDealt { get; }
        public int EnemyMaxDamageDealt { get; }

        public bool PlayerCanDie { get; }
        public bool EnemyCanDie { get; }

        public CombatPreview(
            RpsResult rpsResult,
            int playerSupportBonus,
            int enemySupportBonus,
            int playerEffectiveRange,
            int enemyEffectiveRange,
            int playerMinDamageDealt,
            int playerMaxDamageDealt,
            int enemyMinDamageDealt,
            int enemyMaxDamageDealt,
            bool playerCanDie,
            bool enemyCanDie)
        {
            RpsResult = rpsResult;
            PlayerSupportBonus = playerSupportBonus;
            EnemySupportBonus = enemySupportBonus;
            PlayerEffectiveRange = playerEffectiveRange;
            EnemyEffectiveRange = enemyEffectiveRange;
            PlayerMinDamageDealt = playerMinDamageDealt;
            PlayerMaxDamageDealt = playerMaxDamageDealt;
            EnemyMinDamageDealt = enemyMinDamageDealt;
            EnemyMaxDamageDealt = enemyMaxDamageDealt;
            PlayerCanDie = playerCanDie;
            EnemyCanDie = enemyCanDie;
        }
    }
}
