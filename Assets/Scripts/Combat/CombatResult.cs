namespace CardsUnity
{
    public readonly struct CombatResult
    {
        public int DamageDealt { get; }

        public bool Destroyed { get; }

        public CombatResult(int damage, bool destroyed)
        {
            DamageDealt = damage;
            Destroyed = destroyed;
        }
    }
}
