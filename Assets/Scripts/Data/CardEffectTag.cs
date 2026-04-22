using UnityEngine;

namespace CardsUnity
{
    public enum EffectTrigger
    {
        OnPlay,
        OnEndTurn,
        AfterAttack,
        OnDestroyed
    }

    [CreateAssetMenu(menuName = "CardsUnity/Card Effect Tag", fileName = "NewEffectTag")]
    public class CardEffectTag : ScriptableObject
    {
        public string tagName;
        public EffectTrigger trigger;
        [TextArea] public string description;
    }
}
