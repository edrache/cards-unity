using System;
using CardsUnity.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public sealed class CardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private Text iconText;
        [SerializeField] private Text nameText;
        [SerializeField] private Text valueText;
        [SerializeField] private Text rpsText;
        [SerializeField] private Text roleText;
        [SerializeField] private Text ownerText;
        [SerializeField] private Text flavorText;
        [SerializeField] private Text previewBadgeText;

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Transform _originalParent;
        private int _originalSiblingIndex;
        private Canvas _rootCanvas;

        public CardInstance Card { get; private set; }
        public bool CanDrag { get; private set; }

        public event Action<CardView> OnDragStarted;
        public event Action<CardView> OnDragEnded;

        public void Initialize(
            Image cardBackground,
            Text icon,
            Text cardName,
            Text value,
            Text rps,
            Text role,
            Text owner,
            Text flavor,
            Text previewBadge = null)
        {
            background = cardBackground;
            iconText = icon;
            nameText = cardName;
            valueText = value;
            rpsText = rps;
            roleText = role;
            ownerText = owner;
            flavorText = flavor;
            previewBadgeText = previewBadge;
            CacheComponents();
        }

        public void Bind(CardInstance card)
        {
            CacheComponents();
            Card = card;

            if (card == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            SetText(iconText, string.IsNullOrWhiteSpace(card.Emoji) ? "?" : card.Emoji);
            SetText(nameText, card.CardName);
            SetText(valueText, card.Value.ToString());
            SetText(rpsText, card.Rps.ToString());
            SetText(roleText, card.Role.ToString());
            SetText(ownerText, card.Owner.ToString());
            SetText(flavorText, card.Flavor);
            SetPreviewBadge(string.Empty, false);

            if (background != null)
                background.color = GetCardColor(card);
        }

        public void SetCanDrag(bool canDrag)
        {
            CanDrag = canDrag;
        }

        public void SetPreviewBadge(string text, bool warning)
        {
            if (previewBadgeText == null) return;

            previewBadgeText.text = text;
            previewBadgeText.gameObject.SetActive(!string.IsNullOrWhiteSpace(text));
            previewBadgeText.color = warning ? new Color32(128, 32, 24, 255) : new Color32(36, 82, 52, 255);
        }

        public void Clear()
        {
            Bind(null);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!CanDrag || Card == null) return;
            CacheComponents();

            _rootCanvas = GetComponentInParent<Canvas>();
            _originalParent = transform.parent;
            _originalSiblingIndex = transform.GetSiblingIndex();

            if (_rootCanvas != null)
                transform.SetParent(_rootCanvas.transform, true);

            transform.SetAsLastSibling();
            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.alpha = 0.82f;
            }

            OnDragStarted?.Invoke(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!CanDrag || Card == null || _rectTransform == null) return;

            if (_rootCanvas != null && _rootCanvas.renderMode != RenderMode.WorldSpace)
                _rectTransform.anchoredPosition += eventData.delta / _rootCanvas.scaleFactor;
            else
                _rectTransform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!CanDrag || Card == null) return;

            RestoreAfterDrag();
            OnDragEnded?.Invoke(this);
        }

        public void RestoreAfterDrag()
        {
            if (_originalParent != null)
            {
                transform.SetParent(_originalParent, false);
                transform.SetSiblingIndex(_originalSiblingIndex);
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.alpha = 1f;
            }
        }

        private void CacheComponents()
        {
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
                target.text = value;
        }

        private static Color GetCardColor(CardInstance card)
        {
            if (card.Owner == CardOwner.Enemy)
                return new Color32(255, 232, 224, 255);

            switch (card.Rps)
            {
                case RpsType.Pressure:
                    return new Color32(255, 241, 210, 255);
                case RpsType.Appeal:
                    return new Color32(225, 245, 232, 255);
                case RpsType.Positioning:
                    return new Color32(224, 235, 255, 255);
                default:
                    return Color.white;
            }
        }
    }
}
