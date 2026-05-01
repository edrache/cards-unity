using System;

namespace CardsUnity
{
    public static class PlayedCardCounterDominanceResolver
    {
        public static StoryEffectResolutionKey Resolve(PlayedCardCounterState state)
        {
            if (state == null)
                return StoryEffectResolutionKey.FullTie;

            int force = state.Force;
            int presence = state.Presence;
            int wit = state.Wit;
            int highest = Math.Max(force, Math.Max(presence, wit));

            bool forceLeads = force == highest;
            bool presenceLeads = presence == highest;
            bool witLeads = wit == highest;
            int leaders = (forceLeads ? 1 : 0) + (presenceLeads ? 1 : 0) + (witLeads ? 1 : 0);

            if (leaders == 1)
            {
                if (forceLeads)
                    return StoryEffectResolutionKey.Force;

                if (presenceLeads)
                    return StoryEffectResolutionKey.Presence;

                return StoryEffectResolutionKey.Wit;
            }

            if (leaders == 3)
                return StoryEffectResolutionKey.FullTie;

            if (forceLeads && presenceLeads)
                return ResolveRpsTie(CardType.Force, CardType.Presence);

            if (presenceLeads && witLeads)
                return ResolveRpsTie(CardType.Presence, CardType.Wit);

            return ResolveRpsTie(CardType.Wit, CardType.Force);
        }

        private static StoryEffectResolutionKey ResolveRpsTie(CardType first, CardType second)
        {
            var outcome = CombatResolver.DetermineOutcome(first, second);
            var winner = outcome == BattleOutcome.Win ? first : second;

            return winner switch
            {
                CardType.Force => StoryEffectResolutionKey.Force,
                CardType.Presence => StoryEffectResolutionKey.Presence,
                CardType.Wit => StoryEffectResolutionKey.Wit,
                _ => StoryEffectResolutionKey.FullTie
            };
        }
    }
}
