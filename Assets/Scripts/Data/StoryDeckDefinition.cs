using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity
{
    [CreateAssetMenu(menuName = "CardsUnity/Story Deck Definition", fileName = "NewStoryDeck")]
    public class StoryDeckDefinition : ScriptableObject
    {
        public List<StoryCardDefinition> cards;
        public StoryDrawMode drawMode;
    }
}
