using System;

namespace CardsUnity
{
    public static class CombatResolver
    {
        private static readonly CardType[] Beats =
        {
            CardType.Presence,
            CardType.Wit,
            CardType.Force
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

            var damage = CalculateDamage(attacker, defender, rng);

            defender.CurrentValue = Math.Max(0, defender.CurrentValue - damage);
            return new CombatResult(damage, defender.CurrentValue <= 0);
        }

        public static CombatExchangeResult ResolveExchange(CardInstance first, CardInstance second, Random rng)
        {
            if (first == null) throw new ArgumentNullException(nameof(first));
            if (second == null) throw new ArgumentNullException(nameof(second));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            int firstDamage = CalculateDamage(first, second, rng);
            int secondDamage = CalculateDamage(second, first, rng);

            first.CurrentValue = Math.Max(0, first.CurrentValue - secondDamage);
            second.CurrentValue = Math.Max(0, second.CurrentValue - firstDamage);

            return new CombatExchangeResult(
                new CombatResult(firstDamage, second.CurrentValue <= 0),
                new CombatResult(secondDamage, first.CurrentValue <= 0));
        }

        private static int CalculateDamage(CardInstance attacker, CardInstance defender, Random rng)
        {
            var outcome = DetermineOutcome(attacker.Definition.type, defender.Definition.type);
            return outcome == BattleOutcome.Win
                ? attacker.CurrentValue
                : rng.Next(1, attacker.CurrentValue + 1);
        }
    }
}
