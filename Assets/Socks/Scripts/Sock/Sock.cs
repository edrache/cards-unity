using System.Collections.Generic;
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

    [Tooltip("Które segmenty można manipulować. Puste = wszystkie.")]
    [SerializeField] Transform[] manipulableSegments;

    static readonly int OutlineEnabledId = Shader.PropertyToID("_OutlineEnabled");
    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

    public GameObject SourcePrefab;

    public static readonly List<Sock> AllSocks = new List<Sock>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetAllSocks() => AllSocks.Clear();

    Material _mat;
    Color _originalOutlineColor;
    Rigidbody[] _rigidbodies;
    Collider[] _colliders;
    SockSimulator _simulator;

    bool _isHeld;

    // Segment hover & manipulation
    Renderer[] _segmentRenderers;
    HashSet<int> _manipulableIndices;
    int _hoveredSegmentIndex = -1;
    int _manipulatedSegmentIndex = -1;
    Rigidbody _manipulatedRb;
    bool _manipulatedWasGravity;
    float _holdDistance;
    Vector3 _manipulationTargetPos;

    [Header("Manipulation Follow")]
    [SerializeField] float manipulationFollowSpeed = 50f;

    [SerializeField] SockBlobShadow blobShadow;

    Transform[] _activeSlots;
    Transform[] _handSlots;

    void Awake()
    {
        if (sockRenderer == null)
            sockRenderer = GetComponentInChildren<Renderer>();

        _mat         = sockRenderer.material;
        _originalOutlineColor = _mat.GetColor(OutlineColorId);
        _rigidbodies = GetComponentsInChildren<Rigidbody>();
        Unhighlight();
        _colliders            = GetComponentsInChildren<Collider>();
        _simulator            = GetComponent<SockSimulator>();

        _segmentRenderers = new Renderer[segments.Length];
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;
            _segmentRenderers[i] = segments[i].GetComponentInChildren<Renderer>();
            if (_segmentRenderers[i] != null)
                _segmentRenderers[i].enabled = false;
        }

        _manipulableIndices = new HashSet<int>();
        if (manipulableSegments == null || manipulableSegments.Length == 0)
        {
            for (int i = 0; i < segments.Length; i++)
                _manipulableIndices.Add(i);
        }
        else
        {
            for (int i = 0; i < segments.Length; i++)
                for (int j = 0; j < manipulableSegments.Length; j++)
                    if (segments[i] == manipulableSegments[j])
                        _manipulableIndices.Add(i);
        }

        AllSocks.Add(this);
    }

    void OnDestroy()
    {
        AllSocks.Remove(this);
    }

    void LateUpdate()
    {
        if (!_isHeld) return;
        for (int i = 0; i < segments.Length && i < _activeSlots.Length; i++)
            SnapToSlot(segments[i], _activeSlots[i]);
    }

    // --- Segment hover ---

    public void UpdateHoveredSegment(Ray ray)
    {
        if (_isHeld) return;
        int closest = FindClosestSegmentToRay(ray);
        if (closest == _hoveredSegmentIndex) return;
        SetSegmentRendererEnabled(_hoveredSegmentIndex, false);
        _hoveredSegmentIndex = closest;
        SetSegmentRendererEnabled(_hoveredSegmentIndex, true);
    }

    public void ClearHoveredSegment()
    {
        SetSegmentRendererEnabled(_hoveredSegmentIndex, false);
        _hoveredSegmentIndex = -1;
    }

    int FindClosestSegmentToRay(Ray ray)
    {
        int closest = -1;
        float minDist = float.MaxValue;
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;
            if (!_manipulableIndices.Contains(i)) continue;
            Vector3 toSeg = segments[i].position - ray.origin;
            float t = Vector3.Dot(toSeg, ray.direction);
            if (t <= 0f) continue;
            float dist = Vector3.Cross(ray.direction, toSeg).magnitude;
            if (dist < minDist) { minDist = dist; closest = i; }
        }
        return closest;
    }

    void SetSegmentRendererEnabled(int index, bool value)
    {
        if (index < 0 || index >= _segmentRenderers.Length) return;
        if (_segmentRenderers[index] != null)
            _segmentRenderers[index].enabled = value;
    }

    // --- Segment manipulation ---

    public bool StartManipulating(Camera cam)
    {
        if (_hoveredSegmentIndex < 0 || _isHeld) return false;
        _simulator?.WakeUp();
        _manipulatedSegmentIndex = _hoveredSegmentIndex;
        _manipulatedRb = segments[_manipulatedSegmentIndex].GetComponent<Rigidbody>();
        if (_manipulatedRb != null)
        {
            _manipulatedWasGravity = _manipulatedRb.useGravity;
            _manipulatedRb.useGravity = false;
            _manipulatedRb.linearVelocity = Vector3.zero;
            _manipulatedRb.angularVelocity = Vector3.zero;
        }
        _holdDistance = Vector3.Distance(cam.transform.position, segments[_manipulatedSegmentIndex].position);
        _manipulationTargetPos = segments[_manipulatedSegmentIndex].position;
        blobShadow?.SetVisible(true);
        return true;
    }

    public void UpdateManipulation(Camera cam, float targetDistance, float approachSpeed)
    {
        if (_manipulatedSegmentIndex < 0 || segments[_manipulatedSegmentIndex] == null) return;
        _holdDistance = Mathf.MoveTowards(_holdDistance, targetDistance, approachSpeed * Time.deltaTime);
        Ray ray = cam.ScreenPointToRay(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        _manipulationTargetPos = ray.origin + ray.direction * _holdDistance;
    }

    void FixedUpdate()
    {
        if (_manipulatedRb == null || _manipulatedSegmentIndex < 0) return;
        _manipulatedRb.linearVelocity = (_manipulationTargetPos - _manipulatedRb.position) * manipulationFollowSpeed;
        _manipulatedRb.angularVelocity = Vector3.zero;
    }

    public void StopManipulating()
    {
        if (_manipulatedRb != null)
        {
            _manipulatedRb.useGravity = _manipulatedWasGravity;
            _manipulatedRb.linearVelocity = Vector3.zero;
            _manipulatedRb.angularVelocity = Vector3.zero;
        }
        _manipulatedSegmentIndex = -1;
        _manipulatedRb = null;
        blobShadow?.SetVisible(false);
        _simulator?.OnThrown();
    }

    public Rigidbody GetManipulatedBone() => _manipulatedRb;

    // ---

    public Material GetSharedMaterial() => sockRenderer.sharedMaterial;

    public void SetMaterial(Material mat)
    {
        sockRenderer.sharedMaterial = mat;
        _mat = sockRenderer.material;
        Unhighlight();
    }

    public void Highlight()
    {
        _mat.SetFloat(OutlineEnabledId, 1f);
        _mat.EnableKeyword("DR_OUTLINE_ON");
    }

    public void Unhighlight()
    {
        _mat.SetFloat(OutlineEnabledId, 0f);
        _mat.DisableKeyword("DR_OUTLINE_ON");
    }

    public void ShowPairHighlight(Color color)
    {
        _mat.SetFloat(OutlineEnabledId, 1f);
        _mat.EnableKeyword("DR_OUTLINE_ON");
        _mat.SetColor(OutlineColorId, color);
    }

    public void HidePairHighlight()
    {
        _mat.SetFloat(OutlineEnabledId, 0f);
        _mat.DisableKeyword("DR_OUTLINE_ON");
        _mat.SetColor(OutlineColorId, _originalOutlineColor);
    }

    public void PickUp(Transform[] slots)
    {
        StopManipulating();
        ClearHoveredSegment();
        _simulator?.OnPickedUp();

        _handSlots   = slots;
        _activeSlots = slots;

        SetColliders(false);
        SetKinematic(true);

        for (int i = 0; i < segments.Length && i < slots.Length; i++)
            TweenSegmentToSlot(segments[i], slots[i], pickUpDuration, pickUpEase);

        _isHeld = true;
        blobShadow?.SetVisible(true);
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
        blobShadow?.SetVisible(true);
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
        blobShadow?.SetVisible(false);

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
