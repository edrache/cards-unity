using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    [AddComponentMenu("UI/Rect Outline Graphic")]
    [RequireComponent(typeof(CanvasRenderer))]
    public class RectOutlineGraphic : MaskableGraphic
    {
        public enum StrokeMode
        {
            Solid = 0,
            Dashed = 1,
        }

        public enum DashFitMode
        {
            Fixed = 0,
            FitToEdge = 1,
        }

        private const string ShaderName = "CardsUnity/UI/Rect Outline";
        private const float MinPatternLength = 0.001f;

        [SerializeField, Min(0f)] private float lineThickness = 6f;
        [SerializeField, Min(0f)] private float cornerRadius = 0f;
        [SerializeField] private StrokeMode strokeMode = StrokeMode.Solid;
        [SerializeField] private DashFitMode dashFitMode = DashFitMode.Fixed;
        [SerializeField, Min(0.001f)] private float dashLength = 18f;
        [SerializeField, Min(0f)] private float gapLength = 10f;
        [SerializeField] private float dashOffset;

        private Material _runtimeMaterial;

        public float LineThickness
        {
            get => lineThickness;
            set
            {
                value = Mathf.Max(0f, value);
                if (Mathf.Approximately(lineThickness, value))
                    return;

                lineThickness = value;
                SetVerticesDirty();
            }
        }

        public StrokeMode OutlineStrokeMode
        {
            get => strokeMode;
            set
            {
                if (strokeMode == value)
                    return;

                strokeMode = value;
                SetMaterialDirty();
            }
        }

        public float CornerRadius
        {
            get => cornerRadius;
            set
            {
                value = Mathf.Max(0f, value);
                if (Mathf.Approximately(cornerRadius, value))
                    return;

                cornerRadius = value;
                SetVerticesDirty();
                SetMaterialDirty();
            }
        }

        public DashFitMode OutlineDashFitMode
        {
            get => dashFitMode;
            set
            {
                if (dashFitMode == value)
                    return;

                dashFitMode = value;
                SetMaterialDirty();
            }
        }

        public float DashLength
        {
            get => dashLength;
            set
            {
                value = Mathf.Max(MinPatternLength, value);
                if (Mathf.Approximately(dashLength, value))
                    return;

                dashLength = value;
                SetMaterialDirty();
            }
        }

        public float GapLength
        {
            get => gapLength;
            set
            {
                value = Mathf.Max(0f, value);
                if (Mathf.Approximately(gapLength, value))
                    return;

                gapLength = value;
                SetMaterialDirty();
            }
        }

        public float DashOffset
        {
            get => dashOffset;
            set
            {
                if (Mathf.Approximately(dashOffset, value))
                    return;

                dashOffset = value;
                SetMaterialDirty();
            }
        }

        public override Texture mainTexture => s_WhiteTexture;

        protected override void Awake()
        {
            base.Awake();
            EnsureMaterial();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureMaterial();
            SetAllDirty();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            DestroyRuntimeMaterial();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            DestroyRuntimeMaterial();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            lineThickness = Mathf.Max(0f, lineThickness);
            cornerRadius = Mathf.Max(0f, cornerRadius);
            dashLength = Mathf.Max(MinPatternLength, dashLength);
            gapLength = Mathf.Max(0f, gapLength);
            EnsureMaterial();
            SetAllDirty();
        }
#endif

        [Preserve]
        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            var rect = GetPixelAdjustedRect();
            float width = rect.width;
            float height = rect.height;
            if (width <= 0f || height <= 0f || lineThickness <= 0f)
                return;

            AddQuad(vertexHelper, color, rect.min, rect.max);
        }

        protected override void UpdateMaterial()
        {
            EnsureMaterial();
            UpdateMaterialProperties();
            base.UpdateMaterial();
        }

        private void AddQuad(VertexHelper vertexHelper, Color32 vertexColor, Vector2 min, Vector2 max)
        {
            int startIndex = vertexHelper.currentVertCount;
            var vertex = UIVertex.simpleVert;
            vertex.color = vertexColor;

            vertex.position = new Vector3(min.x, min.y);
            vertex.uv0 = new Vector2(0f, 0f);
            vertex.uv1 = new Vector2(min.x, min.y);
            vertexHelper.AddVert(vertex);

            vertex.position = new Vector3(min.x, max.y);
            vertex.uv0 = new Vector2(0f, 1f);
            vertex.uv1 = new Vector2(min.x, max.y);
            vertexHelper.AddVert(vertex);

            vertex.position = new Vector3(max.x, max.y);
            vertex.uv0 = new Vector2(1f, 1f);
            vertex.uv1 = new Vector2(max.x, max.y);
            vertexHelper.AddVert(vertex);

            vertex.position = new Vector3(max.x, min.y);
            vertex.uv0 = new Vector2(1f, 0f);
            vertex.uv1 = new Vector2(max.x, min.y);
            vertexHelper.AddVert(vertex);

            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
        }

        private void EnsureMaterial()
        {
            if (_runtimeMaterial != null)
                return;

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
                return;

            _runtimeMaterial = new Material(shader)
            {
                name = $"{ShaderName} (Instance)",
                hideFlags = HideFlags.HideAndDontSave,
            };
            material = _runtimeMaterial;
            UpdateMaterialProperties();
        }

        private void UpdateMaterialProperties()
        {
            if (_runtimeMaterial == null)
                return;

            _runtimeMaterial.SetFloat("_StrokeMode", (float)strokeMode);
            _runtimeMaterial.SetFloat("_DashFitMode", (float)dashFitMode);
            _runtimeMaterial.SetFloat("_DashLength", dashLength);
            _runtimeMaterial.SetFloat("_GapLength", gapLength);
            _runtimeMaterial.SetFloat("_DashOffset", dashOffset);
            var rect = GetPixelAdjustedRect();
            float thickness = Mathf.Min(lineThickness, rect.width * 0.5f, rect.height * 0.5f);
            float radius = Mathf.Min(cornerRadius, rect.width * 0.5f, rect.height * 0.5f);
            _runtimeMaterial.SetFloat("_LineThickness", thickness);
            _runtimeMaterial.SetFloat("_CornerRadius", radius);
            _runtimeMaterial.SetVector("_RectSize", new Vector4(rect.width, rect.height, 0f, 0f));
        }

        private void DestroyRuntimeMaterial()
        {
            if (_runtimeMaterial == null)
                return;

            if (material == _runtimeMaterial)
                material = null;

            if (Application.isPlaying)
                Destroy(_runtimeMaterial);
            else
                DestroyImmediate(_runtimeMaterial);

            _runtimeMaterial = null;
        }
    }
}
