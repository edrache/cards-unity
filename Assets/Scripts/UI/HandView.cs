using System.Collections.Generic;
using CardsUnity.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public sealed class HandView : MonoBehaviour
    {
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private Text emptyText;

        private readonly List<CardView> _views = new List<CardView>();

        public void Initialize(RectTransform root, CardView prefab, Text emptyLabel)
        {
            contentRoot = root;
            cardPrefab = prefab;
            emptyText = emptyLabel;
        }

        public void Bind(IReadOnlyList<CardInstance> cards)
        {
            int count = cards != null ? cards.Count : 0;
            EnsureViewCount(count);

            for (int i = 0; i < _views.Count; i++)
            {
                bool active = i < count;
                _views[i].gameObject.SetActive(active);
                if (active)
                    _views[i].Bind(cards[i]);
            }

            if (emptyText != null)
                emptyText.gameObject.SetActive(count == 0);
        }

        private void EnsureViewCount(int count)
        {
            if (cardPrefab == null || contentRoot == null) return;

            while (_views.Count < count)
            {
                CardView view = Instantiate(cardPrefab, contentRoot);
                view.gameObject.SetActive(true);
                _views.Add(view);
            }
        }
    }
}
