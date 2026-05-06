using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity
{
    [CreateAssetMenu(menuName = "CardsUnity/NPC Deck Definition", fileName = "NewNpcDeck")]
    public class NpcDeckDefinition : ScriptableObject
    {
        public List<NpcCardDefinition> cards = new();
        public bool shuffleOnInit = true;
    }
}
