using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity
{
    public enum StoryEffectResolutionKey
    {
        Force,
        Presence,
        Wit,
        FullTie,
    }

    public enum StoryEffectDeckTarget
    {
        Player,
        Opponent,
    }

    public enum StoryEffectDeckPlacement
    {
        DrawPile,
        DiscardPile,
    }

    [Serializable]
    public class StoryEffectDeckMutation
    {
        public StoryEffectDeckTarget targetDeck = StoryEffectDeckTarget.Player;
        public StoryEffectDeckPlacement placement = StoryEffectDeckPlacement.DiscardPile;
        public List<CardDefinition> cards = new();
    }

    [Serializable]
    public class StoryEffectSlotMutation
    {
        [Min(0)] public int slotIndex;
        public bool active = true;
        public SlotDefinition slotDefinition;
    }

    [Serializable]
    public class StoryEffectOutcome
    {
        public List<StoryEffectDeckMutation> deckMutations = new();
        public List<StoryEffectSlotMutation> slotMutations = new();
    }

    [CreateAssetMenu(menuName = "CardsUnity/Story Effect Definition", fileName = "NewStoryEffect")]
    public class StoryEffectDefinition : ScriptableObject
    {
        public StoryEffectTrigger trigger;
        public StoryEffectAction action;
        [Min(1)] public int actionValue = 1;
        [Tooltip("If true, the effect description is shown on the story card UI during gameplay.")]
        public bool showOnStoryCard = true;
        [TextArea] public string description;
        public StoryEffectOutcome forceOutcome = new();
        public StoryEffectOutcome presenceOutcome = new();
        public StoryEffectOutcome witOutcome = new();
        public StoryEffectOutcome fullTieOutcome = new();

        public StoryEffectOutcome GetOutcome(StoryEffectResolutionKey key)
        {
            return key switch
            {
                StoryEffectResolutionKey.Force => forceOutcome,
                StoryEffectResolutionKey.Presence => presenceOutcome,
                StoryEffectResolutionKey.Wit => witOutcome,
                StoryEffectResolutionKey.FullTie => fullTieOutcome,
                _ => null
            };
        }
    }
}
