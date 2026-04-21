using System;
using CardsUnity.Config;
using CardsUnity.Data;
using DG.Tweening;
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
        [SerializeField] private AnimConfig animConfig;
        [SerializeField] private CardVisualConfig visualConfig;

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Image _previewBadgeBackground;
        private Transform _originalParent;
        private int _originalSiblingIndex;
        private Canvas _rootCanvas;
        private Vector2 _restoredAnchoredPosition;
        private Vector2 _lastDragDelta;

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
            Text previewBadge = null,
            AnimConfig animationConfig = null,
            CardVisualConfig cardVisualConfig = null)
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
            animConfig = animationConfig;
            visualConfig = cardVisualConfig;
            CacheComponents();
            ApplyVisualConfig(visualConfig);
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

            PlayAppear();
        }

        public void SetAnimConfig(AnimConfig animationConfig)
        {
            animConfig = animationConfig;
        }

        public void ApplyVisualConfig(CardVisualConfig cardVisualConfig)
        {
            visualConfig = cardVisualConfig;
            if (visualConfig == null) return;

            CacheComponents();
            if (_rectTransform != null)
                _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, visualConfig.Width);
            if (_rectTransform != null)
                _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, visualConfig.Height);

            LayoutElement layout = GetComponent<LayoutElement>();
            if (layout == null)
                layout = gameObject.AddComponent<LayoutElement>();
            layout.minWidth = visualConfig.Width;
            layout.preferredWidth = visualConfig.Width;
            layout.minHeight = visualConfig.Height;
            layout.preferredHeight = visualConfig.Height;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            if (background != null)
                RoundedImageUtility.ApplyRoundedSprite(background, visualConfig.CornerRadius);
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
            if (_previewBadgeBackground != null)
                _previewBadgeBackground.color = warning ? new Color32(255, 226, 219, 255) : new Color32(219, 246, 226, 255);
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
            _restoredAnchoredPosition = _rectTransform != null ? _rectTransform.anchoredPosition : Vector2.zero;
            _lastDragDelta = Vector2.zero;

            if (_rootCanvas != null)
                transform.SetParent(_rootCanvas.transform, true);

            transform.SetAsLastSibling();
            transform.DOKill();
            transform.DOScale(GetLiftScale(), GetLiftDuration()).SetEase(Ease.OutBack);
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

            _lastDragDelta = eventData.delta;
            if (_rootCanvas != null && _rootCanvas.renderMode != RenderMode.WorldSpace)
                _rectTransform.anchoredPosition += eventData.delta / _rootCanvas.scaleFactor;
            else
                _rectTransform.position = eventData.position;

            float tilt = Mathf.Clamp(-eventData.delta.x * 0.7f, -GetTiltMax(), GetTiltMax());
            transform.DOKill(false);
            transform.DOScale(GetLiftScale(), 0.08f).SetEase(Ease.OutQuad);
            transform.DOLocalRotate(new Vector3(0f, 0f, tilt), 0.08f).SetEase(Ease.OutQuad);
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

            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = _restoredAnchoredPosition;
                transform.DOKill();
                transform.DOScale(Vector3.one, GetSnapDuration()).SetEase(Ease.OutQuad);
                transform.DOLocalRotate(Vector3.zero, GetSnapDuration()).SetEase(Ease.OutQuad);
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.alpha = 1f;
            }
        }

        public void PlaySnap()
        {
            if (!isActiveAndEnabled) return;

            transform.DOKill();
            transform.localScale = Vector3.one * 0.96f;
            transform.localRotation = Quaternion.identity;
            transform.DOScale(Vector3.one, GetSnapDuration()).SetEase(Ease.OutBack);
        }

        public void PlayCombatReveal(bool defeated, float direction)
        {
            if (!isActiveAndEnabled) return;

            float duration = GetResolveFlashDuration();
            transform.DOKill();
            Sequence sequence = DOTween.Sequence();
            sequence.Append(transform.DOScale(Vector3.one * 1.08f, duration * 0.45f).SetEase(Ease.OutQuad));
            sequence.Join(transform.DOLocalRotate(new Vector3(0f, 0f, direction * GetResolveTiltAngle()), duration * 0.45f).SetEase(Ease.OutQuad));
            sequence.Append(transform.DOScale(defeated ? Vector3.one * 0.94f : Vector3.one, duration * 0.55f).SetEase(Ease.OutQuad));
            sequence.Join(transform.DOLocalRotate(defeated ? new Vector3(0f, 0f, direction * GetResolveTiltAngle()) : Vector3.zero, duration * 0.55f).SetEase(Ease.OutQuad));
        }

        public void PlayCleanup()
        {
            if (!isActiveAndEnabled) return;

            transform.DOKill();
            transform.DOScale(Vector3.one, GetCleanupDuration()).SetEase(Ease.OutQuad);
            transform.DOLocalRotate(Vector3.zero, GetCleanupDuration()).SetEase(Ease.OutQuad);
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
            if (previewBadgeText != null && _previewBadgeBackground == null)
                _previewBadgeBackground = previewBadgeText.GetComponent<Image>();
        }

        private void PlayAppear()
        {
            if (!isActiveAndEnabled) return;

            transform.DOKill(false);
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
        }

        private float GetLiftScale() => animConfig != null ? animConfig.LiftScale : 1.15f;
        private float GetTiltMax() => animConfig != null ? animConfig.TiltMax : 40f;
        private float GetSnapDuration() => animConfig != null ? animConfig.SnapDuration : 0.05f;
        private float GetLiftDuration() => animConfig != null ? animConfig.LiftDuration : 0.55f;
        private float GetResolveFlashDuration() => animConfig != null ? animConfig.ResolveFlashDuration : 0.45f;
        private float GetResolveTiltAngle() => animConfig != null ? animConfig.ResolveTiltAngle : 30f;
        private float GetCleanupDuration() => animConfig != null ? animConfig.CleanupDuration : 0.55f;

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
