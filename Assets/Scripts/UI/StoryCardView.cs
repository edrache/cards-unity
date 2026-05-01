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
        [SerializeField] private TextMeshProUGUI effectsText;

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

            if (effectsText != null)
            {
                if (card.effects == null || card.effects.Count == 0)
                {
                    effectsText.text = string.Empty;
                }
                else
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var effect in card.effects)
                    {
                        if (effect != null && effect.showOnStoryCard && !string.IsNullOrEmpty(effect.description))
                            sb.AppendLine(effect.description);
                    }
                    effectsText.text = sb.ToString().TrimEnd();
                }
            }
        }
    }
}
