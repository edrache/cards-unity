using System;
using System.Collections.Generic;
using CardsUnity.Data;

namespace CardsUnity.Runtime
{
    public static class RewardFlow
    {
        // Shuffles a fresh clone of every player-owned card (across all zones) and stores
        // the first up to 3 as reward choices in state.RewardChoices.
        public static void GenerateRewardChoices(GameState state, Random rng = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var pool = new List<CardInstance>();
            foreach (CardInstance c in state.PlayerDeck)    pool.Add(new CardInstance(c.Definition));
            foreach (CardInstance c in state.PlayerHand)    pool.Add(new CardInstance(c.Definition));
            foreach (CardInstance c in state.PlayerCemetery) pool.Add(new CardInstance(c.Definition));
            foreach (CardInstance c in state.PlayerBoard)   if (c != null) pool.Add(new CardInstance(c.Definition));

            DeckFactory.Shuffle(pool, rng);

            state.RewardChoices.Clear();
            int count = Math.Min(3, pool.Count);
            for (int i = 0; i < count; i++)
                state.RewardChoices.Add(pool[i]);
        }

        // Adds the chosen card to the end of the player draw pile and clears the reward choices.
        // The card is appended without reshuffling so it is drawn in a predictable future round.
        public static void SelectReward(GameState state, CardInstance chosen)
        {
            if (state  == null)   throw new ArgumentNullException(nameof(state));
            if (chosen == null)   throw new ArgumentNullException(nameof(chosen));
            if (!state.RewardChoices.Contains(chosen))
                throw new ArgumentException("The chosen card is not among the current reward choices.", nameof(chosen));

            state.PlayerDeck.Add(chosen);
            state.RewardChoices.Clear();
        }

        // Resets the enemy side for a new encounter: clears enemy board/deck/cemetery,
        // creates a fresh enemy deck from the provided definitions, fills the board,
        // and resets the round counter and phase to Draw.
        public static void SetupNewEncounter(GameState state, IReadOnlyList<CardData> enemyDefs, Random rng = null)
        {
            if (state     == null) throw new ArgumentNullException(nameof(state));
            if (enemyDefs == null) throw new ArgumentNullException(nameof(enemyDefs));

            state.EnemyDeck.Clear();
            state.EnemyCemetery.Clear();
            for (int i = 0; i < state.EnemyBoard.Length; i++)
                state.EnemyBoard[i] = null;

            state.EnemyDeck.AddRange(DeckFactory.CreateEnemyDeck(enemyDefs, rng));
            RoundFlow.FillEnemyBoard(state);

            state.Round = 1;
            state.Phase = GamePhase.Draw;
        }
    }
}
