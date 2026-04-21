using CardsUnity.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public sealed class CardView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Text iconText;
        [SerializeField] private Text nameText;
        [SerializeField] private Text valueText;
        [SerializeField] private Text rpsText;
        [SerializeField] private Text roleText;
        [SerializeField] private Text ownerText;
        [SerializeField] private Text flavorText;

        public CardInstance Card { get; private set; }

        public void Initialize(
            Image cardBackground,
            Text icon,
            Text cardName,
            Text value,
            Text rps,
            Text role,
            Text owner,
            Text flavor)
        {
            background = cardBackground;
            iconText = icon;
            nameText = cardName;
            valueText = value;
            rpsText = rps;
            roleText = role;
            ownerText = owner;
            flavorText = flavor;
        }

        public void Bind(CardInstance card)
        {
            Card = card;

            if (card == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            SetText(iconText, string.IsNullOrWhiteSpace(card.Emoji) ? "?" : card.Emoji);
            SetText(nameText, card.CardName);
            SetText(valueText, card.Value.ToString());
            SetText(rpsText, card.Rps.ToString());
            SetText(roleText, card.Role.ToString());
            SetText(ownerText, card.Owner.ToString());
            SetText(flavorText, card.Flavor);

            if (background != null)
                background.color = GetCardColor(card);
        }

        public void Clear()
        {
            Bind(null);
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
                target.text = value;
        }

        private static Color GetCardColor(CardInstance card)
        {
            if (card.Owner == CardOwner.Enemy)
                return new Color32(255, 232, 224, 255);

            switch (card.Rps)
            {
                case RpsType.Pressure:
                    return new Color32(255, 241, 210, 255);
                case RpsType.Appeal:
                    return new Color32(225, 245, 232, 255);
                case RpsType.Positioning:
                    return new Color32(224, 235, 255, 255);
                default:
                    return Color.white;
            }
        }
    }
}
