using UnityEngine;

public class Sock : MonoBehaviour
{
    [SerializeField] Renderer sockRenderer;

    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

    Color _originalOutlineColor;
    Material _mat;

    void Awake()
    {
        if (sockRenderer == null)
            sockRenderer = GetComponentInChildren<Renderer>();

        _mat = sockRenderer.material;
        _originalOutlineColor = _mat.GetColor(OutlineColorId);
    }

    public void Highlight()   => _mat.SetColor(OutlineColorId, Color.white);
    public void Unhighlight() => _mat.SetColor(OutlineColorId, _originalOutlineColor);
}
