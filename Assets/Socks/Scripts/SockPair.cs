using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SockPair : MonoBehaviour
{
    [SerializeField] Renderer rendererLeft;
    [SerializeField] Renderer rendererRight;

    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

    Material _matLeft;
    Material _matRight;
    Color    _originalOutlineLeft;
    Color    _originalOutlineRight;

    Sock _sockA;
    Sock _sockB;

    [SerializeField] float holdFollowSpeed = 50f;

    Rigidbody _rb;
    float     _holdDistance;
    Vector3   _holdTargetPos;
    bool      _isHeld;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    void CacheMaterials()
    {
        if (rendererLeft != null)
        {
            _matLeft             = rendererLeft.material;
            _originalOutlineLeft = _matLeft.GetColor(OutlineColorId);
        }
        if (rendererRight != null)
        {
            _matRight             = rendererRight.material;
            _originalOutlineRight = _matRight.GetColor(OutlineColorId);
        }
    }

    public void Setup(Sock sockA, Sock sockB)
    {
        _sockA = sockA;
        _sockB = sockB;

        if (rendererLeft  != null) rendererLeft.sharedMaterial  = sockA.GetSharedMaterial();
        if (rendererRight != null) rendererRight.sharedMaterial = sockB.GetSharedMaterial();
        CacheMaterials();

        sockA.gameObject.SetActive(false);
        sockB.gameObject.SetActive(false);
    }

    public void Decompose()
    {
        _sockA.transform.position = transform.position;
        _sockB.transform.position = transform.position;
        _sockA.gameObject.SetActive(true);
        _sockB.gameObject.SetActive(true);
        Destroy(gameObject);
    }

    void FixedUpdate()
    {
        if (!_isHeld) return;
        _rb.linearVelocity  = (_holdTargetPos - _rb.position) * holdFollowSpeed;
        _rb.angularVelocity = Vector3.zero;
    }

    public void StartHold(Camera cam)
    {
        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _holdDistance  = Vector3.Distance(cam.transform.position, transform.position);
        _holdTargetPos = transform.position;
        _isHeld        = true;
    }

    public void UpdateHold(Camera cam)
    {
        Ray ray = cam.ScreenPointToRay(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        _holdTargetPos = ray.origin + ray.direction * _holdDistance;
    }

    public void StopHold(Vector3 throwForce)
    {
        _isHeld = false;
        _rb.AddForce(throwForce, ForceMode.Impulse);
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
}
