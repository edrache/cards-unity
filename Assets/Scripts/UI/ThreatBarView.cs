using System.Collections.Generic;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public class ThreatBarView : MonoBehaviour
    {
        [SerializeField] private CardType type;
        [SerializeField] private RectTransform segmentContainer;
        [SerializeField] private GameObject segmentPrefab;
        [SerializeField] private Transform tickContainer;
        [SerializeField] private GameObject tickPrefab;
        [SerializeField] private TextMeshProUGUI valueLabel;
        [SerializeField] private float segmentSpacing = 4f;

        private readonly List<MMProgressBar> _segmentProgressBars = new();
        private readonly List<Image> _segmentFills = new();
        private readonly List<GameObject> _activeTicks = new();

        public CardType Type => type;

        public void Build(ThreatBarState bar)
        {
            if (segmentContainer == null || segmentPrefab == null || bar == null)
                return;

            EnsureValueLabel();

            foreach (Transform child in segmentContainer)
                Destroy(child.gameObject);

            _segmentProgressBars.Clear();
            _segmentFills.Clear();

            int totalMaxValue = 0;
            foreach (var segment in bar.Segments)
                totalMaxValue += segment.MaxValue;

            SyncTicks(totalMaxValue);

            if (totalMaxValue <= 0)
                return;

            float containerWidth = segmentContainer.rect.width - segmentSpacing * (bar.Segments.Count - 1);

            foreach (var segment in bar.Segments)
            {
                var segmentObject = Instantiate(segmentPrefab, segmentContainer);
                if (segmentObject.TryGetComponent<RectTransform>(out var rectTransform))
                {
                    float width = containerWidth * ((float)segment.MaxValue / totalMaxValue);
                    rectTransform.sizeDelta = new Vector2(width, rectTransform.sizeDelta.y);
                }

                _segmentProgressBars.Add(segmentObject.GetComponentInChildren<MMProgressBar>());
                _segmentFills.Add(segmentObject.GetComponentInChildren<Image>());
            }

            Refresh(bar);
        }

        public void Refresh(ThreatBarState bar)
        {
            if (bar == null)
            {
                ClearTicks();
                return;
            }

            int totalMaxValue = 0;
            foreach (var segment in bar.Segments)
                totalMaxValue += segment.MaxValue;

            SyncTicks(totalMaxValue);

            for (int i = 0; i < _segmentFills.Count && i < bar.Segments.Count; i++)
            {
                var segment = bar.Segments[i];
                var progressBar = _segmentProgressBars.Count > i ? _segmentProgressBars[i] : null;
                var fill = _segmentFills[i];

                if (progressBar != null)
                {
                    progressBar.UpdateBar(segment.CurrentValue, 0f, segment.MaxValue);
                }
                else if (fill != null)
                {
                    fill.fillAmount = segment.MaxValue > 0 ? (float)segment.CurrentValue / segment.MaxValue : 0f;
                }
            }

            UpdateValueLabel(bar);
        }

        private void UpdateValueLabel(ThreatBarState bar)
        {
            if (valueLabel == null || bar == null)
                return;

            int currentValue = 0;
            int maxValue = 0;

            foreach (var segment in bar.Segments)
            {
                currentValue += segment.CurrentValue;
                maxValue += segment.MaxValue;
            }

            valueLabel.text = $"{type} {currentValue}/{maxValue}";
        }

        private void SyncTicks(int tickCount)
        {
            if (tickContainer == null || tickPrefab == null)
                return;

            while (_activeTicks.Count > tickCount)
            {
                int lastIndex = _activeTicks.Count - 1;
                var tick = _activeTicks[lastIndex];
                _activeTicks.RemoveAt(lastIndex);

                if (tick != null)
                    Destroy(tick);
            }

            while (_activeTicks.Count < tickCount)
            {
                var tick = Instantiate(tickPrefab, tickContainer);
                _activeTicks.Add(tick);
            }
        }

        private void ClearTicks()
        {
            foreach (var tick in _activeTicks)
            {
                if (tick != null)
                    Destroy(tick);
            }

            _activeTicks.Clear();
        }

        private void EnsureValueLabel()
        {
            if (valueLabel != null)
                return;

            var labelObject = new GameObject("ThreatValueLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(transform, false);

            var rectTransform = labelObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1f, 0.5f);
            rectTransform.anchorMax = new Vector2(1f, 0.5f);
            rectTransform.pivot = new Vector2(0f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(12f, 0f);
            rectTransform.sizeDelta = new Vector2(96f, 28f);

            valueLabel = labelObject.GetComponent<TextMeshProUGUI>();
            valueLabel.alignment = TextAlignmentOptions.Left;
            valueLabel.fontSize = 18f;
            valueLabel.color = new Color(0.91f, 0.89f, 0.83f, 0.96f);
            valueLabel.raycastTarget = false;

            if (TMP_Settings.defaultFontAsset != null)
                valueLabel.font = TMP_Settings.defaultFontAsset;
        }
    }
}
