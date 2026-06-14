using UnityEngine;
using UnityEngine.Rendering.Universal;

public class SockBlobShadow : MonoBehaviour
{
    [SerializeField] DecalProjector decalProjector;

    [Tooltip("Segmenty do śledzenia. Puste = cały segment[0] z Sock.")]
    [SerializeField] Transform[] trackedSegments;

    [SerializeField] float yOffset = 3f;
    [SerializeField] Vector3 projectorRotation = new Vector3(90f, 0f, 0f);

    void Awake()
    {
        if (decalProjector == null)
            decalProjector = GetComponentInChildren<DecalProjector>();
        SetVisible(false);
    }

    void LateUpdate()
    {
        if (trackedSegments == null || trackedSegments.Length == 0) return;

        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (Transform seg in trackedSegments)
        {
            if (seg == null) continue;
            sum += seg.position;
            count++;
        }
        if (count == 0) return;

        Vector3 center = sum / count;
        transform.SetPositionAndRotation(
            new Vector3(center.x, center.y + yOffset, center.z),
            Quaternion.Euler(projectorRotation)
        );
    }

    public void SetVisible(bool visible)
    {
        if (decalProjector != null)
            decalProjector.enabled = visible;
    }
}
