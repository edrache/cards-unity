using DG.Tweening;
using UnityEngine;

public class Sock : MonoBehaviour
{
    [SerializeField] Renderer sockRenderer;
    [SerializeField] float pickUpDuration = 0.4f;

    [Header("Segmenty fizyczne (te same co w SockPhysicsBuilder)")]
    [SerializeField] Transform segCholewka;
    [SerializeField] Transform segSrodstopie;
    [SerializeField] Transform segNosek;

    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

    Color _originalOutlineColor;
    Material _mat;
    Rigidbody[] _rigidbodies;
    Collider[] _colliders;

    bool _isHeld;
    Transform _slotCholewka;
    Transform _slotSrodstopie;
    Transform _slotNosek;

    void Awake()
    {
        if (sockRenderer == null)
            sockRenderer = GetComponentInChildren<Renderer>();

        _mat         = sockRenderer.material;
        _originalOutlineColor = _mat.GetColor(OutlineColorId);
        _rigidbodies = GetComponentsInChildren<Rigidbody>();
        _colliders   = GetComponentsInChildren<Collider>();
    }

    // Kinematic Rigidbody nie śledzi rodzica gdy porusza się kamera —
    // więc co klatkę wymuszamy pozycję segmentu równą pozycji slotu.
    void LateUpdate()
    {
        if (!_isHeld) return;
        SnapToSlot(segCholewka,   _slotCholewka);
        SnapToSlot(segSrodstopie, _slotSrodstopie);
        SnapToSlot(segNosek,      _slotNosek);
    }

    public void Highlight()   => _mat.SetColor(OutlineColorId, Color.white);
    public void Unhighlight() => _mat.SetColor(OutlineColorId, _originalOutlineColor);

    public void PickUp(Transform slotCholewka, Transform slotSrodstopie, Transform slotNosek)
    {
        _slotCholewka   = slotCholewka;
        _slotSrodstopie = slotSrodstopie;
        _slotNosek      = slotNosek;

        SetColliders(false);
        SetKinematic(true);

        TweenSegmentToSlot(segCholewka,   slotCholewka);
        TweenSegmentToSlot(segSrodstopie, slotSrodstopie);
        TweenSegmentToSlot(segNosek,      slotNosek);

        _isHeld = true;
    }

    public void Throw(Vector3 force)
    {
        _isHeld = false;

        SetColliders(true);
        SetKinematic(false);

        foreach (Rigidbody rb in _rigidbodies)
            rb.AddForce(force, ForceMode.Impulse);
    }

    void TweenSegmentToSlot(Transform segment, Transform slot)
    {
        if (segment == null || slot == null) return;

        segment.DOMove(slot.position, pickUpDuration).SetEase(Ease.InOutQuad);
        segment.DORotateQuaternion(slot.rotation, pickUpDuration).SetEase(Ease.InOutQuad);
    }

    void SnapToSlot(Transform segment, Transform slot)
    {
        if (segment == null || slot == null) return;
        segment.SetPositionAndRotation(slot.position, slot.rotation);
    }

    void SetKinematic(bool value)
    {
        foreach (Rigidbody rb in _rigidbodies)
        {
            rb.isKinematic  = value;
            rb.interpolation = value
                ? RigidbodyInterpolation.None
                : RigidbodyInterpolation.Interpolate;
        }
    }

    void SetColliders(bool enabled)
    {
        foreach (Collider col in _colliders)
            col.enabled = enabled;
    }
}
