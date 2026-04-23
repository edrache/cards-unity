namespace CardsUnity
{
    public readonly struct CombatExchangeResult
    {
        public CombatResult FirstResult { get; }
        public CombatResult SecondResult { get; }

        public CombatExchangeResult(CombatResult firstResult, CombatResult secondResult)
        {
            FirstResult = firstResult;
            SecondResult = secondResult;
        }
    }
}
