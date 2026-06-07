using UnityEngine;

/// <summary>
/// Przesuwa kości SkinnedMeshRenderer tak, żeby siatka
/// zawsze pasowała do fizycznych segmentów.
///
/// Zapamiętuje offset rotacji między segmentem a kością przy starcie,
/// dzięki czemu kość "zgina się" o tyle samo co segment — bez skręcania siatki.
///
/// LateUpdate — po tym jak fizyka zaktualizowała pozycje.
/// </summary>
public class SockBoneFollower : MonoBehaviour
{
    [Header("Kości siatki (z Armature)")]
    [SerializeField] Transform boneCholewka;
    [SerializeField] Transform boneSrodstopie;
    [SerializeField] Transform boneNosek;

    [Header("Segmenty fizyczne")]
    [SerializeField] Transform segCholewka;
    [SerializeField] Transform segSrodstopie;
    [SerializeField] Transform segNosek;

    // Offset rotacji: różnica między segmentem a kością w pozycji spoczynkowej
    Quaternion _offCholewka;
    Quaternion _offSrodstopie;
    Quaternion _offNosek;

    void Start()
    {
        _offCholewka   = Quaternion.Inverse(segCholewka.rotation)   * boneCholewka.rotation;
        _offSrodstopie = Quaternion.Inverse(segSrodstopie.rotation) * boneSrodstopie.rotation;
        _offNosek      = Quaternion.Inverse(segNosek.rotation)      * boneNosek.rotation;
    }

    void LateUpdate()
    {
        boneCholewka.SetPositionAndRotation(
            segCholewka.position,
            segCholewka.rotation * _offCholewka);

        boneSrodstopie.SetPositionAndRotation(
            segSrodstopie.position,
            segSrodstopie.rotation * _offSrodstopie);

        boneNosek.SetPositionAndRotation(
            segNosek.position,
            segNosek.rotation * _offNosek);
    }
}
