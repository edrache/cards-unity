using UnityEngine;

/// <summary>
/// Przesuwa kości SkinnedMeshRenderer tak, żeby siatka
/// zawsze pasowała do fizycznych segmentów.
///
/// Listy bones i segments muszą mieć tę samą długość i kolejność.
/// LateUpdate — po tym jak fizyka zaktualizowała pozycje.
/// </summary>
public class SockBoneFollower : MonoBehaviour
{
    [Header("Kości siatki (z Armature)")]
    [SerializeField] Transform[] bones;

    [Header("Segmenty fizyczne")]
    [SerializeField] Transform[] segments;

    Quaternion[] _offsets;

    void Start()
    {
        int count = Mathf.Min(bones.Length, segments.Length);
        _offsets = new Quaternion[count];
        for (int i = 0; i < count; i++)
            _offsets[i] = Quaternion.Inverse(segments[i].rotation) * bones[i].rotation;
    }

    void LateUpdate()
    {
        int count = _offsets.Length;
        for (int i = 0; i < count; i++)
            bones[i].SetPositionAndRotation(
                segments[i].position,
                segments[i].rotation * _offsets[i]);
    }
}
