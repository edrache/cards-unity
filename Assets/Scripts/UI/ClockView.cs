using CardsUnity;
using System.Collections.Generic;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public class ClockView : MonoBehaviour
    {
        [SerializeField] private MMProgressBar progressBar;
        [SerializeField] private Image fillImage;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Transform tickContainer;
        [SerializeField] private GameObject clockTickPrefab;

        private readonly List<GameObject> _activeTicks = new();

        public void Refresh(ClockState clock)
        {
            if (clock == null)
            {
                if (progressBar != null)
                    progressBar.SetBar01(0f);
                if (fillImage != null)
                    fillImage.fillAmount = 0f;
                if (label != null)
                    label.text = string.Empty;
                ClearTicks();
                return;
            }

            SyncTicks(clock.MaxValue);

            if (progressBar != null)
                progressBar.UpdateBar(clock.CurrentValue, 0f, clock.MaxValue);
            else if (fillImage != null)
                fillImage.fillAmount = clock.Progress;

            if (label != null)
                label.text = $"{clock.CurrentValue} / {clock.MaxValue}";
        }

        private void SyncTicks(int tickCount)
        {
            if (tickContainer == null || clockTickPrefab == null)
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
                var tick = Instantiate(clockTickPrefab, tickContainer);
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
    }
}
