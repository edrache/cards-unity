using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public class StatusView : MonoBehaviour
    {
        [SerializeField] private Transform iconContainer;
        [SerializeField] private GameObject statusIconPrefab;

        private readonly List<GameObject> _activeIcons = new();

        public void Refresh(StatusCollection collection)
        {
            foreach (var icon in _activeIcons)
            {
                if (icon != null)
                    Destroy(icon);
            }

            _activeIcons.Clear();

            if (collection == null || iconContainer == null || statusIconPrefab == null)
                return;

            foreach (var status in collection.Statuses)
            {
                if (status == null || status.IsExpired || status.Definition == null)
                    continue;

                var iconObject = Instantiate(statusIconPrefab, iconContainer);
                _activeIcons.Add(iconObject);

                var image = iconObject.GetComponentInChildren<Image>();
                if (image != null)
                    image.sprite = status.Definition.icon;

                var label = iconObject.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = status.Definition.durationType == StatusDurationType.Permanent
                        ? "∞"
                        : status.RemainingTurns.ToString();
                }
            }
        }
    }
}
