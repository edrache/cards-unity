using System;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    [RequireComponent(typeof(Button))]
    public class EndTurnButton : MonoBehaviour
    {
        public Action OnClicked;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_button != null)
                _button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(HandleClick);
        }

        private void HandleClick()
        {
            OnClicked?.Invoke();
        }
    }
}
