using DG.Tweening;
using UnityEngine;

public class Sock : MonoBehaviour
{
    [SerializeField] Renderer sockRenderer;
    [SerializeField] SkinnedMeshRenderer skinnedMesh;
    [SerializeField] int blendShapeIndex = 0;

    [SerializeField] float pickUpDuration = 0.4f;
    [SerializeField] Ease pickUpEase = Ease.InOutQuad;

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

    // Aktywne sloty (do których LateUpdate snapuje segmenty)
    Transform _activeSlotCholewka;
    Transform _activeSlotSrodstopie;
    Transform _activeSlotNosek;

    // Sloty ręki zapamiętane z PickUp — potrzebne do Unpair
    Transform _handSlotCholewka;
    Transform _handSlotSrodstopie;
    Transform _handSlotNosek;

    void Awake()
    {
        if (sockRenderer == null)
            sockRenderer = GetComponentInChildren<Renderer>();

        _mat                  = sockRenderer.material;
        _originalOutlineColor = _mat.GetColor(OutlineColorId);
        _rigidbodies          = GetComponentsInChildren<Rigidbody>();
        _colliders            = GetComponentsInChildren<Collider>();
    }

    void LateUpdate()
    {
        if (!_isHeld) return;
        SnapToSlot(segCholewka,   _activeSlotCholewka);
        SnapToSlot(segSrodstopie, _activeSlotSrodstopie);
        SnapToSlot(segNosek,      _activeSlotNosek);
    }

    public Material GetSharedMaterial() => sockRenderer.sharedMaterial;

    public void SetMaterial(Material mat)
    {
        sockRenderer.sharedMaterial = mat;
        _mat                  = sockRenderer.material;
        _originalOutlineColor = _mat.GetColor(OutlineColorId);
    }

    public void Highlight()   => _mat.SetColor(OutlineColorId, Color.white);
    public void Unhighlight() => _mat.SetColor(OutlineColorId, _originalOutlineColor);

    public void PickUp(Transform slotCholewka, Transform slotSrodstopie, Transform slotNosek)
    {
        _handSlotCholewka   = slotCholewka;
        _handSlotSrodstopie = slotSrodstopie;
        _handSlotNosek      = slotNosek;

        _activeSlotCholewka   = slotCholewka;
        _activeSlotSrodstopie = slotSrodstopie;
        _activeSlotNosek      = slotNosek;

        SetColliders(false);
        SetKinematic(true);

        TweenSegmentToSlot(segCholewka,   slotCholewka,   pickUpDuration, pickUpEase);
        TweenSegmentToSlot(segSrodstopie, slotSrodstopie, pickUpDuration, pickUpEase);
        TweenSegmentToSlot(segNosek,      slotNosek,      pickUpDuration, pickUpEase);

        _isHeld = true;
    }

    // Używane przy podnoszeniu gotowej pary — sock od razu trafia w stan sparowany.
    public void PickUpPaired(
        Transform handCholewka,   Transform handSrodstopie,   Transform handNosek,
        Transform pairCholewka,   Transform pairSrodstopie,   Transform pairNosek)
    {
        _handSlotCholewka   = handCholewka;
        _handSlotSrodstopie = handSrodstopie;
        _handSlotNosek      = handNosek;

        _activeSlotCholewka   = pairCholewka;
        _activeSlotSrodstopie = pairSrodstopie;
        _activeSlotNosek      = pairNosek;

        SetColliders(false);
        SetKinematic(true);

        TweenSegmentToSlot(segCholewka,   pairCholewka,   pickUpDuration, pickUpEase);
        TweenSegmentToSlot(segSrodstopie, pairSrodstopie, pickUpDuration, pickUpEase);
        TweenSegmentToSlot(segNosek,      pairNosek,      pickUpDuration, pickUpEase);

        if (skinnedMesh != null)
            skinnedMesh.SetBlendShapeWeight(blendShapeIndex, 100f);

        _isHeld = true;
    }

    public void Pair(Transform targetCholewka, Transform targetSrodstopie, Transform targetNosek,
                     float duration, Ease ease)
    {
        _activeSlotCholewka   = targetCholewka;
        _activeSlotSrodstopie = targetSrodstopie;
        _activeSlotNosek      = targetNosek;

        TweenSegmentToSlot(segCholewka,   targetCholewka,   duration, ease);
        TweenSegmentToSlot(segSrodstopie, targetSrodstopie, duration, ease);
        TweenSegmentToSlot(segNosek,      targetNosek,      duration, ease);

        TweenBlendShape(100f, duration, ease);
    }

    public void Unpair(float duration, Ease ease)
    {
        _activeSlotCholewka   = _handSlotCholewka;
        _activeSlotSrodstopie = _handSlotSrodstopie;
        _activeSlotNosek      = _handSlotNosek;

        TweenSegmentToSlot(segCholewka,   _handSlotCholewka,   duration, ease);
        TweenSegmentToSlot(segSrodstopie, _handSlotSrodstopie, duration, ease);
        TweenSegmentToSlot(segNosek,      _handSlotNosek,      duration, ease);

        TweenBlendShape(0f, duration, ease);
    }

    public void Throw(Vector3 force)
    {
        _isHeld = false;

        SetColliders(true);
        SetKinematic(false);

        foreach (Rigidbody rb in _rigidbodies)
            rb.AddForce(force, ForceMode.Impulse);
    }

    void TweenSegmentToSlot(Transform segment, Transform slot, float duration, Ease ease)
    {
        if (segment == null || slot == null) return;
        segment.DOMove(slot.position, duration).SetEase(ease);
        segment.DORotateQuaternion(slot.rotation, duration).SetEase(ease);
    }

    void TweenBlendShape(float target, float duration, Ease ease)
    {
        if (skinnedMesh == null) return;
        float current = skinnedMesh.GetBlendShapeWeight(blendShapeIndex);
        DOVirtual.Float(current, target, duration, v => skinnedMesh.SetBlendShapeWeight(blendShapeIndex, v))
                 .SetEase(ease);
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
            rb.isKinematic   = value;
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
