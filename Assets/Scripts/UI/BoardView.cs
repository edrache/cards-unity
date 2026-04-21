using CardsUnity.Data;
using CardsUnity.Combat;
using UnityEngine;

namespace CardsUnity.UI
{
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private SlotView[] playerSlots;
        [SerializeField] private SlotView[] enemySlots;

        public SlotView[] PlayerSlots => playerSlots;
        public SlotView[] EnemySlots => enemySlots;

        public void Initialize(SlotView[] players, SlotView[] enemies)
        {
            playerSlots = players;
            enemySlots = enemies;
        }

        public void Bind(GameState state)
        {
            if (state == null) return;
            BindSlots(playerSlots, state.PlayerBoard);
            BindSlots(enemySlots, state.EnemyBoard);
        }

        public void BindPreviewBadges(GameState state)
        {
            if (state == null || playerSlots == null) return;

            for (int i = 0; i < playerSlots.Length; i++)
            {
                SlotView slot = playerSlots[i];
                CardView cardView = slot != null ? slot.CardView : null;
                if (cardView == null || cardView.Card == null)
                    continue;

                if (state.Phase != GamePhase.Placement || i >= state.EnemyBoard.Length || state.PlayerBoard[i] == null || state.EnemyBoard[i] == null)
                {
                    cardView.SetPreviewBadge(string.Empty, false);
                    continue;
                }

                CombatPreview preview = CombatResolver.GetCombatPreview(
                    state.PlayerBoard[i],
                    state.EnemyBoard[i],
                    state.PlayerBoard,
                    state.EnemyBoard,
                    i);

                cardView.SetPreviewBadge($"DMG {preview.PlayerMinDamageDealt}-{preview.PlayerMaxDamageDealt}", preview.PlayerCanDie);
            }
        }

        private static void BindSlots(SlotView[] slots, CardInstance[] cards)
        {
            if (slots == null || cards == null) return;

            int count = Mathf.Min(slots.Length, cards.Length);
            for (int i = 0; i < count; i++)
            {
                if (slots[i] != null)
                    slots[i].Bind(cards[i]);
            }
        }
    }
}
