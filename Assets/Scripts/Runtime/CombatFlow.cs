using System;
using System.Collections.Generic;
using CardsUnity.Combat;
using CardsUnity.Data;

namespace CardsUnity.Runtime
{
    public static class CombatFlow
    {
        // Returns a queue of slot indices to resolve this round (same order as RoundFlow.GetResolutionOrder).
        public static Queue<int> BuildQueue(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return new Queue<int>(RoundFlow.GetResolutionOrder(state));
        }

        // Resolves the next slot in the queue. Returns null when the queue is empty or
        // the slot no longer has both cards (e.g. already resolved out of order).
        public static CombatResult ResolveStep(GameState state, Queue<int> queue, Random rng)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (queue == null) throw new ArgumentNullException(nameof(queue));
            if (rng   == null) throw new ArgumentNullException(nameof(rng));

            while (queue.Count > 0)
            {
                int slotIndex  = queue.Dequeue();
                CardInstance pc = state.PlayerBoard[slotIndex];
                CardInstance ec = state.EnemyBoard[slotIndex];
                if (pc == null || ec == null) continue;
                return CombatResolver.ResolvePair(pc, ec, state.PlayerBoard, state.EnemyBoard, slotIndex, rng);
            }
            return null;
        }

        // Cleans up after all combat steps: moves dead cards to cemeteries, returns
        // surviving player cards to hand, refills the enemy board, and increments the round.
        // Does NOT set GameState.Phase — call DetermineNextPhase and set the phase externally.
        public static void FinalizeRound(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            for (int i = 0; i < state.PlayerBoard.Length; i++)
            {
                CardInstance card = state.PlayerBoard[i];
                if (card == null) continue;
                state.PlayerBoard[i] = null;
                if (card.IsDestroyed)
                    state.PlayerCemetery.Add(card);
                else
                    state.PlayerHand.Add(card);
            }

            for (int i = 0; i < state.EnemyBoard.Length; i++)
            {
                CardInstance card = state.EnemyBoard[i];
                if (card == null) continue;
                if (card.IsDestroyed)
                {
                    state.EnemyBoard[i] = null;
                    state.EnemyCemetery.Add(card);
                }
            }

            RoundFlow.FillEnemyBoard(state);
            state.Round++;
        }

        // Determines the next game phase after FinalizeRound has been called.
        public static GamePhase DetermineNextPhase(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            bool playerHasCards = state.PlayerHand.Count > 0 || state.PlayerDeck.Count > 0;
            if (!playerHasCards)
                return GamePhase.End;

            bool enemyHasCards = state.EnemyDeck.Count > 0;
            if (!enemyHasCards)
            {
                foreach (CardInstance card in state.EnemyBoard)
                    if (card != null) { enemyHasCards = true; break; }
            }

            return enemyHasCards ? GamePhase.Draw : GamePhase.Reward;
        }
    }
}
