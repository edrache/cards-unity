using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity
{
    [CreateAssetMenu(menuName = "CardsUnity/Story Card Definition", fileName = "NewStoryCard")]
    public class StoryCardDefinition : ScriptableObject
    {
        public string title;
        [TextArea] public string content;
        public Sprite artwork;
        public List<CardEffectTag> effects;
        [Min(1)] public int clockMaxValue;
    }
}
