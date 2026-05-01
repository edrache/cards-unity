using CardsUnity;
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
                return;
            }

            if (progressBar != null)
                progressBar.UpdateBar(clock.CurrentValue, 0f, clock.MaxValue);
            else if (fillImage != null)
                fillImage.fillAmount = clock.Progress;

            if (label != null)
                label.text = $"{clock.CurrentValue} / {clock.MaxValue}";
        }
    }
}
