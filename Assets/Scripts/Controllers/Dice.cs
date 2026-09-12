using System;

namespace CardsUnity.Controllers
{
    /// <summary>Shared OSR dice rules. Callers own their random stream.</summary>
    public static class Dice
    {
        /// <summary>Rolls one d6 and succeeds on 1 through X. Zero never succeeds; six always does.</summary>
        public static bool RollXIn6(Random random, int x)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            return SucceedsXIn6(random.Next(1, 7), x);
        }

        /// <summary>Resolves an existing d6 result, including rolls supplied by other game systems.</summary>
        public static bool SucceedsXIn6(int roll, int x)
        {
            if (roll < 1 || roll > 6) throw new ArgumentOutOfRangeException(nameof(roll));
            return roll <= Math.Max(0, Math.Min(6, x));
        }
    }
}
