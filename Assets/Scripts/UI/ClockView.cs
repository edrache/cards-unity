using CardsUnity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public class ClockView : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private TextMeshProUGUI label;

        public void Refresh(ClockState clock)
        {
            if (clock == null)
            {
                if (fillImage != null)
                    fillImage.fillAmount = 0f;
                if (label != null)
                    label.text = string.Empty;
                return;
            }

            if (fillImage != null)
                fillImage.fillAmount = clock.Progress;

            if (label != null)
                label.text = $"{clock.CurrentValue} / {clock.MaxValue}";
        }
    }
}
