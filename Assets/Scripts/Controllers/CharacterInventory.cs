using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Session inventory of collected valuables, grouped by name and unit value.</summary>
    public sealed class CharacterInventory : MonoBehaviour
    {
        public sealed class Entry
        {
            public string Name { get; }
            public int UnitValue { get; }
            public int Count { get; internal set; }
            public Entry(string name, int value) { Name = name; UnitValue = value; Count = 1; }
        }

        private readonly List<Entry> items = new List<Entry>();
        private Vector2 scroll;
        public IReadOnlyList<Entry> Items => items;
        public int TotalValue { get; private set; }
        public int TotalCount { get; private set; }

        public bool Collect(Treasure treasure)
        {
            if (treasure == null || !treasure.isActiveAndEnabled) return false;
            string itemName = treasure.DisplayName;
            int value = treasure.Value;
            var entry = items.Find(item => item.Name == itemName && item.UnitValue == value);
            if (entry == null) items.Add(new Entry(itemName, value));
            else entry.Count++;
            TotalCount++;
            TotalValue += value;
            // Disable immediately to prevent a second collector taking the same object this frame.
            treasure.gameObject.SetActive(false);
            Destroy(treasure.gameObject);
            return true;
        }

        private void OnGUI()
        {
            float width = Mathf.Min(280f, Screen.width - 24f);
            float height = Mathf.Min(270f, Screen.height * 0.45f);
            GUILayout.BeginArea(new Rect(12f, 12f, width, height), GUI.skin.box);
            GUILayout.Label("EKWIPUNEK · " + TotalCount);
            if (items.Count == 0) GUILayout.Label("Brak zebranych skarbów");
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var item in items)
                GUILayout.Label(item.Name + " × " + item.Count);
            GUILayout.EndScrollView();
            GUILayout.Label("Wartość skarbów: " + TotalValue);
            GUILayout.EndArea();
        }
    }
}
