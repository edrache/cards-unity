using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity
{
    public enum NpcEffectAction
    {
        None,
        AddCardToPlayerHand,
        DamageToThreat,
        IncrementOpponentClock,
    }

    [Serializable]
    public class NpcEffect
    {
        public NpcEffectAction action;
        public CardType targetCardType;
        public int actionValue;
        public CardDefinition cardToAdd;
    }

    [CreateAssetMenu(menuName = "CardsUnity/NPC Card Definition", fileName = "NewNpcCard")]
    public class NpcCardDefinition : ScriptableObject
    {
        public string characterName;
        public Sprite portrait;
        [TextArea] public string narrativeText;
        public List<NpcEffect> effects = new();
    }
}
