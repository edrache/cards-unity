using System;
using CardsUnity;
using TMPro;
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
        [SerializeField] private TextMeshProUGUI noOpponentCardEffectLabel;
        [SerializeField] private TextMeshProUGUI noPlayerCardEffectLabel;
        [SerializeField] private TextMeshProUGUI passiveEffectLabel;

        public int SlotIndex { get; set; }
        public Action<CardView, int> OnCardDropped;

        private SlotDefinition _definition;
        private CardView _playerView;
        private CardView _opponentView;
        private bool HasPlayerCardSpot => _definition == null || _definition.hasPlayerCardSpot;
        private bool HasOpponentCardSpot => _definition == null || _definition.hasOpponentCardSpot;
        private bool CanAcceptPlayerCard => HasPlayerCardSpot && _playerView == null;

        public void ShowPlayerCard(CardInstance card, CardView prefab)
        {
            if (_playerView != null)
                Destroy(_playerView.gameObject);

            if (!HasPlayerCardSpot || card == null || prefab == null || playerCardAnchor == null)
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

            if (!HasOpponentCardSpot || card == null || prefab == null || opponentCardAnchor == null)
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

        public void SetEffectDescriptions(SlotDefinition definition)
        {
            _definition = definition;
            EnsureEffectLabels();
            ApplySpotAvailability();

            ApplyEffectLabel(noOpponentCardEffectLabel, null);
            ApplyEffectLabel(noPlayerCardEffectLabel, null);
            ApplyEffectLabel(passiveEffectLabel, null);

            if (definition?.effects == null)
                return;

            foreach (var effect in definition.effects)
            {
                if (effect == null)
                    continue;

                switch (effect.context)
                {
                    case SlotEffectContext.NoOpponentCard:
                        ApplyEffectLabel(noOpponentCardEffectLabel, effect.description);
                        break;
                    case SlotEffectContext.NoPlayerCard:
                        ApplyEffectLabel(noPlayerCardEffectLabel, effect.description);
                        break;
                    case SlotEffectContext.Passive:
                        ApplyEffectLabel(passiveEffectLabel, effect.description);
                        break;
                }
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (!CanAcceptPlayerCard)
                return;

            var dragged = eventData.pointerDrag?.GetComponent<CardView>();
            if (dragged != null)
                OnCardDropped?.Invoke(dragged, SlotIndex);
        }

        private static void ApplyEffectLabel(TextMeshProUGUI label, string text)
        {
            if (label == null)
                return;

            label.text = text ?? string.Empty;
            label.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private void ApplySpotAvailability()
        {
            if (playerCardAnchor != null)
                playerCardAnchor.gameObject.SetActive(HasPlayerCardSpot);

            if (opponentCardAnchor != null)
                opponentCardAnchor.gameObject.SetActive(HasOpponentCardSpot);
        }

        private void EnsureEffectLabels()
        {
            if (noOpponentCardEffectLabel == null)
                noOpponentCardEffectLabel = CreateEffectLabel("NoOpponentEffectLabel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(0.5f, 1f));

            if (passiveEffectLabel == null)
                passiveEffectLabel = CreateEffectLabel("PassiveEffectLabel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0.5f, 0.5f));

            if (noPlayerCardEffectLabel == null)
                noPlayerCardEffectLabel = CreateEffectLabel("NoPlayerEffectLabel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(0.5f, 0f));
        }

        private TextMeshProUGUI CreateEffectLabel(string objectName, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 pivot)
        {
            var labelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(transform, false);

            var rectTransform = labelObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = new Vector2(180f, 28f);

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.fontSize = 11f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.91f, 0.89f, 0.83f, 0.92f);
            label.raycastTarget = false;

            if (TMP_Settings.defaultFontAsset != null)
                label.font = TMP_Settings.defaultFontAsset;

            labelObject.SetActive(false);
            return label;
        }
    }
}
