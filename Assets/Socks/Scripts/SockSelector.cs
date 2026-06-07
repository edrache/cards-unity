using Rewired;
using UnityEngine;

public class SockSelector : MonoBehaviour
{
    [SerializeField] float maxDistance = 10f;
    [SerializeField] LayerMask sockLayerMask = ~0;
    [SerializeField] string pickUpActionName = "Interact";
    [SerializeField] Transform holdPoint;
    [SerializeField] float throwForce = 8f;

    [Header("Sloty dla segmentów (dzieci HoldPoint)")]
    [SerializeField] Transform slotCholewka;
    [SerializeField] Transform slotSrodstopie;
    [SerializeField] Transform slotNosek;

    Player _player;
    Sock _current;
    Sock _held;

    void Awake() => _player = ReInput.players.GetPlayer(0);

    void Update()
    {
        if (_player.GetButtonDown(pickUpActionName))
        {
            if (_held != null)
                ThrowHeld();
            else if (_current != null)
                PickUpCurrent();

            return;
        }

        if (_held == null)
            UpdateSelection();
    }

    void UpdateSelection()
    {
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        Sock hit = Physics.Raycast(ray, out RaycastHit info, maxDistance, sockLayerMask)
            ? info.collider.GetComponentInParent<Sock>()
            : null;

        if (hit == _current) return;

        _current?.Unhighlight();
        _current = hit;
        _current?.Highlight();
    }

    void PickUpCurrent()
    {
        _current.Unhighlight();
        _current.PickUp(slotCholewka, slotSrodstopie, slotNosek);
        _held = _current;
        _current = null;
    }

    void ThrowHeld()
    {
        _held.Throw(Camera.main.transform.forward * throwForce);
        _held = null;
    }
}
