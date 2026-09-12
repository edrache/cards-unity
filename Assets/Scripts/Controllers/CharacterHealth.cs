using System;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.Controllers
{
    /// <summary>
    /// Segmented player health. A partially refilled segment is not protection: an incoming hit
    /// clears it as well as the rightmost full segment.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterHealth : MonoBehaviour
    {
        [Header("Balance")]
        [Tooltip("Optional shared player balance. Existing fields below are used when empty.")]
        [SerializeField] private PlayerGameplayBalanceProfile balanceProfile;

        [Header("Local fallback — Segmented Health")]
        [SerializeField, Range(1, 8)] private int slotCount = 3;
        [SerializeField, Min(0.01f)] private float refillSecondsPerSlot = 10f;
        [Header("Per-instance HUD appearance")]
        [SerializeField] private Color fullFillColor = new Color(1f, 0.55f, 0.16f, 1f);
        [SerializeField] private Color refillingFillColor = new Color(0.5f, 0.24f, 0.08f, 1f);
        [SerializeField] private Color emptyBackgroundColor = new Color(0.08f, 0.06f, 0.05f, 0.95f);

        private float[] slotFills;
        private Slider[] slotViews;
        private Image[] slotBackgrounds;
        private CharacterInventory inventory;
        private CaveExit caveExit;
        private RectTransform hudRoot;
        private bool initialized;

        public bool IsDead { get; private set; }
        public PlayerGameplayBalanceProfile BalanceProfile => balanceProfile;
        private PlayerHealthBalance HealthBalance => balanceProfile != null ? balanceProfile.Health : null;
        public int ActiveSlotCount
        {
            get
            {
                int count = 0;
                if (slotFills == null) return count;
                for (int i = 0; i < slotFills.Length; i++)
                    if (slotFills[i] >= 1f) count++;
                return count;
            }
        }
        public int SlotCount => slotFills == null
            ? Mathf.Clamp(HealthBalance?.SlotCount ?? slotCount, 1, 8) : slotFills.Length;
        /// <summary>Raised once when an accepted hit leaves the character alive.</summary>
        public event Action Damaged;
        public event Action Died;

        private void Awake() => InitializeSlots();

        private void OnEnable()
        {
            EnsureHud();
            RefreshHud();
        }

        private void Update() => Tick(Time.deltaTime);

        private void OnDisable()
        {
            if (hudRoot != null) hudRoot.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (hudRoot != null) Destroy(hudRoot.gameObject);
        }

        /// <summary>Returns the fill of a segment, where one is an active segment.</summary>
        public float GetSlotFill(int index)
        {
            if (slotFills == null || index < 0 || index >= slotFills.Length) return 0f;
            return slotFills[index];
        }

        /// <summary>Consumes one full segment and abandons any in-progress refill.</summary>
        public bool TryTakeDamage()
        {
            InitializeSlots();
            if (!isActiveAndEnabled || IsDead || IsRunCompleted()) return false;

            int rightmostFull = -1;
            for (int i = slotFills.Length - 1; i >= 0; i--)
            {
                if (slotFills[i] >= 1f)
                {
                    rightmostFull = i;
                    break;
                }
            }

            if (rightmostFull < 0)
            {
                for (int i = 0; i < slotFills.Length; i++) slotFills[i] = 0f;
                Die();
                RefreshHud();
                return true;
            }

            slotFills[rightmostFull] = 0f;
            for (int i = 0; i < slotFills.Length; i++)
                if (slotFills[i] > 0f && slotFills[i] < 1f) slotFills[i] = 0f;

            bool isLethal = ActiveSlotCount == 0;
            if (isLethal) Die();
            RefreshHud();
            if (!isLethal) Damaged?.Invoke();
            return true;
        }

        /// <summary>Advances the leftmost empty segment only. This may also be driven directly in tests.</summary>
        public void Tick(float dt)
        {
            InitializeSlots();
            EnsureHud();
            if (IsDead || IsRunCompleted() || dt <= 0f) return;

            int refillIndex = -1;
            for (int i = 0; i < slotFills.Length; i++)
            {
                if (slotFills[i] < 1f)
                {
                    refillIndex = i;
                    break;
                }
            }
            if (refillIndex < 0) return;

            float remaining = dt;
            float duration = Mathf.Max(0.01f, HealthBalance?.RefillSecondsPerSlot ?? refillSecondsPerSlot);
            while (remaining > 0f && refillIndex >= 0)
            {
                float needed = (1f - slotFills[refillIndex]) * duration;
                if (remaining < needed)
                {
                    slotFills[refillIndex] = Mathf.Clamp01(slotFills[refillIndex] + remaining / duration);
                    break;
                }

                slotFills[refillIndex] = 1f;
                remaining -= needed;

                refillIndex = -1;
                for (int i = 0; i < slotFills.Length; i++)
                    if (slotFills[i] < 1f) { refillIndex = i; break; }
            }
            RefreshHud();
        }

        private void InitializeSlots()
        {
            if (initialized) return;
            initialized = true;
            int count = Mathf.Clamp(HealthBalance?.SlotCount ?? slotCount, 1, 8);
            slotFills = new float[count];
            for (int i = 0; i < count; i++) slotFills[i] = 1f;
        }

        private void OnValidate() => slotCount = Mathf.Clamp(slotCount, 1, 8);

#if UNITY_EDITOR
        internal void CopyLegacyBalanceTo(PlayerHealthBalance destination)
        {
            destination.Capture(slotCount, refillSecondsPerSlot);
        }

        internal void AssignBalanceProfile(PlayerGameplayBalanceProfile profile)
        {
            balanceProfile = profile;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private bool IsRunCompleted()
        {
            if (caveExit == null) caveExit = GetComponent<CaveExit>();
            return caveExit != null && caveExit.IsCompleted;
        }

        private void Die()
        {
            if (IsDead) return;
            IsDead = true;
            Died?.Invoke();
        }

        private void EnsureHud()
        {
            if (hudRoot != null)
            {
                if (!hudRoot.gameObject.activeSelf) hudRoot.gameObject.SetActive(true);
                return;
            }
            if (inventory == null) inventory = GetComponent<CharacterInventory>();
            if (inventory == null || inventory.UiCanvas == null) return;

            const float slotWidth = 52f;
            const float slotHeight = 14f;
            const float slotSpacing = 6f;
            float width = slotFills.Length * slotWidth + (slotFills.Length - 1) * slotSpacing;
            hudRoot = CreateRect("Health Slots", inventory.UiCanvas.transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f - width, -40f), new Vector2(-18f, -20f));
            var layout = hudRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = slotSpacing;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            slotViews = new Slider[slotFills.Length];
            slotBackgrounds = new Image[slotFills.Length];
            for (int i = 0; i < slotFills.Length; i++) CreateSlotView(i, slotWidth, slotHeight);
            RefreshHud();
        }

        private void CreateSlotView(int index, float width, float height)
        {
            var row = CreateRect("Health Slot " + (index + 1), hudRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var size = row.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = width;
            size.preferredHeight = height;
            var background = row.gameObject.AddComponent<Image>();
            background.color = emptyBackgroundColor;
            background.raycastTarget = false;

            var fillArea = CreateRect("Fill Area", row, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            var fill = CreateRect("Fill", fillArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.raycastTarget = false;
            var slider = row.gameObject.AddComponent<Slider>();
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fill;
            slider.direction = Slider.Direction.LeftToRight;

            slotViews[index] = slider;
            slotBackgrounds[index] = background;
        }

        private void RefreshHud()
        {
            if (slotViews == null) return;
            for (int i = 0; i < slotViews.Length; i++)
            {
                float fill = slotFills[i];
                slotViews[i].SetValueWithoutNotify(fill);
                var image = slotViews[i].fillRect.GetComponent<Image>();
                image.color = fill >= 1f ? fullFillColor : refillingFillColor;
                slotBackgrounds[i].color = emptyBackgroundColor;
            }
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = LayerMask.NameToLayer("UI");
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }
    }
}
