using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

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
        private static readonly List<CharacterInventory> activeViews = new List<CharacterInventory>();
        private readonly Vector3[] panelCorners = new Vector3[4];
        private RectTransform inventoryPanel;
        private Canvas inventoryCanvas;
        private Text heading, contents, total;
        private GameObject ownedEventSystem;
        private readonly System.Text.StringBuilder text = new System.Text.StringBuilder();
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
            RefreshView();
            return true;
        }

        /// <summary>Opaque inventory bounds in camera UVs, exempt from empty-world masking.</summary>
        public static Vector4 GetDitherUIRect(Camera camera)
        {
            foreach (var view in activeViews)
            {
                if (view == null || view.inventoryPanel == null || view.inventoryCanvas == null
                    || !view.inventoryCanvas.isActiveAndEnabled || view.inventoryCanvas.worldCamera != camera) continue;
                view.inventoryPanel.GetWorldCorners(view.panelCorners);
                Vector2 min = Vector2.one * float.PositiveInfinity;
                Vector2 max = Vector2.one * float.NegativeInfinity;
                foreach (var corner in view.panelCorners)
                {
                    Vector2 uv = camera.WorldToViewportPoint(corner);
                    min = Vector2.Min(min, uv);
                    max = Vector2.Max(max, uv);
                }
                return new Vector4(min.x, min.y, max.x, max.y);
            }
            return new Vector4(-1, -1, -1, -1);
        }

        private void OnEnable()
        {
            if (!activeViews.Contains(this)) activeViews.Add(this);
            if (inventoryCanvas == null) BuildView();
            inventoryCanvas.gameObject.SetActive(true);
            RefreshView();
        }

        private void LateUpdate()
        {
            // Resolve again if the main camera is replaced or spawned after the character.
            var camera = Camera.main;
            inventoryCanvas.enabled = camera != null;
            if (camera == null) return;
            inventoryCanvas.worldCamera = camera;
            inventoryCanvas.planeDistance = camera.nearClipPlane + 0.1f;
        }

        private void OnDisable()
        {
            activeViews.Remove(this);
            if (inventoryCanvas != null) inventoryCanvas.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (inventoryCanvas != null) Destroy(inventoryCanvas.gameObject);
            if (ownedEventSystem != null) Destroy(ownedEventSystem);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = LayerMask.NameToLayer("UI");
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static Text Label(RectTransform rect, int size, Font font)
        {
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.raycastTarget = false;
            label.supportRichText = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private void BuildView()
        {
            // Camera-space geometry is rendered before the camera's dither pass.
            var root = new GameObject("Inventory Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.layer = LayerMask.NameToLayer("UI");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            inventoryCanvas = root.GetComponent<Canvas>();
            inventoryCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            inventoryCanvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var panel = Rect("Inventory Panel", root.transform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(16, -286), new Vector2(316, -16));
            inventoryPanel = panel;
            panel.gameObject.AddComponent<Image>().color = Color.black;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            heading = Label(Rect("Heading", panel, new Vector2(0, 1), Vector2.one,
                new Vector2(16, -46), new Vector2(-16, -14)), 24, font);
            total = Label(Rect("Total Value", panel, Vector2.zero, new Vector2(1, 0),
                new Vector2(16, 12), new Vector2(-16, 40)), 20, font);
            var viewport = Rect("Viewport", panel, Vector2.zero, Vector2.one,
                new Vector2(16, 50), new Vector2(-16, -56));
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            var content = Rect("Items", viewport, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1);
            contents = Label(content, 22, font);
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25;
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                ownedEventSystem = new GameObject("Inventory Event System", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ownedEventSystem, gameObject.scene);
            }
            LateUpdate();
        }

        private void RefreshView()
        {
            if (heading == null) return;
            heading.text = "EKWIPUNEK · " + TotalCount;
            total.text = "Wartość skarbów: " + TotalValue;
            text.Clear();
            if (items.Count == 0) text.Append("Brak zebranych skarbów");
            foreach (var item in items)
            {
                if (text.Length > 0) text.Append('\n');
                text.Append(item.Name).Append(" × ").Append(item.Count);
            }
            contents.text = text.ToString();
        }
    }
}
