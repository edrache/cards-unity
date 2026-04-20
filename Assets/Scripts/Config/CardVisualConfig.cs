using UnityEngine;

namespace CardsUnity.Config
{
    [CreateAssetMenu(fileName = "CardVisualConfig", menuName = "Cards Unity/Config/Card Visual Config")]
    public sealed class CardVisualConfig : ScriptableObject
    {
        [SerializeField, Min(1f)]
        private float width = 150f;

        [SerializeField, Min(1f)]
        private float height = 210f;

        [SerializeField, Min(0f)]
        private float cornerRadius = 10f;

        public float Width => width;
        public float Height => height;
        public float CornerRadius => cornerRadius;

#if UNITY_EDITOR
        private void OnValidate()
        {
            width = Mathf.Max(1f, width);
            height = Mathf.Max(1f, height);
            cornerRadius = Mathf.Max(0f, cornerRadius);
        }
#endif
    }
}
