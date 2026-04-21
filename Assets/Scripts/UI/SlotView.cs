using System;
using CardsUnity.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public sealed class SlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IDropHandler, IPointerClickHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private Text labelText;
        [SerializeField] private CardView cardView;

        private Color _normalColor = new Color32(238, 238, 238, 255);
        private Color _hoverColor = new Color32(255, 255, 210, 255);
        private Color _validDropColor = new Color32(204, 246, 219, 255);
        private Color _invalidDropColor = new Color32(255, 218, 213, 255);
        private bool _hasDropHint;

        public int SlotIndex { get; private set; }
        public CardOwner Owner { get; private set; }
        public bool IsHovering { get; private set; }
        public CardInstance Card => cardView != null ? cardView.Card : null;

        public event Action<SlotView, bool> OnHoverChanged;
        public event Action<SlotView, CardView> OnCardDropped;
        public event Action<SlotView> OnUnplaceRequested;

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
            {
                cardView.Bind(card);
                cardView.SetCanDrag(false);
            }
        }

        public void SetDropHint(bool active, bool valid)
        {
            _hasDropHint = active;
            if (background == null) return;

            if (!active)
                background.color = IsHovering ? _hoverColor : _normalColor;
            else
                background.color = valid ? _validDropColor : _invalidDropColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHover(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHover(false);
        }

        public void OnDrop(PointerEventData eventData)
        {
            CardView droppedCard = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<CardView>()
                : null;

            if (droppedCard != null)
                OnCardDropped?.Invoke(this, droppedCard);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Owner == CardOwner.Player && Card != null)
                OnUnplaceRequested?.Invoke(this);
        }

        private void SetHover(bool hovering)
        {
            IsHovering = hovering;
            if (background != null)
            {
                if (_hasDropHint)
                    return;
                background.color = hovering ? _hoverColor : _normalColor;
            }
            OnHoverChanged?.Invoke(this, hovering);
        }

        private void SetLabel()
        {
            if (labelText != null)
                labelText.text = $"{Owner} {SlotIndex + 1}";
        }
    }
}
