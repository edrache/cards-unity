using UnityEngine;

/// <summary>
/// Buduje fizyczny rig skarpety: konfiguruje Rigidbody na 3 segmentach
/// i łączy je HingeJointami w runtime.
///
/// Setup w Inspektorze:
///   segCholewka  — korzeń łańcucha (brak HingeJoint)
///   segSrodstopie — połączony z Cholewką
///   segNosek      — połączony ze Śródstopiem
///
/// Każdy segment musi mieć BoxCollider i Rigidbody dodane ręcznie.
/// </summary>
[DefaultExecutionOrder(-10)]
public class SockPhysicsBuilder : MonoBehaviour
{
    [Header("Segmenty fizyczne (każdy z Rigidbody + BoxCollider)")]
    [SerializeField] Rigidbody segCholewka;
    [SerializeField] Rigidbody segSrodstopie;
    [SerializeField] Rigidbody segNosek;

    [Header("Rigidbody")]
    [SerializeField] float mass           = 0.03f;
    [SerializeField] float linearDamping  = 5.0f;   // wyższe = szybciej zasypia
    [SerializeField] float angularDamping = 12.0f;  // tłumi obroty przy lądowaniu
    [SerializeField] float sleepThreshold = 0.15f;  // próg usypiania (domyślne Unity: 0.005)

    [Header("HingeJoint — limity zgięcia")]
    [SerializeField] float jointMin   = -40f;
    [SerializeField] float jointMax   =  15f;
    [SerializeField] float spring     =   0.5f;
    [SerializeField] float damper     =   5.0f;

    void Awake()
    {
        SetupRigidbody(segCholewka);
        SetupRigidbody(segSrodstopie);
        SetupRigidbody(segNosek);

        // Połącz śródstopie z cholewką w punkcie styku
        Vector3 anchorSrod = segSrodstopie.transform
            .InverseTransformPoint(segCholewka.transform.position);
        AddHinge(segSrodstopie, segCholewka, anchorSrod);

        // Połącz nosek ze śródstopiem w punkcie styku
        Vector3 anchorNosek = segNosek.transform
            .InverseTransformPoint(segSrodstopie.transform.position);
        AddHinge(segNosek, segSrodstopie, anchorNosek);

        // Środek masy nisko → skarpeta naturalnie się kładzie
        segCholewka.centerOfMass   = new Vector3(0f, -0.004f, 0f);
        segSrodstopie.centerOfMass = new Vector3(0f, -0.004f, 0f);
        segNosek.centerOfMass      = new Vector3(0f, -0.004f, 0f);
    }

    void SetupRigidbody(Rigidbody rb)
    {
        rb.mass           = mass;
        rb.linearDamping  = linearDamping;
        rb.angularDamping = angularDamping;
        rb.sleepThreshold = sleepThreshold;
        rb.interpolation  = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
    }

    void AddHinge(Rigidbody child, Rigidbody parent, Vector3 localAnchor)
    {
        var hinge           = child.gameObject.AddComponent<HingeJoint>();
        hinge.connectedBody = parent;
        hinge.anchor        = localAnchor;
        hinge.axis          = Vector3.right;

        hinge.useLimits = true;
        hinge.limits    = new JointLimits
        {
            min               = jointMin,
            max               = jointMax,
            bounciness        = 0f,
            bounceMinVelocity = 0.5f
        };

        hinge.useSpring = true;
        hinge.spring    = new JointSpring
        {
            spring         = spring,
            damper         = damper,
            targetPosition = 0f
        };
    }
}
