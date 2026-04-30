using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public class StoryCardView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI contentText;
        [SerializeField] private Image artworkImage;
        [SerializeField] private Image clockFillImage;
        [SerializeField] private TextMeshProUGUI clockLabel;

        public void Refresh(StoryCardDefinition card, ClockState clock)
        {
            gameObject.SetActive(card != null);

            if (card == null)
                return;

            if (titleText != null)
                titleText.text = card.title;

            if (contentText != null)
                contentText.text = card.content;

            if (artworkImage != null)
            {
                artworkImage.sprite = card.artwork;
                artworkImage.enabled = card.artwork != null;
            }

            if (clockFillImage != null)
                clockFillImage.fillAmount = clock != null ? clock.Progress : 0f;

            if (clockLabel != null)
                clockLabel.text = clock != null ? $"{clock.CurrentValue} / {clock.MaxValue}" : string.Empty;
        }
    }
}
