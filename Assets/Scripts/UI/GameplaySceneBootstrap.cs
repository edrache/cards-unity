using CardsUnity.Controllers;
using CardsUnity.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public sealed class GameplaySceneBootstrap : MonoBehaviour
    {
        [SerializeField] private bool buildOnAwake = true;
        [SerializeField] private bool drawOpeningHandOnStart = true;

        private GameManager _gameManager;
        private RoundController _roundController;

        private void Awake()
        {
            if (buildOnAwake)
                Build();
        }

        private void Start()
        {
            if (drawOpeningHandOnStart && _gameManager != null && _gameManager.State.Phase == GamePhase.Draw)
                _roundController.DrawCards();
        }

        public void Build()
        {
            EnsureEventSystem();

            GameObject systems = new GameObject("Gameplay Systems");
            systems.transform.SetParent(transform, false);
            _gameManager = systems.AddComponent<GameManager>();
            _roundController = systems.AddComponent<RoundController>();
            CombatController combatController = systems.AddComponent<CombatController>();
            RewardController rewardController = systems.AddComponent<RewardController>();
            _roundController.Initialize(_gameManager);
            combatController.Initialize(_gameManager);
            rewardController.Initialize(_gameManager);

            Canvas canvas = CreateCanvas();
            RectTransform root = CreatePanel("Root", canvas.transform, new Color32(34, 38, 45, 255));
            Stretch(root);
            VerticalLayoutGroup rootLayout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(24, 24, 18, 18);
            rootLayout.spacing = 16;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            RectTransform top = CreateHorizontalBand("Top", root, 12, 150);
            HUDView hudView = CreateHud(top);
            CombatResultView combatResultView = CreateCombatResultView(top);
            DeckPanelView deckPanelView = CreateDeckPanel(top);

            RectTransform enemyBand = CreateBoardBand("Enemy Board", root, 236);
            RectTransform playerBand = CreateBoardBand("Player Board", root, 236);
            BoardView boardView = root.gameObject.AddComponent<BoardView>();
            SlotView[] enemySlots = CreateSlots(enemyBand, CardOwner.Enemy);
            SlotView[] playerSlots = CreateSlots(playerBand, CardOwner.Player);
            boardView.Initialize(playerSlots, enemySlots);

            RectTransform handBand = CreateBoardBand("Hand", root, 252);
            HandView handView = handBand.gameObject.AddComponent<HandView>();
            RectTransform handContent = CreateHorizontalContent("HandContent", handBand, 10);
            Text emptyHand = CreateText("EmptyHand", handBand, "No cards in hand", 18, TextAnchor.MiddleCenter);
            CardView cardTemplate = CreateCardTemplate(canvas.transform);
            handView.Initialize(handContent, cardTemplate, emptyHand);

            RewardView rewardView = CreateRewardView(root, cardTemplate);
            TooltipView tooltipView = CreateTooltipView(root);

            GameplayUIView ui = root.gameObject.AddComponent<GameplayUIView>();
            ui.Initialize(_gameManager, _roundController, combatController, rewardController, boardView, handView, hudView, deckPanelView, rewardView, combatResultView, tooltipView);
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(eventSystem);
        }

        private static Canvas CreateCanvas()
        {
            GameObject canvasObject = new GameObject("Gameplay Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private static HUDView CreateHud(Transform parent)
        {
            RectTransform panel = CreatePanel("HUD", parent, new Color32(246, 247, 240, 255));
            panel.gameObject.AddComponent<HorizontalLayoutGroup>().spacing = 8;
            panel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            Text phase = CreateText("Phase", panel, "Phase", 18, TextAnchor.MiddleLeft);
            Text round = CreateText("Round", panel, "Round", 18, TextAnchor.MiddleLeft);
            Text outcome = CreateText("Outcome", panel, string.Empty, 18, TextAnchor.MiddleLeft);
            Button draw = CreateButton("DrawButton", panel, "Draw");
            Button resolve = CreateButton("ResolveButton", panel, "Resolve");
            Button continueCombat = CreateButton("ContinueButton", panel, "Continue");

            HUDView view = panel.gameObject.AddComponent<HUDView>();
            view.Initialize(phase, round, outcome, draw, resolve, continueCombat);
            return view;
        }

        private static DeckPanelView CreateDeckPanel(Transform parent)
        {
            RectTransform panel = CreatePanel("Deck Panel", parent, new Color32(232, 238, 241, 255));
            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 2;
            panel.gameObject.AddComponent<LayoutElement>().preferredWidth = 280;

            Text playerDeck = CreateText("PlayerDeck", panel, string.Empty, 16, TextAnchor.MiddleLeft);
            Text playerCemetery = CreateText("PlayerCemetery", panel, string.Empty, 16, TextAnchor.MiddleLeft);
            Text enemyDeck = CreateText("EnemyDeck", panel, string.Empty, 16, TextAnchor.MiddleLeft);
            Text enemyCemetery = CreateText("EnemyCemetery", panel, string.Empty, 16, TextAnchor.MiddleLeft);

            DeckPanelView view = panel.gameObject.AddComponent<DeckPanelView>();
            view.Initialize(playerDeck, playerCemetery, enemyDeck, enemyCemetery);
            return view;
        }

        private static CombatResultView CreateCombatResultView(Transform parent)
        {
            RectTransform panel = CreatePanel("Combat Result", parent, new Color32(239, 231, 217, 255));
            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 2;
            LayoutElement element = panel.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1;
            element.preferredHeight = 132;

            Text title = CreateText("CombatResultTitle", panel, string.Empty, 17, TextAnchor.MiddleLeft);
            Text body = CreateText("CombatResultBody", panel, string.Empty, 13, TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;

            CombatResultView view = panel.gameObject.AddComponent<CombatResultView>();
            view.Initialize(title, body);
            return view;
        }

        private static RewardView CreateRewardView(Transform parent, CardView cardTemplate)
        {
            RectTransform panel = CreateBoardBand("Reward", parent, 252);
            RewardView view = panel.gameObject.AddComponent<RewardView>();
            Text title = CreateText("RewardTitle", panel, "Reward draft", 20, TextAnchor.MiddleCenter);
            RectTransform content = CreateHorizontalContent("RewardChoices", panel, 12);
            view.Initialize(content, cardTemplate, title);
            panel.gameObject.SetActive(false);
            return view;
        }

        private static TooltipView CreateTooltipView(Transform parent)
        {
            RectTransform panel = CreatePanel("Tooltip", parent, new Color32(232, 238, 241, 255));
            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 10, 10);
            layout.spacing = 3;
            LayoutElement element = panel.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 112;
            element.flexibleWidth = 1;

            Text title = CreateText("TooltipTitle", panel, string.Empty, 17, TextAnchor.MiddleLeft);
            Text body = CreateText("TooltipBody", panel, string.Empty, 14, TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;

            TooltipView view = panel.gameObject.AddComponent<TooltipView>();
            view.Initialize(title, body);
            return view;
        }

        private static SlotView[] CreateSlots(Transform parent, CardOwner owner)
        {
            SlotView[] slots = new SlotView[3];
            RectTransform content = CreateHorizontalContent($"{owner}Slots", parent, 16);
            for (int i = 0; i < slots.Length; i++)
            {
                RectTransform slot = CreatePanel($"{owner} Slot {i + 1}", content, new Color32(238, 238, 238, 255));
                LayoutElement layout = slot.gameObject.AddComponent<LayoutElement>();
                layout.preferredWidth = 190;
                layout.preferredHeight = 210;
                VerticalLayoutGroup group = slot.gameObject.AddComponent<VerticalLayoutGroup>();
                group.padding = new RectOffset(10, 10, 8, 10);
                group.spacing = 6;

                Text label = CreateText("Label", slot, $"{owner} {i + 1}", 14, TextAnchor.MiddleCenter);
                CardView card = CreateCardView("Card", slot);
                SlotView slotView = slot.gameObject.AddComponent<SlotView>();
                slotView.Initialize(i, owner, slot.GetComponent<Image>(), label, card);
                slots[i] = slotView;
            }

            return slots;
        }

        private static CardView CreateCardTemplate(Transform parent)
        {
            CardView template = CreateCardView("Card Template", parent);
            template.gameObject.SetActive(false);
            return template;
        }

        private static CardView CreateCardView(string name, Transform parent)
        {
            RectTransform card = CreatePanel(name, parent, Color.white);
            LayoutElement layout = card.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 160;
            layout.preferredHeight = 200;

            VerticalLayoutGroup group = card.gameObject.AddComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(10, 10, 8, 8);
            group.spacing = 3;

            Text icon = CreateText("Icon", card, "?", 30, TextAnchor.MiddleCenter);
            Text cardName = CreateText("Name", card, "Card", 17, TextAnchor.MiddleCenter);
            Text value = CreateText("Value", card, "0", 24, TextAnchor.MiddleCenter);
            Text rps = CreateText("Rps", card, "RPS", 13, TextAnchor.MiddleCenter);
            Text role = CreateText("Role", card, "Role", 13, TextAnchor.MiddleCenter);
            Text preview = CreateText("Preview", card, string.Empty, 12, TextAnchor.MiddleCenter);
            Text owner = CreateText("Owner", card, "Owner", 12, TextAnchor.MiddleCenter);
            Text flavor = CreateText("Flavor", card, string.Empty, 11, TextAnchor.UpperCenter);

            CardView view = card.gameObject.AddComponent<CardView>();
            view.Initialize(card.GetComponent<Image>(), icon, cardName, value, rps, role, owner, flavor, preview);
            return view;
        }

        private static RectTransform CreateHorizontalBand(string name, Transform parent, int spacing, float height)
        {
            RectTransform band = CreatePanel(name, parent, new Color32(0, 0, 0, 0));
            HorizontalLayoutGroup layout = band.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            LayoutElement element = band.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.flexibleWidth = 1;
            return band;
        }

        private static RectTransform CreateBoardBand(string title, Transform parent, float height)
        {
            RectTransform band = CreatePanel(title, parent, new Color32(246, 247, 240, 255));
            VerticalLayoutGroup layout = band.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 10, 14);
            layout.spacing = 8;
            LayoutElement element = band.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.flexibleWidth = 1;
            CreateText($"{title} Title", band, title, 18, TextAnchor.MiddleLeft);
            return band;
        }

        private static RectTransform CreateHorizontalContent(string name, Transform parent, int spacing)
        {
            RectTransform content = CreatePanel(name, parent, new Color32(0, 0, 0, 0));
            HorizontalLayoutGroup layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            LayoutElement element = content.gameObject.AddComponent<LayoutElement>();
            element.flexibleHeight = 1;
            element.flexibleWidth = 1;
            return content;
        }

        private static Button CreateButton(string name, Transform parent, string label)
        {
            RectTransform rect = CreatePanel(name, parent, new Color32(64, 86, 106, 255));
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 110;
            layout.preferredHeight = 44;
            Button button = rect.gameObject.AddComponent<Button>();
            Text text = CreateText("Text", rect, label, 16, TextAnchor.MiddleCenter);
            text.color = Color.white;
            Stretch(text.rectTransform);
            return button;
        }

        private static Text CreateText(string name, Transform parent, string text, int size, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text uiText = textObject.GetComponent<Text>();
            uiText.text = text;
            uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            uiText.fontSize = size;
            uiText.alignment = alignment;
            uiText.color = new Color32(29, 31, 36, 255);
            uiText.resizeTextForBestFit = true;
            uiText.resizeTextMinSize = 9;
            uiText.resizeTextMaxSize = size;
            LayoutElement layout = textObject.AddComponent<LayoutElement>();
            layout.minHeight = Mathf.Max(22, size + 8);
            return uiText;
        }

        private static RectTransform CreatePanel(string name, Transform parent, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.color = color;
            return panel.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
