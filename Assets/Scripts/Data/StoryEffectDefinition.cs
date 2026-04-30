using UnityEngine;

namespace CardsUnity
{
    [CreateAssetMenu(menuName = "CardsUnity/Story Effect Definition", fileName = "NewStoryEffect")]
    public class StoryEffectDefinition : ScriptableObject
    {
        public StoryEffectTrigger trigger;
        public StoryEffectAction action;
        [Min(1)] public int actionValue = 1;
        [TextArea] public string description;
    }
}
