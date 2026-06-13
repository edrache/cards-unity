using DG.Tweening;
using Rewired;
using UnityEngine;

public class SockSelector : MonoBehaviour
{
    [SerializeField] float maxDistance = 10f;
    [SerializeField] LayerMask sockLayerMask = ~0;
    [SerializeField] float throwForce = 8f;

    [Header("Left Hand")]
    [SerializeField] string actionLeft = "InteractLeft";
    [SerializeField] Transform[] slotsLeft;

    [Header("Right Hand")]
    [SerializeField] string actionRight = "InteractRight";
    [SerializeField] Transform[] slotsRight;

    [Header("Segment Manipulation")]
    [SerializeField] string actionInteract = "InteractSock";

    [Header("Pair / Unpair")]
    [SerializeField] string actionPair  = "Pair";
    [SerializeField] float pairDuration = 0.5f;
    [SerializeField] Ease  pairEase     = Ease.InOutBack;

    [Header("Pair Slots — Left Sock")]
    [SerializeField] Transform[] pairSlotsLeft;

    [Header("Pair Slots — Right Sock")]
    [SerializeField] Transform[] pairSlotsRight;

    [Header("Throw Pair")]
    [SerializeField] SockPair sockPairPrefab;
    [SerializeField] Transform pairSpawnPoint;
    [SerializeField] float pairThrowForce = 8f;

    Player _player;
    Sock _current;
    SockPair _currentPair;
    Sock _heldLeft;
    Sock _heldRight;
    bool _isPaired;

    bool _isManipulating;
    Sock _manipulatingSock;

    void Awake() => _player = ReInput.players.GetPlayer(0);

    void Update()
    {
        HandleManipulation();

        if (_isManipulating) return;

        bool pressedLeft  = _player.GetButtonDown(actionLeft);
        bool pressedRight = _player.GetButtonDown(actionRight);
        bool pressedPair  = _player.GetButtonDown(actionPair);

        if (pressedPair) { TogglePair(); return; }

        if (pressedLeft || pressedRight)
        {
            if (_isPaired)
            {
                ThrowPair();
                return;
            }

            if (_currentPair != null && _heldLeft == null && _heldRight == null)
            {
                PickUpPair();
                return;
            }

            if (pressedLeft)
                HandleHand(ref _heldLeft, slotsLeft);

            if (pressedRight)
                HandleHand(ref _heldRight, slotsRight);
        }

        UpdateSelection();
        UpdateSegmentHover();
    }

    void HandleManipulation()
    {
        if (_player.GetButtonDown(actionInteract) && _current != null && !_isManipulating)
        {
            if (_current.StartManipulating(Camera.main))
            {
                _isManipulating = true;
                _manipulatingSock = _current;
            }
        }

        if (_isManipulating)
        {
            if (_player.GetButton(actionInteract))
                _manipulatingSock?.UpdateManipulation(Camera.main);

            if (_player.GetButtonUp(actionInteract))
            {
                _manipulatingSock?.StopManipulating();
                _isManipulating = false;
                _manipulatingSock = null;
            }
        }
    }

    void UpdateSegmentHover()
    {
        if (_current == null) return;
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        _current.UpdateHoveredSegment(ray);
    }

    void HandleHand(ref Sock held, Transform[] slots)
    {
        if (held != null)
        {
            ThrowHeld(ref held);
        }
        else if (_current != null)
        {
            _current.Unhighlight();
            _current.PickUp(slots);
            held = _current;
            _current = null;
        }
    }

    void ThrowHeld(ref Sock held)
    {
        held.Throw(Camera.main.transform.forward * throwForce);
        held = null;
    }

    void ThrowPair()
    {
        if (sockPairPrefab == null) return;

        Vector3 spawnPos = pairSpawnPoint != null
            ? pairSpawnPoint.position
            : (pairSlotsLeft[0].position + pairSlotsRight[0].position) * 0.5f;

        SockPair pair = Instantiate(sockPairPrefab, spawnPos, Quaternion.identity);
        pair.SetMaterials(_heldLeft.GetSharedMaterial(), _heldRight.GetSharedMaterial());
        pair.Throw(Camera.main.transform.forward * pairThrowForce);

        Destroy(_heldLeft.gameObject);
        Destroy(_heldRight.gameObject);

        _heldLeft  = null;
        _heldRight = null;
        _isPaired  = false;
    }

    void PickUpPair()
    {
        _currentPair.Unhighlight();

        var (sockLeft, sockRight) = _currentPair.Decompose(_currentPair.transform.position);

        sockLeft.PickUpPaired(slotsLeft, pairSlotsLeft);
        sockRight.PickUpPaired(slotsRight, pairSlotsRight);

        _heldLeft  = sockLeft;
        _heldRight = sockRight;
        _isPaired  = true;

        Destroy(_currentPair.gameObject);
        _currentPair = null;
    }

    void TogglePair()
    {
        if (_isPaired)
        {
            _heldLeft?.Unpair(pairDuration, pairEase);
            _heldRight?.Unpair(pairDuration, pairEase);
            _isPaired = false;
        }
        else if (_heldLeft != null && _heldRight != null)
        {
            _heldLeft.Pair(pairSlotsLeft, pairDuration, pairEase);
            _heldRight.Pair(pairSlotsRight, pairDuration, pairEase);
            _isPaired = true;
        }
    }

    void UpdateSelection()
    {
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));

        Sock hitSock     = null;
        SockPair hitPair = null;

        if (Physics.Raycast(ray, out RaycastHit info, maxDistance, sockLayerMask))
        {
            hitSock = info.collider.GetComponentInParent<Sock>();
            if (hitSock == null)
                hitPair = info.collider.GetComponentInParent<SockPair>();
        }

        if (hitSock == _heldLeft || hitSock == _heldRight)
            hitSock = null;

        if (hitSock != _current)
        {
            _current?.Unhighlight();
            _current?.ClearHoveredSegment();
            _current = hitSock;
            _current?.Highlight();
        }

        if (hitPair != _currentPair)
        {
            _currentPair?.Unhighlight();
            _currentPair = hitPair;
            _currentPair?.Highlight();
        }
    }
}
