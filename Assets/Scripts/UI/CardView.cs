using CardsUnity;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public class CardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI valueText;
        [SerializeField] private TextMeshProUGUI typeText;
        [SerializeField] private TextMeshProUGUI flavorText;
        [SerializeField] private Image artworkImage;
        [SerializeField] private Image cardBackground;
        [SerializeField] private float exhaustedRotationAngle = -18f;

        public static readonly Color[] TypeColors =
        {
            new Color(0.85f, 0.3f, 0.3f),
            new Color(0.3f, 0.7f, 0.85f),
            new Color(0.3f, 0.85f, 0.5f),
        };

        public CardDefinition Card { get; private set; }
        public System.Action<CardView> OnDragStart;
        public System.Action<CardView> OnDragEnd;

        private Transform _originalParent;
        private int _originalSiblingIndex;
        private Canvas _rootCanvas;
        private CanvasGroup _canvasGroup;
        private bool _draggable = true;
        private Quaternion _baseLocalRotation;

        private void Awake()
        {
            _baseLocalRotation = transform.localRotation;
            EnsureDragDependencies();
        }

        private void EnsureDragDependencies()
        {
            _rootCanvas = GetComponentInParent<Canvas>(includeInactive: true);

            if (_canvasGroup != null)
                return;

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void SetDraggable(bool draggable)
        {
            _draggable = draggable;
        }

        public void SetCard(CardDefinition card)
        {
            Card = card;

            if (card == null)
            {
                ApplyExhaustedVisual(false);

                if (titleText) titleText.text = string.Empty;
                if (valueText) valueText.text = string.Empty;
                if (typeText) typeText.text = string.Empty;
                if (flavorText) flavorText.text = string.Empty;
                if (artworkImage)
                {
                    artworkImage.sprite = null;
                    artworkImage.enabled = false;
                }

                return;
            }

            if (titleText) titleText.text = card.title;
            if (valueText) valueText.text = card.value.ToString();
            if (typeText) typeText.text = card.type.ToString();
            if (flavorText) flavorText.text = card.flavorText;

            if (artworkImage)
            {
                artworkImage.sprite = card.artwork;
                artworkImage.enabled = card.artwork != null;
            }

            if (cardBackground)
            {
                int index = Mathf.Clamp((int)card.type, 0, TypeColors.Length - 1);
                cardBackground.color = TypeColors[index];
            }

            ApplyExhaustedVisual(false);
        }

        public void SetCardInstance(CardInstance card)
        {
            if (card == null)
            {
                SetCard((CardDefinition)null);
                return;
            }

            SetCard(card.Definition);
            if (valueText)
                valueText.text = card.CurrentValue.ToString();
            ApplyExhaustedVisual(card.IsExhausted);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            EnsureDragDependencies();

            if (!_draggable || _rootCanvas == null)
                return;

            _originalParent = transform.parent;
            _originalSiblingIndex = transform.GetSiblingIndex();
            transform.SetParent(_rootCanvas.transform, worldPositionStays: true);
            transform.SetAsLastSibling();
            _canvasGroup.blocksRaycasts = false;
            OnDragStart?.Invoke(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            EnsureDragDependencies();

            if (!_draggable || _rootCanvas == null)
                return;

            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                _rootCanvas.transform as RectTransform,
                eventData.position,
                _rootCanvas.worldCamera,
                out var worldPos);
            transform.position = worldPos;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            EnsureDragDependencies();

            if (!_draggable || _rootCanvas == null)
                return;

            if (_originalParent != null)
            {
                transform.SetParent(_originalParent, worldPositionStays: true);
                transform.SetSiblingIndex(_originalSiblingIndex);
            }

            _canvasGroup.blocksRaycasts = true;
            OnDragEnd?.Invoke(this);
        }

        private void ApplyExhaustedVisual(bool isExhausted)
        {
            transform.localRotation = isExhausted
                ? _baseLocalRotation * Quaternion.Euler(0f, 0f, exhaustedRotationAngle)
                : _baseLocalRotation;
        }
    }
}
