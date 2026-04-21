using System.Collections.Generic;
using CardsUnity.Controllers;
using CardsUnity.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public sealed class RewardView : MonoBehaviour
    {
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private Text titleText;

        private readonly List<Button> _buttons = new List<Button>();
        private RewardController _rewardController;

        public void Initialize(RectTransform root, CardView prefab, Text title)
        {
            contentRoot = root;
            cardPrefab = prefab;
            titleText = title;
        }

        public void BindController(RewardController rewardController)
        {
            _rewardController = rewardController;
        }

        public void Bind(GameState state)
        {
            if (state == null || state.Phase != GamePhase.Reward)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            if (titleText != null)
                titleText.text = "Reward draft";

            EnsureButtonCount(state.RewardChoices.Count);

            for (int i = 0; i < _buttons.Count; i++)
            {
                bool active = i < state.RewardChoices.Count;
                _buttons[i].gameObject.SetActive(active);
                if (!active) continue;

                CardInstance choice = state.RewardChoices[i];
                CardView view = _buttons[i].GetComponentInChildren<CardView>(true);
                if (view != null)
                    view.Bind(choice);

                _buttons[i].onClick.RemoveAllListeners();
                _buttons[i].onClick.AddListener(() => _rewardController?.SelectRewardAndStartEncounter(choice));
            }
        }

        private void EnsureButtonCount(int count)
        {
            if (contentRoot == null || cardPrefab == null) return;

            while (_buttons.Count < count)
            {
                GameObject buttonObject = new GameObject("RewardChoice", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(contentRoot, false);
                Image image = buttonObject.GetComponent<Image>();
                image.color = new Color32(255, 255, 255, 0);
                Button button = buttonObject.GetComponent<Button>();
                CardView view = Instantiate(cardPrefab, buttonObject.transform);
                view.gameObject.SetActive(true);
                LayoutElement viewLayout = view.GetComponent<LayoutElement>();
                LayoutElement buttonLayout = buttonObject.AddComponent<LayoutElement>();
                if (viewLayout != null)
                {
                    buttonLayout.minWidth = viewLayout.minWidth;
                    buttonLayout.preferredWidth = viewLayout.preferredWidth;
                    buttonLayout.minHeight = viewLayout.minHeight;
                    buttonLayout.preferredHeight = viewLayout.preferredHeight;
                }
                _buttons.Add(button);
            }
        }
    }
}
