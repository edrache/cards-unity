using UnityEngine;

namespace CardsUnity.Config
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Cards Unity/Config/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        [SerializeField, Min(1)]
        private int slotCount = 3;

        public int SlotCount => slotCount;

#if UNITY_EDITOR
        private void OnValidate()
        {
            slotCount = Mathf.Max(1, slotCount);
        }
#endif
    }
}
