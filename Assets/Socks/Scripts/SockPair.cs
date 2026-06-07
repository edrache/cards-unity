using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SockPair : MonoBehaviour
{
    [SerializeField] Renderer rendererLeft;
    [SerializeField] Renderer rendererRight;

    [Header("Prefaby skarpet do odrodzenia przy podnoszeniu")]
    [SerializeField] GameObject sockPrefabLeft;
    [SerializeField] GameObject sockPrefabRight;

    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

    Material _matLeft;
    Material _matRight;
    Color _originalOutlineLeft;
    Color _originalOutlineRight;

    Material _storedMaterialLeft;
    Material _storedMaterialRight;

    Rigidbody _rb;

    void Awake()
    {
        _rb = GetComponentInChildren<Rigidbody>();
        CacheMaterials();
    }

    void CacheMaterials()
    {
        if (rendererLeft != null)
        {
            _matLeft              = rendererLeft.material;
            _originalOutlineLeft  = _matLeft.GetColor(OutlineColorId);
        }
        if (rendererRight != null)
        {
            _matRight             = rendererRight.material;
            _originalOutlineRight = _matRight.GetColor(OutlineColorId);
        }
    }

    public void SetMaterials(Material left, Material right)
    {
        _storedMaterialLeft  = left;
        _storedMaterialRight = right;

        if (rendererLeft  != null) rendererLeft.sharedMaterial  = left;
        if (rendererRight != null) rendererRight.sharedMaterial = right;

        CacheMaterials();
    }

    public void Highlight()
    {
        _matLeft?.SetColor(OutlineColorId,  Color.white);
        _matRight?.SetColor(OutlineColorId, Color.white);
    }

    public void Unhighlight()
    {
        _matLeft?.SetColor(OutlineColorId,  _originalOutlineLeft);
        _matRight?.SetColor(OutlineColorId, _originalOutlineRight);
    }

    public void Throw(Vector3 force) => _rb.AddForce(force, ForceMode.Impulse);

    // Tworzy dwie indywidualne skarpety z materiałami tej pary.
    public (Sock left, Sock right) Decompose(Vector3 spawnPos)
    {
        GameObject prefabL = sockPrefabLeft;
        GameObject prefabR = sockPrefabRight != null ? sockPrefabRight : sockPrefabLeft;

        Sock left  = Instantiate(prefabL, spawnPos, Quaternion.identity).GetComponent<Sock>();
        Sock right = Instantiate(prefabR, spawnPos, Quaternion.identity).GetComponent<Sock>();

        Material matLeft  = _storedMaterialLeft  ?? rendererLeft?.sharedMaterial;
        Material matRight = _storedMaterialRight ?? rendererRight?.sharedMaterial;

        if (matLeft  != null) left.SetMaterial(matLeft);
        if (matRight != null) right.SetMaterial(matRight);

        return (left, right);
    }
}
