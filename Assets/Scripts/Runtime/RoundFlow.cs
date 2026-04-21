using System;
using System.Collections.Generic;
using CardsUnity.Data;

namespace CardsUnity.Runtime
{
    public static class RoundFlow
    {
        // Draws cards from playerDeck into playerHand until hand size equals the number
        // of empty playerBoard slots, or the deck is empty. Clears placementOrder and
        // advances phase to Placement.
        public static void DrawCards(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            int emptySlots = CountEmptySlots(state.PlayerBoard);
            while (state.PlayerHand.Count < emptySlots && state.PlayerDeck.Count > 0)
            {
                CardInstance card = state.PlayerDeck[0];
                state.PlayerDeck.RemoveAt(0);
                state.PlayerHand.Add(card);
            }

            state.PlacementOrder.Clear();
            state.Phase = GamePhase.Placement;
        }

        // Moves a card from playerHand to playerBoard[slotIndex] and records placement order.
        // Returns false when the card is not in hand or the slot is already occupied.
        public static bool PlaceCard(GameState state, CardInstance card, int slotIndex)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (card == null)  throw new ArgumentNullException(nameof(card));
            if ((uint)slotIndex >= (uint)state.PlayerBoard.Length) throw new ArgumentOutOfRangeException(nameof(slotIndex));

            if (!state.PlayerHand.Contains(card)) return false;
            if (state.PlayerBoard[slotIndex] != null) return false;

            state.PlayerHand.Remove(card);
            state.PlayerBoard[slotIndex] = card;
            state.PlacementOrder.Add(card);
            return true;
        }

        // Returns the card at playerBoard[slotIndex] to playerHand and removes it from placementOrder.
        // Returns false when the slot is empty.
        public static bool UnplaceCard(GameState state, int slotIndex)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if ((uint)slotIndex >= (uint)state.PlayerBoard.Length) throw new ArgumentOutOfRangeException(nameof(slotIndex));

            CardInstance card = state.PlayerBoard[slotIndex];
            if (card == null) return false;

            state.PlayerBoard[slotIndex] = null;
            state.PlayerHand.Add(card);
            state.PlacementOrder.Remove(card);
            return true;
        }

        // Returns true when every player board slot is filled, or when the player hand is empty.
        public static bool CanResolve(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            if (state.PlayerHand.Count == 0) return true;
            foreach (CardInstance slot in state.PlayerBoard)
                if (slot == null) return false;
            return true;
        }

        // Returns the ordered list of slot indices to resolve. Placement order is honoured first;
        // only slots where both player and enemy have a card are included. Any remaining contested
        // slots not covered by placement order are appended in ascending index order.
        public static List<int> GetResolutionOrder(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var result = new List<int>(state.PlayerBoard.Length);
            var added  = new HashSet<int>();

            foreach (CardInstance card in state.PlacementOrder)
            {
                int idx = Array.IndexOf(state.PlayerBoard, card);
                if (idx >= 0 && state.EnemyBoard[idx] != null && added.Add(idx))
                    result.Add(idx);
            }

            for (int i = 0; i < state.PlayerBoard.Length; i++)
            {
                if (state.PlayerBoard[i] != null && state.EnemyBoard[i] != null && added.Add(i))
                    result.Add(i);
            }

            return result;
        }

        // Fills the enemy board left to right from the enemy deck up to slotCount cards.
        // Used at run start and by CombatController after each round.
        public static void FillEnemyBoard(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            for (int i = 0; i < state.EnemyBoard.Length && state.EnemyDeck.Count > 0; i++)
            {
                if (state.EnemyBoard[i] != null) continue;
                state.EnemyBoard[i] = state.EnemyDeck[0];
                state.EnemyDeck.RemoveAt(0);
            }
        }

        private static int CountEmptySlots(CardInstance[] board)
        {
            int count = 0;
            foreach (CardInstance slot in board)
                if (slot == null) count++;
            return count;
        }
    }
}
