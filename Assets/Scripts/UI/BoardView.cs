using System;
using CardsUnity;
using UnityEngine;

namespace CardsUnity.UI
{
    public class BoardView : MonoBehaviour
    {
        [SerializeField] private SlotView[] slotViews;
        [SerializeField] private CardView opponentCardPrefab;

        public Action<CardDefinition, int> OnCardPlayed;

        private void Awake()
        {
            if (slotViews == null)
                return;

            for (int i = 0; i < slotViews.Length; i++)
            {
                var slotView = slotViews[i];
                if (slotView == null)
                    continue;

                int slotIndex = i;
                slotView.SlotIndex = slotIndex;
                slotView.ApplyDefinition(slotView.Definition);
                slotView.OnCardDropped += (cardView, droppedSlotIndex) =>
                    OnCardPlayed?.Invoke(cardView.Card, droppedSlotIndex);
            }
        }

        public void Refresh(BoardState board)
        {
            if (slotViews == null || board == null || board.Slots == null)
                return;

            int count = Math.Min(slotViews.Length, board.Slots.Length);
            for (int i = 0; i < count; i++)
            {
                var slot = board.Slots[i];
                if (slot == null)
                    continue;

                slotViews[i].SetHighlight(false);

                if (slot.HasPlayerCard)
                    slotViews[i].ShowPlayerCard(slot.PlayerCard, opponentCardPrefab);
                else
                    slotViews[i].ClearPlayerCard();

                if (slot.HasOpponentCard)
                    slotViews[i].ShowOpponentCard(slot.OpponentCard, opponentCardPrefab);
                else
                    slotViews[i].ClearOpponentCard();
            }

            for (int i = count; i < slotViews.Length; i++)
            {
                if (slotViews[i] == null)
                    continue;

                slotViews[i].SetHighlight(false);
                slotViews[i].ClearPlayerCard();
                slotViews[i].ClearOpponentCard();
            }
        }

        public void SetHighlightAll(bool active)
        {
            if (slotViews == null)
                return;

            foreach (var slotView in slotViews)
            {
                if (slotView != null)
                    slotView.SetHighlight(active);
            }
        }

        public void ConfigureSlots(SlotDefinition[] definitions)
        {
            if (slotViews == null)
                return;

            if (definitions == null)
            {
                for (int i = 0; i < slotViews.Length; i++)
                {
                    if (slotViews[i] != null)
                        slotViews[i].ApplyDefinition(null);
                }

                return;
            }

            int count = Math.Min(slotViews.Length, definitions.Length);
            for (int i = 0; i < count; i++)
            {
                if (slotViews[i] != null)
                    slotViews[i].ApplyDefinition(definitions[i]);
            }

            for (int i = count; i < slotViews.Length; i++)
            {
                if (slotViews[i] != null)
                    slotViews[i].ApplyDefinition(null);
            }
        }

        public SlotDefinition[] GetSceneSlotDefinitions()
        {
            if (slotViews == null)
                return Array.Empty<SlotDefinition>();

            var definitions = new SlotDefinition[slotViews.Length];
            for (int i = 0; i < slotViews.Length; i++)
                definitions[i] = slotViews[i] != null ? slotViews[i].Definition : null;

            return definitions;
        }

        public bool HasAnySceneSlotDefinitions()
        {
            if (slotViews == null)
                return false;

            foreach (var slotView in slotViews)
            {
                if (slotView != null && slotView.Definition != null)
                    return true;
            }

            return false;
        }
    }
}
