using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity
{
    [CreateAssetMenu(menuName = "CardsUnity/Card Definition", fileName = "NewCard")]
    public class CardDefinition : ScriptableObject
    {
        public string title;
        public CardType type;
        public int value;
        public List<CardEffectTag> effects;
        public Sprite artwork;
        [TextArea] public string flavorText;
    }
}
