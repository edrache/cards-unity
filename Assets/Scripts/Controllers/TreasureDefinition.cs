using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Shared economic and presentation identity, independent of a treasure's prefab name.</summary>
    [CreateAssetMenu(menuName = "Cards Unity/Treasure Definition", fileName = "Treasure Definition")]
    public sealed class TreasureDefinition : ScriptableObject
    {
        [Tooltip("Player-facing name. Inventory groups collected items by this name and their unit value.")]
        [SerializeField] private string displayName = "New treasure";
        [SerializeField, Min(1)] private int value = 100;
        [Tooltip("Counts toward the coin total in the run summary, independently of the prefab name.")]
        [SerializeField] private bool isCoin;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public int Value => Mathf.Max(1, value);
        public bool IsCoin => isCoin;
        private void OnValidate() => value = Mathf.Max(1, value);

#if UNITY_EDITOR
        /// <summary>Copies effective authored data. The caller records Undo and saves the asset.</summary>
        public void CaptureFrom(Treasure source)
        {
            if (source == null) return;
            displayName = source.DisplayName;
            value = source.Value;
            isCoin = source.IsCoin;
        }
#endif
    }
}
