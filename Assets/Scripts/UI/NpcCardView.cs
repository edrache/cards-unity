using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public class NpcCardView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI narrativeLabel;

        public void Refresh(NpcCardDefinition card)
        {
            bool hasCard = card != null;

            if (panel != null)
                panel.SetActive(hasCard);
            else
                gameObject.SetActive(hasCard);

            if (!hasCard)
                return;

            if (portraitImage != null)
            {
                portraitImage.sprite = card.portrait;
                portraitImage.enabled = card.portrait != null;
            }

            if (nameLabel != null)
                nameLabel.text = card.characterName;

            if (narrativeLabel != null)
                narrativeLabel.text = card.narrativeText;
        }
    }
}
