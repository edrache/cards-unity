using UnityEngine;

namespace CardsUnity.Data
{
    [CreateAssetMenu(fileName = "CardData", menuName = "Cards Unity/Data/Card Data")]
    public sealed class CardData : ScriptableObject
    {
        [SerializeField]
        private string id = string.Empty;

        [SerializeField]
        private string cardName = string.Empty;

        [SerializeField]
        private RpsType rps = RpsType.Pressure;

        [SerializeField, Min(1)]
        private int value = 1;

        [SerializeField]
        private CardRole role = CardRole.None;

        [SerializeField]
        private CardOwner owner = CardOwner.Player;

        [SerializeField]
        private string emoji = string.Empty;

        [SerializeField, TextArea]
        private string flavor = string.Empty;

        public string Id => id;
        public string CardName => cardName;
        public RpsType Rps => rps;
        public int Value => value;
        public CardRole Role => role;
        public CardOwner Owner => owner;
        public string Emoji => emoji;
        public string Flavor => flavor;

#if UNITY_EDITOR
        private void OnValidate()
        {
            value = Mathf.Max(1, value);
        }
#endif
    }
}
