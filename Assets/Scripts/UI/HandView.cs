using System;
using System.Collections.Generic;
using CardsUnity;
using UnityEngine;

namespace CardsUnity.UI
{
    public class HandView : MonoBehaviour
    {
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private Transform cardContainer;

        public Action<CardView> OnCardDragStart;
        public Action<CardView> OnCardDragEnd;

        private readonly List<CardView> _views = new List<CardView>();

        public void Refresh(HandState hand)
        {
            for (int i = 0; i < _views.Count; i++)
            {
                if (_views[i] != null)
                    Destroy(_views[i].gameObject);
            }

            _views.Clear();

            if (hand == null || cardPrefab == null || cardContainer == null)
                return;

            foreach (var card in hand.Cards)
            {
                var view = Instantiate(cardPrefab, cardContainer);
                view.SetDraggable(true);
                view.SetCardInstance(card);
                view.OnDragStart += HandleDragStart;
                view.OnDragEnd += HandleDragEnd;
                _views.Add(view);
            }
        }

        private void HandleDragStart(CardView view)
        {
            OnCardDragStart?.Invoke(view);
        }

        private void HandleDragEnd(CardView view)
        {
            OnCardDragEnd?.Invoke(view);
        }
    }
}
