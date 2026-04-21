using System;
using CardsUnity.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public sealed class SlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private Text labelText;
        [SerializeField] private CardView cardView;

        private Color _normalColor = new Color32(238, 238, 238, 255);
        private Color _hoverColor = new Color32(255, 255, 210, 255);

        public int SlotIndex { get; private set; }
        public CardOwner Owner { get; private set; }
        public bool IsHovering { get; private set; }
        public CardInstance Card => cardView != null ? cardView.Card : null;

        public event Action<SlotView, bool> OnHoverChanged;

        public void Initialize(int slotIndex, CardOwner owner, Image slotBackground, Text label, CardView card)
        {
            SlotIndex = slotIndex;
            Owner = owner;
            background = slotBackground;
            labelText = label;
            cardView = card;
            SetLabel();
            SetHover(false);
        }

        public void Bind(CardInstance card)
        {
            SetLabel();
            if (cardView != null)
                cardView.Bind(card);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHover(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHover(false);
        }

        private void SetHover(bool hovering)
        {
            IsHovering = hovering;
            if (background != null)
                background.color = hovering ? _hoverColor : _normalColor;
            OnHoverChanged?.Invoke(this, hovering);
        }

        private void SetLabel()
        {
            if (labelText != null)
                labelText.text = $"{Owner} {SlotIndex + 1}";
        }
    }
}
