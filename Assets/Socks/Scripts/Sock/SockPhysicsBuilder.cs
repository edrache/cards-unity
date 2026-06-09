using UnityEngine;

[DefaultExecutionOrder(-10)]
public class SockPhysicsBuilder : MonoBehaviour
{
    [Header("Segmenty fizyczne (każdy z Rigidbody + BoxCollider)")]
    [SerializeField] Rigidbody[] segments;

    [Header("Rigidbody")]
    [SerializeField] float mass           = 0.03f;
    [SerializeField] float linearDamping  = 5.0f;
    [SerializeField] float angularDamping = 12.0f;
    [SerializeField] float sleepThreshold = 0.15f;

    [Header("HingeJoint — limity zgięcia")]
    [SerializeField] float jointMin = -40f;
    [SerializeField] float jointMax =  15f;
    [SerializeField] float spring   =   0.5f;
    [SerializeField] float damper   =   5.0f;

    void Awake()
    {
        if (segments == null || segments.Length == 0) return;

        foreach (Rigidbody rb in segments)
            SetupRigidbody(rb);

        for (int i = 1; i < segments.Length; i++)
        {
            Vector3 anchor = segments[i].transform
                .InverseTransformPoint(segments[i - 1].transform.position);
            AddHinge(segments[i], segments[i - 1], anchor);
        }

        foreach (Rigidbody rb in segments)
            rb.centerOfMass = new Vector3(0f, -0.004f, 0f);
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
