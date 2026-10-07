using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.Controllers
{
    // Debug overlay of the rhythm structure: one phrase drawn as bars of beat cells (4 x 4 by
    // default), the current beat lit, a playhead sweeping through the phrase and a TextMeshPro
    // status line. Without an assigned Canvas it builds its own screen overlay at runtime.
    public sealed class RhythmPhraseDebugView : MonoBehaviour
    {
        [Tooltip("Conductor to show. Empty uses the active one (the Shadow Knight's).")]
        [SerializeField] private RhythmConductor conductor;
        [Tooltip("Canvas to draw into. Empty builds a dedicated screen overlay canvas.")]
        [SerializeField] private Canvas canvas;
        [Tooltip("Show the overlay. Uncheck to hide it without removing the component.")]
        [SerializeField] private bool visible = true;

        [Header("Layout")]
        [SerializeField] private Vector2 panelSize = new Vector2(640f, 92f);
        [Tooltip("Panel offset from the bottom centre of the canvas.")]
        [SerializeField] private Vector2 panelOffset = new Vector2(0f, 24f);
        [SerializeField, Range(0f, 24f)] private float barGap = 10f;
        [SerializeField, Range(0f, 12f)] private float beatGap = 3f;

        [Header("Colours")]
        [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.7f);
        [SerializeField] private Color beatColor = new Color(1f, 1f, 1f, 0.12f);
        [SerializeField] private Color downbeatColor = new Color(1f, 0.75f, 0.3f, 0.3f);
        [SerializeField] private Color activeColor = new Color(1f, 0.85f, 0.45f, 1f);
        [SerializeField] private Color playheadColor = Color.white;
        [Tooltip("Seconds the lit beat takes to fade back.")]
        [SerializeField, Range(0.05f, 1f)] private float flashFade = 0.35f;

        private RectTransform panel, grid, playhead;
        private TMP_Text status;
        private Image[] cells;
        private float[] flash;
        private int builtBeatsPerBar, builtBarsPerPhrase;
        private long shownBeat = long.MinValue;
        private bool ownsCanvas;

        private RhythmConductor Conductor => conductor != null ? conductor : RhythmConductor.Current;

        private void OnEnable()
        {
            if (panel != null) panel.gameObject.SetActive(visible);
        }

        private void OnDisable()
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (ownsCanvas && canvas != null) Destroy(canvas.gameObject);
            else if (panel != null) Destroy(panel.gameObject);
        }

        private void LateUpdate()
        {
            RhythmConductor source = Conductor;
            bool show = visible && source != null && source.IsReady;
            if (!show)
            {
                if (panel != null) panel.gameObject.SetActive(false);
                return;
            }
            if (panel == null) BuildPanel();
            panel.gameObject.SetActive(true);
            if (builtBeatsPerBar != source.BeatsPerBar || builtBarsPerPhrase != source.BarsPerPhrase)
                BuildGrid(source.BeatsPerBar, source.BarsPerPhrase);

            int beatsPerPhrase = cells.Length;
            RhythmBeat beat = source.LastBeat;
            if (source.NextBeatIndex > 0 && beat.Index != shownBeat)
            {
                shownBeat = beat.Index;
                flash[(int)RhythmBeat.Mod(beat.Index, beatsPerPhrase)] = 1f;
            }
            float fade = Time.unscaledDeltaTime / flashFade;
            for (int i = 0; i < cells.Length; i++)
            {
                flash[i] = Mathf.Max(0f, flash[i] - fade);
                Color rest = i % builtBeatsPerBar == 0 ? downbeatColor : beatColor;
                cells[i].color = Color.Lerp(rest, activeColor, flash[i]);
            }

            // Beat N sounds at position N, so the cell of beat N spans N..N+1 and the playhead sits
            // at its left edge exactly when the beat lands.
            float t = (float)(source.PhrasePosition / beatsPerPhrase);
            int cell = Mathf.Min(beatsPerPhrase - 1, Mathf.FloorToInt(t * beatsPerPhrase));
            RectTransform cellRect = cells[cell].rectTransform;
            float within = t * beatsPerPhrase - cell;
            float x = cellRect.anchoredPosition.x + within * cellRect.sizeDelta.x;
            playhead.anchoredPosition = new Vector2(x, playhead.anchoredPosition.y);

            int bar = cell / builtBeatsPerBar;
            status.text = source.Bpm.ToString("0") + " BPM  x" + source.StepsPerBeat + "  " + source.Mode
                + (source.IsMoving ? "" : " (stoi)") + "    Takt " + (bar + 1) + "/" + builtBarsPerPhrase
                + "  Bit " + (cell % builtBeatsPerBar + 1) + "/" + builtBeatsPerBar;
        }

        private void BuildPanel()
        {
            if (canvas == null)
            {
                ownsCanvas = true;
                var root = new GameObject("Rhythm Debug Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                root.layer = LayerMask.NameToLayer("UI");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
                canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 200;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.matchWidthOrHeight = 0.5f;
            }
            panel = NewRect("Rhythm Phrase", canvas.transform);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0f);
            panel.sizeDelta = panelSize;
            panel.anchoredPosition = panelOffset;
            panel.gameObject.AddComponent<Image>().color = panelColor;
            panel.GetComponent<Image>().raycastTarget = false;

            var statusRect = NewRect("Status", panel);
            statusRect.anchorMin = new Vector2(0f, 1f);
            statusRect.anchorMax = Vector2.one;
            statusRect.pivot = new Vector2(0.5f, 1f);
            statusRect.sizeDelta = new Vector2(-24f, 26f);
            statusRect.anchoredPosition = new Vector2(0f, -6f);
            var label = statusRect.gameObject.AddComponent<TextMeshProUGUI>();
            var font = Resources.Load<TMP_FontAsset>("Fonts/CaveUI");
            if (font != null) label.font = font;
            label.fontSize = 18;
            label.color = Color.white;
            label.raycastTarget = false;
            label.richText = false;
            label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            status = label;

            grid = NewRect("Bars", panel);
            grid.anchorMin = Vector2.zero;
            grid.anchorMax = new Vector2(1f, 0f);
            grid.pivot = new Vector2(0.5f, 0f);
            grid.sizeDelta = new Vector2(-24f, panelSize.y - 46f);
            grid.anchoredPosition = new Vector2(0f, 10f);
        }

        private void BuildGrid(int beatsPerBar, int barsPerPhrase)
        {
            for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);
            builtBeatsPerBar = beatsPerBar;
            builtBarsPerPhrase = barsPerPhrase;
            int count = beatsPerBar * barsPerPhrase;
            cells = new Image[count];
            flash = new float[count];
            shownBeat = long.MinValue;

            float width = panelSize.x - 24f;
            float height = panelSize.y - 46f;
            float barWidth = (width - barGap * (barsPerPhrase - 1)) / barsPerPhrase;
            float cellWidth = (barWidth - beatGap * (beatsPerBar - 1)) / beatsPerBar;
            for (int i = 0; i < count; i++)
            {
                int bar = i / beatsPerBar, beat = i % beatsPerBar;
                var rect = NewRect("Bar " + (bar + 1) + " Beat " + (beat + 1), grid);
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                rect.sizeDelta = new Vector2(cellWidth, height);
                rect.anchoredPosition = new Vector2(bar * (barWidth + barGap) + beat * (cellWidth + beatGap), 0f);
                cells[i] = rect.gameObject.AddComponent<Image>();
                cells[i].raycastTarget = false;
            }

            playhead = NewRect("Playhead", grid);
            playhead.anchorMin = playhead.anchorMax = Vector2.zero;
            playhead.pivot = new Vector2(0.5f, 0f);
            playhead.sizeDelta = new Vector2(2f, height + 8f);
            playhead.anchoredPosition = new Vector2(0f, -4f);
            var line = playhead.gameObject.AddComponent<Image>();
            line.color = playheadColor;
            line.raycastTarget = false;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
