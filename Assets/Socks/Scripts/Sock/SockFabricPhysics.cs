using UnityEngine;

/// <summary>
/// Tłumi nierealistyczne obroty segmentów skarpety.
/// Dodaj na każdy segment (Bone1, Bone2, Bone3).
///
/// Bez tego skarpeta kręci się jak śmigło wokół własnej osi.
/// Ten skrypt delikatnie prostuje każdy segment do pozycji "leżącej płasko".
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SockFabricPhysics : MonoBehaviour
{
    [Tooltip("Siła przywracająca płaskie ułożenie. Zacznij od 0.3, zwiększaj jeśli za mało.")]
    [SerializeField] float flatteningTorque = 0.3f;

    [Tooltip("Tolerancja — poniżej tego kąta skrypt nie działa (oszczędność CPU).")]
    [SerializeField] float tiltThreshold = 0.05f;

    [Tooltip("Tłumienie obrotu wokół osi pionowej (Y). 1.0 = brak tłumienia, 0.8 = spore tłumienie.")]
    [SerializeField] float angularDampY = 0.85f;

    Rigidbody rb;

    void Awake() => rb = GetComponent<Rigidbody>();

    void FixedUpdate()
    {
        if (rb.IsSleeping()) return;

        // Oś Y segmentu powinna wskazywać w górę — skarpeta leży płasko
        Vector3 up   = transform.up;
        float   tilt = 1f - Mathf.Abs(Vector3.Dot(up, Vector3.up));

        if (tilt > tiltThreshold)
        {
            Vector3 correction = Vector3.Cross(up, Vector3.up);
            rb.AddTorque(correction * flatteningTorque, ForceMode.Acceleration);
        }

        // Tłumienie obrotu wokół pionowej osi — zapobiega "kręceniu się" segmentu
        Vector3 av = rb.angularVelocity;
        av.y *= angularDampY;
        rb.angularVelocity = av;
    }
}
