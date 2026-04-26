using System;
using CardsUnity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public class SlotView : MonoBehaviour, IDropHandler
    {
        [SerializeField] private Transform playerCardAnchor;
        [SerializeField] private Transform opponentCardAnchor;
        [SerializeField] private Image highlightImage;

        public int SlotIndex { get; set; }
        public Action<CardView, int> OnCardDropped;

        private CardView _playerView;
        private CardView _opponentView;
        private bool CanAcceptPlayerCard => _playerView == null;

        public void ShowPlayerCard(CardInstance card, CardView prefab)
        {
            if (_playerView != null)
                Destroy(_playerView.gameObject);

            if (card == null || prefab == null || playerCardAnchor == null)
            {
                _playerView = null;
                return;
            }

            _playerView = Instantiate(prefab, playerCardAnchor);
            _playerView.SetDraggable(false);
            _playerView.SetCardInstance(card);
        }

        public void UpdatePlayerValue(CardInstance card)
        {
            if (_playerView != null)
                _playerView.SetCardInstance(card);
        }

        public void ClearPlayerCard()
        {
            if (_playerView != null)
                Destroy(_playerView.gameObject);

            _playerView = null;
        }

        public void ShowOpponentCard(CardInstance card, CardView prefab)
        {
            if (_opponentView != null)
                Destroy(_opponentView.gameObject);

            if (card == null || prefab == null || opponentCardAnchor == null)
            {
                _opponentView = null;
                return;
            }

            _opponentView = Instantiate(prefab, opponentCardAnchor);
            _opponentView.SetDraggable(false);
            _opponentView.SetCardInstance(card);
        }

        public void UpdateOpponentValue(CardInstance card)
        {
            if (_opponentView != null)
                _opponentView.SetCardInstance(card);
        }

        public void ClearOpponentCard()
        {
            if (_opponentView != null)
                Destroy(_opponentView.gameObject);

            _opponentView = null;
        }

        public void SetHighlight(bool active)
        {
            if (highlightImage != null)
                highlightImage.enabled = active && CanAcceptPlayerCard;
        }

        // TODO Task 6: render effect descriptions from definition onto TMP labels
        public void SetEffectDescriptions(SlotDefinition definition) { }

        public void OnDrop(PointerEventData eventData)
        {
            if (!CanAcceptPlayerCard)
                return;

            var dragged = eventData.pointerDrag?.GetComponent<CardView>();
            if (dragged != null)
                OnCardDropped?.Invoke(dragged, SlotIndex);
        }
    }
}
