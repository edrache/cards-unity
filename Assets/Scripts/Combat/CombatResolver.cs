using System;

namespace CardsUnity
{
    public static class CombatResolver
    {
        private static readonly CardType[] Beats =
        {
            CardType.Appeal,
            CardType.Positioning,
            CardType.Pressure
        };

        public static BattleOutcome DetermineOutcome(CardType attacker, CardType defender)
        {
            if (attacker == defender)
            {
                return BattleOutcome.Draw;
            }

            return Beats[(int)attacker] == defender ? BattleOutcome.Win : BattleOutcome.Lose;
        }

        public static CombatResult ResolveCombat(CardInstance attacker, CardInstance defender, Random rng)
        {
            if (attacker == null) throw new ArgumentNullException(nameof(attacker));
            if (defender == null) throw new ArgumentNullException(nameof(defender));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var outcome = DetermineOutcome(attacker.Definition.type, defender.Definition.type);
            var damage = outcome == BattleOutcome.Win
                ? attacker.CurrentValue
                : rng.Next(1, attacker.CurrentValue + 1);

            defender.CurrentValue = Math.Max(0, defender.CurrentValue - damage);
            return new CombatResult(damage, defender.CurrentValue <= 0);
        }
    }
}
