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
    [SerializeField] Transform[] segments;

    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

    Color _originalOutlineColor;
    Material _mat;
    Rigidbody[] _rigidbodies;
    Collider[] _colliders;
    SockSimulator _simulator;

    bool _isHeld;

    Transform[] _activeSlots;
    Transform[] _handSlots;

    void Awake()
    {
        if (sockRenderer == null)
            sockRenderer = GetComponentInChildren<Renderer>();

        _mat                  = sockRenderer.material;
        _originalOutlineColor = _mat.GetColor(OutlineColorId);
        _rigidbodies          = GetComponentsInChildren<Rigidbody>();
        _colliders            = GetComponentsInChildren<Collider>();
        _simulator            = GetComponent<SockSimulator>();
    }

    void LateUpdate()
    {
        if (!_isHeld) return;
        for (int i = 0; i < segments.Length && i < _activeSlots.Length; i++)
            SnapToSlot(segments[i], _activeSlots[i]);
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

    public void PickUp(Transform[] slots)
    {
        _simulator?.OnPickedUp();

        _handSlots   = slots;
        _activeSlots = slots;

        SetColliders(false);
        SetKinematic(true);

        for (int i = 0; i < segments.Length && i < slots.Length; i++)
            TweenSegmentToSlot(segments[i], slots[i], pickUpDuration, pickUpEase);

        _isHeld = true;
    }

    public void PickUpPaired(Transform[] handSlots, Transform[] pairSlots)
    {
        _simulator?.OnPickedUp();

        _handSlots   = handSlots;
        _activeSlots = pairSlots;

        SetColliders(false);
        SetKinematic(true);

        for (int i = 0; i < segments.Length && i < pairSlots.Length; i++)
            TweenSegmentToSlot(segments[i], pairSlots[i], pickUpDuration, pickUpEase);

        if (skinnedMesh != null)
            skinnedMesh.SetBlendShapeWeight(blendShapeIndex, 100f);

        _isHeld = true;
    }

    public void Pair(Transform[] targetSlots, float duration, Ease ease)
    {
        _activeSlots = targetSlots;

        for (int i = 0; i < segments.Length && i < targetSlots.Length; i++)
            TweenSegmentToSlot(segments[i], targetSlots[i], duration, ease);

        TweenBlendShape(100f, duration, ease);
    }

    public void Unpair(float duration, Ease ease)
    {
        _activeSlots = _handSlots;

        for (int i = 0; i < segments.Length && i < _handSlots.Length; i++)
            TweenSegmentToSlot(segments[i], _handSlots[i], duration, ease);

        TweenBlendShape(0f, duration, ease);
    }

    public void Throw(Vector3 force)
    {
        _simulator?.OnThrown();

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
