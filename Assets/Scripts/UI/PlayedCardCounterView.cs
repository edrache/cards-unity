using TMPro;
using UnityEngine;

namespace CardsUnity.UI
{
    public class PlayedCardCounterView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI pressureValueLabel;
        [SerializeField] private TextMeshProUGUI appealValueLabel;
        [SerializeField] private TextMeshProUGUI positioningValueLabel;

        public void Refresh(PlayedCardCounterState state)
        {
            SetLabel(pressureValueLabel, state?.Pressure ?? 0);
            SetLabel(appealValueLabel, state?.Appeal ?? 0);
            SetLabel(positioningValueLabel, state?.Positioning ?? 0);
        }

        private static void SetLabel(TextMeshProUGUI label, int value)
        {
            if (label != null)
                label.text = value.ToString();
        }
    }
}
