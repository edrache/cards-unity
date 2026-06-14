using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SockPair : MonoBehaviour
{
    [SerializeField] Renderer rendererLeft;
    [SerializeField] Renderer rendererRight;

    static readonly int OutlineEnabledId = Shader.PropertyToID("_OutlineEnabled");
    static readonly int OutlineColorId   = Shader.PropertyToID("_OutlineColor");

    Material _matLeft;
    Material _matRight;
    Color    _originalOutlineLeft;
    Color    _originalOutlineRight;

    GameObject _prefabA;
    GameObject _prefabB;
    Material   _sockMatA;
    Material   _sockMatB;

    [SerializeField] float holdFollowSpeed = 50f;
    [SerializeField] SockBlobShadow blobShadow;
    [SerializeField] float decomposeDelay  = 0.3f;

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
        _prefabA   = sockA.SourcePrefab;
        _prefabB   = sockB.SourcePrefab;
        _sockMatA  = sockA.GetSharedMaterial();
        _sockMatB  = sockB.GetSharedMaterial();

        if (rendererLeft  != null) rendererLeft.sharedMaterial  = _sockMatA;
        if (rendererRight != null) rendererRight.sharedMaterial = _sockMatB;
        CacheMaterials();
        DisableOutlines();

        Destroy(sockA.gameObject);
        Destroy(sockB.gameObject);
    }

    public void Decompose(Camera cam)
    {
        StartCoroutine(DecomposeRoutine());
    }

    IEnumerator DecomposeRoutine()
    {
        Vector3 pos = transform.position;

        SpawnSock(_prefabA, _sockMatA, pos);

        yield return new WaitForSeconds(decomposeDelay);

        SpawnSock(_prefabB, _sockMatB, pos);

        Destroy(gameObject);
    }

    void SpawnSock(GameObject prefab, Material mat, Vector3 pos)
    {
        if (prefab == null) return;
        Quaternion rot  = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        GameObject go   = Instantiate(prefab, pos, rot);
        Sock       sock = go.GetComponent<Sock>();
        if (sock != null)
        {
            sock.SourcePrefab = prefab;
            sock.SetMaterial(mat);
        }
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
        blobShadow?.SetVisible(true);
    }

    public void UpdateHold(Camera cam, float targetDistance, float approachSpeed)
    {
        _holdDistance = Mathf.MoveTowards(_holdDistance, targetDistance, approachSpeed * Time.deltaTime);
        Ray ray = cam.ScreenPointToRay(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        _holdTargetPos = ray.origin + ray.direction * _holdDistance;
    }

    public void StopHold(Vector3 throwForce)
    {
        _isHeld = false;
        blobShadow?.SetVisible(false);
        _rb.AddForce(throwForce, ForceMode.Impulse);
    }

    public void Highlight()
    {
        EnableOutlines();
    }

    public void Unhighlight()
    {
        DisableOutlines();
    }

    void EnableOutlines()
    {
        if (_matLeft != null)
        {
            _matLeft.SetFloat(OutlineEnabledId, 1f);
            _matLeft.EnableKeyword("DR_OUTLINE_ON");
        }
        if (_matRight != null)
        {
            _matRight.SetFloat(OutlineEnabledId, 1f);
            _matRight.EnableKeyword("DR_OUTLINE_ON");
        }
    }

    void DisableOutlines()
    {
        if (_matLeft != null)
        {
            _matLeft.SetFloat(OutlineEnabledId, 0f);
            _matLeft.DisableKeyword("DR_OUTLINE_ON");
        }
        if (_matRight != null)
        {
            _matRight.SetFloat(OutlineEnabledId, 0f);
            _matRight.DisableKeyword("DR_OUTLINE_ON");
        }
    }
}
