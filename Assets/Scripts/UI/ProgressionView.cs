using MoreMountains.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public class ProgressionView : MonoBehaviour
    {
        [SerializeField] private MMProgressBar progressBar;
        [SerializeField] private Image fillImage;
        [SerializeField] private TextMeshProUGUI tierLabel;
        [SerializeField] private TextMeshProUGUI forceCountLabel;
        [SerializeField] private TextMeshProUGUI witCountLabel;
        [SerializeField] private TextMeshProUGUI presenceCountLabel;

        public void Refresh(ProgressionState state)
        {
            if (state == null)
            {
                if (progressBar != null)
                    progressBar.SetBar01(0f);
                else if (fillImage != null)
                    fillImage.fillAmount = 0f;

                SetLabel(tierLabel, string.Empty);
                SetLabel(forceCountLabel, "0");
                SetLabel(witCountLabel, "0");
                SetLabel(presenceCountLabel, "0");
                return;
            }

            if (progressBar != null)
                progressBar.UpdateBar(state.CurrentXp, 0f, state.XpCap);
            else if (fillImage != null)
                fillImage.fillAmount = state.Progress;

            SetLabel(tierLabel, $"Lvl {state.CurrentTier + 1}");
            SetLabel(forceCountLabel, state.DefeatedCounters.Force.ToString());
            SetLabel(witCountLabel, state.DefeatedCounters.Wit.ToString());
            SetLabel(presenceCountLabel, state.DefeatedCounters.Presence.ToString());
        }

        private static void SetLabel(TextMeshProUGUI label, string value)
        {
            if (label != null)
                label.text = value;
        }
    }
}
