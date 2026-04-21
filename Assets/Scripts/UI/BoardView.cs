using CardsUnity.Data;
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
