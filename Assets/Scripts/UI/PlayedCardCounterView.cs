using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace CardsUnity.UI
{
    public class PlayedCardCounterView : MonoBehaviour
    {
        [FormerlySerializedAs("pressureValueLabel")]
        [SerializeField] private TextMeshProUGUI forceValueLabel;
        [FormerlySerializedAs("appealValueLabel")]
        [SerializeField] private TextMeshProUGUI presenceValueLabel;
        [FormerlySerializedAs("positioningValueLabel")]
        [SerializeField] private TextMeshProUGUI witValueLabel;

        public void Refresh(PlayedCardCounterState state)
        {
            SetLabel(forceValueLabel, state?.Force ?? 0);
            SetLabel(presenceValueLabel, state?.Presence ?? 0);
            SetLabel(witValueLabel, state?.Wit ?? 0);
        }

        private static void SetLabel(TextMeshProUGUI label, int value)
        {
            if (label != null)
                label.text = value.ToString();
        }
    }
}
