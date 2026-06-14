using Rewired;
using UnityEngine;

public class SockSelector : MonoBehaviour
{
    [SerializeField] float     maxDistance   = 10f;
    [SerializeField] LayerMask sockLayerMask = ~0;

    [Header("Segment Manipulation")]
    [SerializeField] string actionInteract = "InteractSock";

    [Header("Pair / Unpair")]
    [SerializeField] string actionPair            = "Pair";
    [SerializeField] string actionUnpair          = "Unpair";
    [SerializeField] float  pairProximityDistance = 0.5f;
    [SerializeField] Color  pairHighlightColor    = Color.green;

    [Header("Hold Distance")]
    [SerializeField] float holdCameraDistance = 2f;
    [SerializeField] float holdApproachSpeed  = 3f;

    [Header("Pair Throw")]
    [SerializeField] SockPair sockPairPrefab;
    [SerializeField] float    pairThrowForce = 8f;

    Player   _player;
    Sock     _current;
    SockPair _currentPair;

    bool _isManipulating;
    Sock _manipulatingSock;
    Sock _pairCandidate;

    bool     _isHoldingPair;
    SockPair _heldPair;

    void Awake() => _player = ReInput.players.GetPlayer(0);

    void Update()
    {
        HandleManipulation();

        if (_isHoldingPair)
        {
            HandlePairHold();
            return;
        }

        if (_isManipulating)
        {
            TryPair();
            return;
        }

        TryPickUpPair();
        UpdateSelection();
        UpdateSegmentHover();
    }

    void HandleManipulation()
    {
        if (_player.GetButtonDown(actionInteract) && _current != null && !_isManipulating && !_isHoldingPair)
        {
            if (_current.StartManipulating(Camera.main))
            {
                _isManipulating   = true;
                _manipulatingSock = _current;
            }
        }

        if (_isManipulating)
        {
            if (_player.GetButton(actionInteract))
            {
                _manipulatingSock?.UpdateManipulation(Camera.main, holdCameraDistance, holdApproachSpeed);
                UpdatePairCandidate();
            }

            if (_player.GetButtonUp(actionInteract))
                EndManipulation();
        }
    }

    void UpdatePairCandidate()
    {
        Rigidbody bone = _manipulatingSock?.GetManipulatedBone();
        if (bone == null) { ClearPairCandidate(); return; }

        Ray          ray  = Camera.main.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Sock nearest = null;
        foreach (RaycastHit hit in hits)
        {
            Sock hitSock = hit.collider.GetComponentInParent<Sock>();
            if (hitSock == null || hitSock == _manipulatingSock) continue;
            if (Vector3.Distance(bone.position, hit.point) <= pairProximityDistance)
                nearest = hitSock;
            break;
        }

        if (nearest == _pairCandidate) return;
        _pairCandidate?.HidePairHighlight();
        _pairCandidate = nearest;
        _pairCandidate?.ShowPairHighlight(pairHighlightColor);
    }

    void ClearPairCandidate()
    {
        _pairCandidate?.HidePairHighlight();
        _pairCandidate = null;
    }

    void EndManipulation()
    {
        _manipulatingSock?.StopManipulating();
        ClearPairCandidate();
        _isManipulating   = false;
        _manipulatingSock = null;
    }

    void TryPair()
    {
        if (sockPairPrefab == null || !_player.GetButtonDown(actionPair) || _pairCandidate == null) return;

        Sock      sockA    = _manipulatingSock;
        Sock      sockB    = _pairCandidate;
        Rigidbody bone     = sockA.GetManipulatedBone();
        Vector3   spawnPos = bone != null
            ? (bone.position + sockB.transform.position) * 0.5f
            : sockB.transform.position;

        ClearPairCandidate();
        EndManipulation();

        SockPair pair = Instantiate(sockPairPrefab, spawnPos, Quaternion.identity);
        pair.Setup(sockA, sockB);
    }

    void TryPickUpPair()
    {
        if (!_player.GetButtonDown(actionInteract) || _currentPair == null) return;

        _currentPair.Unhighlight();
        _currentPair.StartHold(Camera.main);
        _heldPair      = _currentPair;
        _currentPair   = null;
        _isHoldingPair = true;
    }

    void HandlePairHold()
    {
        if (_player.GetButton(actionInteract))
            _heldPair.UpdateHold(Camera.main, holdCameraDistance, holdApproachSpeed);

        if (_player.GetButtonDown(actionUnpair))
        {
            _heldPair.Decompose();
            _heldPair      = null;
            _isHoldingPair = false;
            return;
        }

        if (_player.GetButtonUp(actionInteract))
        {
            _heldPair.StopHold(Camera.main.transform.forward * pairThrowForce);
            _heldPair      = null;
            _isHoldingPair = false;
        }
    }

    void UpdateSegmentHover()
    {
        if (_current == null) return;
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        _current.UpdateHoveredSegment(ray);
    }

    void UpdateSelection()
    {
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));

        Sock     hitSock = null;
        SockPair hitPair = null;

        if (Physics.Raycast(ray, out RaycastHit info, maxDistance, sockLayerMask))
        {
            hitSock = info.collider.GetComponentInParent<Sock>();
            if (hitSock == null)
                hitPair = info.collider.GetComponentInParent<SockPair>();
        }

        if (hitSock != _current)
        {
            _current?.Unhighlight();
            _current?.ClearHoveredSegment();
            _current = hitSock;
            _current?.Highlight();
        }

        if (_currentPair != null && !_currentPair) _currentPair = null;

        if (hitPair != _currentPair)
        {
            _currentPair?.Unhighlight();
            _currentPair = hitPair;
            _currentPair?.Highlight();
        }
    }
}
