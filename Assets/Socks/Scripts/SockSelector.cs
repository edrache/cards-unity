using UnityEngine;

public class SockSelector : MonoBehaviour
{
    [SerializeField] float maxDistance = 10f;
    [SerializeField] LayerMask sockLayerMask = ~0;

    Sock _current;

    void Update()
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
}
