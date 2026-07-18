using CardsUnity.Data;
using UnityEngine;

namespace CardsUnity.Runtime
{
    public sealed class CharacterPhysicsRigBuilder : MonoBehaviour
    {
        [SerializeField] private CharacterPhysicsRig rig;
        [SerializeField] private bool ignoreInternalCollisions = true;
        [SerializeField] private bool enableProjection = true;
        [SerializeField] private bool disablePreprocessing = true;
        [SerializeField] private float defaultBodyMass = 1f;
        [SerializeField] private float hipsMass = 8f;
        [SerializeField] private float torsoMass = 4f;
        [SerializeField] private float headMass = 2f;
        [SerializeField] private float upperLimbMass = 1.5f;
        [SerializeField] private float lowerLimbMass = 1f;
        [SerializeField] private float footMass = 0.75f;
        [SerializeField] private float jointSpring = 500f;
        [SerializeField] private float jointDamper = 50f;
        [SerializeField] private float jointMaxForce = 500f;
        [SerializeField] private float jointMassScale = 1f;
        [SerializeField] private float jointConnectedMassScale = 1f;
        [SerializeField] private float projectionDistance = 0.05f;
        [SerializeField] private float projectionAngle = 10f;
        [SerializeField] private float minimumCapsuleHeight = 0.12f;
        [SerializeField] private float capsuleRadiusRatio = 0.22f;
        [SerializeField] private float minimumCapsuleRadius = 0.03f;
        [SerializeField] private float footHeight = 0.06f;
        [SerializeField] private float footWidth = 0.12f;
        [SerializeField] private float footLengthPadding = 0.02f;
        [SerializeField] private float footVerticalOffset = -0.02f;

        [ContextMenu("Build Or Refresh Physics Rig")]
        public void BuildOrRefreshRig()
        {
            if (rig == null)
            {
                rig = GetComponent<CharacterPhysicsRig>();
            }

            if (rig == null)
            {
                return;
            }

            rig.RebuildSegmentsFromAnimator();

            foreach (var segment in rig.Segments)
            {
                if (segment == null || segment.transform == null)
                {
                    continue;
                }

                segment.body = EnsureBody(segment);
                segment.primaryCollider = EnsureCollider(segment);

                if (segment.bone == CharacterRigBone.Hips)
                {
                    segment.joint = null;
                    continue;
                }

                segment.joint = EnsureJoint(segment);
            }

            if (ignoreInternalCollisions)
            {
                IgnoreInternalCollisions();
            }

            rig.RebuildSegmentsFromAnimator();
            rig.LogValidationReport();
        }

        [ContextMenu("Clear Generated Physics Rig")]
        public void ClearGeneratedRig()
        {
            if (rig == null)
            {
                rig = GetComponent<CharacterPhysicsRig>();
            }

            if (rig == null)
            {
                return;
            }

            var root = rig.PhysicalRoot != null ? rig.PhysicalRoot : rig.transform;
            if (root == null)
            {
                return;
            }

            var joints = root.GetComponentsInChildren<Joint>(true);
            foreach (var joint in joints)
            {
                DestroyComponentImmediate(joint);
            }

            var rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            foreach (var body in rigidbodies)
            {
                DestroyComponentImmediate(body);
            }

            var colliders = root.GetComponentsInChildren<Collider>(true);
            foreach (var collider in colliders)
            {
                DestroyComponentImmediate(collider);
            }

            rig.RebuildSegmentsFromAnimator();
            rig.LogValidationReport();
        }

        private Rigidbody EnsureBody(CharacterBodySegment segment)
        {
            var body = segment.body != null ? segment.body : segment.transform.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = segment.transform.gameObject.AddComponent<Rigidbody>();
            }

            body.mass = ResolveMass(segment.bone);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            return body;
        }

        private Collider EnsureCollider(CharacterBodySegment segment)
        {
            var existingCollider = segment.primaryCollider != null
                ? segment.primaryCollider
                : segment.transform.GetComponent<Collider>();

            if (existingCollider != null)
            {
                return existingCollider;
            }

            return segment.bone switch
            {
                CharacterRigBone.Hips => CreateBoxCollider(segment.transform, 0.24f, 0.18f, 0.18f, Vector3.zero),
                CharacterRigBone.Spine => CreateBoneCapsuleCollider(segment.transform, 0.12f),
                CharacterRigBone.Spine1 => CreateBoneCapsuleCollider(segment.transform, 0.12f),
                CharacterRigBone.Spine2 => CreateBoneCapsuleCollider(segment.transform, 0.12f),
                CharacterRigBone.Neck => CreateBoneCapsuleCollider(segment.transform, 0.06f),
                CharacterRigBone.Head => CreateSphereCollider(segment.transform, 0.11f, Vector3.zero),
                CharacterRigBone.LeftShoulder => CreateBoneCapsuleCollider(segment.transform, 0.06f),
                CharacterRigBone.RightShoulder => CreateBoneCapsuleCollider(segment.transform, 0.06f),
                CharacterRigBone.LeftArm => CreateBoneCapsuleCollider(segment.transform, 0.07f),
                CharacterRigBone.RightArm => CreateBoneCapsuleCollider(segment.transform, 0.07f),
                CharacterRigBone.LeftForeArm => CreateBoneCapsuleCollider(segment.transform, 0.06f),
                CharacterRigBone.RightForeArm => CreateBoneCapsuleCollider(segment.transform, 0.06f),
                CharacterRigBone.LeftUpLeg => CreateBoneCapsuleCollider(segment.transform, 0.09f),
                CharacterRigBone.RightUpLeg => CreateBoneCapsuleCollider(segment.transform, 0.09f),
                CharacterRigBone.LeftLeg => CreateBoneCapsuleCollider(segment.transform, 0.08f),
                CharacterRigBone.RightLeg => CreateBoneCapsuleCollider(segment.transform, 0.08f),
                CharacterRigBone.LeftFoot => CreateFootCollider(segment.transform),
                CharacterRigBone.RightFoot => CreateFootCollider(segment.transform),
                _ => CreateSphereCollider(segment.transform, 0.1f, Vector3.zero)
            };
        }

        private ConfigurableJoint EnsureJoint(CharacterBodySegment segment)
        {
            var joint = segment.joint != null ? segment.joint : segment.transform.GetComponent<ConfigurableJoint>();
            if (joint == null)
            {
                joint = segment.transform.gameObject.AddComponent<ConfigurableJoint>();
            }

            ConfigureJointConnection(segment, joint);
            joint.enablePreprocessing = !disablePreprocessing;
            joint.configuredInWorldSpace = false;
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            joint.massScale = jointMassScale;
            joint.connectedMassScale = jointConnectedMassScale;

            var limits = ResolveAngularLimits(segment.bone);
            joint.lowAngularXLimit = new SoftJointLimit { limit = -limits.x };
            joint.highAngularXLimit = new SoftJointLimit { limit = limits.x };
            joint.angularYLimit = new SoftJointLimit { limit = limits.y };
            joint.angularZLimit = new SoftJointLimit { limit = limits.z };
            joint.slerpDrive = new JointDrive
            {
                positionSpring = jointSpring,
                positionDamper = jointDamper,
                maximumForce = jointMaxForce
            };
            joint.projectionMode = enableProjection ? JointProjectionMode.PositionAndRotation : JointProjectionMode.None;
            joint.projectionDistance = projectionDistance;
            joint.projectionAngle = projectionAngle;

            return joint;
        }

        private void ConfigureJointConnection(CharacterBodySegment segment, ConfigurableJoint joint)
        {
            var parentTransform = segment.transform.parent;
            if (parentTransform == null)
            {
                return;
            }

            joint.connectedBody = parentTransform.GetComponent<Rigidbody>();
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero;

            if (joint.connectedBody != null)
            {
                joint.connectedAnchor = joint.connectedBody.transform.InverseTransformPoint(segment.transform.position);
            }
            else
            {
                joint.connectedAnchor = parentTransform.InverseTransformPoint(segment.transform.position);
            }

            var localChildOffset = ResolvePrimaryChildOffset(segment.transform);
            var axis = localChildOffset.sqrMagnitude > Mathf.Epsilon
                ? localChildOffset.normalized
                : Vector3.forward;

            var reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.95f
                ? Vector3.right
                : Vector3.up;

            var secondaryAxis = Vector3.Cross(axis, reference).normalized;
            if (secondaryAxis.sqrMagnitude < Mathf.Epsilon)
            {
                secondaryAxis = Vector3.right;
            }

            joint.axis = axis;
            joint.secondaryAxis = secondaryAxis;
        }

        private float ResolveMass(CharacterRigBone bone)
        {
            return bone switch
            {
                CharacterRigBone.Hips => hipsMass,
                CharacterRigBone.Spine => torsoMass,
                CharacterRigBone.Spine1 => torsoMass,
                CharacterRigBone.Spine2 => torsoMass,
                CharacterRigBone.Head => headMass,
                CharacterRigBone.Neck => 1f,
                CharacterRigBone.LeftShoulder => upperLimbMass,
                CharacterRigBone.RightShoulder => upperLimbMass,
                CharacterRigBone.LeftArm => upperLimbMass,
                CharacterRigBone.RightArm => upperLimbMass,
                CharacterRigBone.LeftForeArm => lowerLimbMass,
                CharacterRigBone.RightForeArm => lowerLimbMass,
                CharacterRigBone.LeftUpLeg => upperLimbMass * 1.5f,
                CharacterRigBone.RightUpLeg => upperLimbMass * 1.5f,
                CharacterRigBone.LeftLeg => lowerLimbMass * 1.5f,
                CharacterRigBone.RightLeg => lowerLimbMass * 1.5f,
                CharacterRigBone.LeftFoot => footMass,
                CharacterRigBone.RightFoot => footMass,
                _ => defaultBodyMass
            };
        }

        private static CapsuleCollider CreateCapsuleCollider(Transform transform, float radius, float height, Vector3 center)
        {
            var collider = transform.gameObject.AddComponent<CapsuleCollider>();
            collider.direction = 2;
            collider.radius = radius;
            collider.height = height;
            collider.center = center;
            return collider;
        }

        private CapsuleCollider CreateBoneCapsuleCollider(Transform transform, float fallbackRadius)
        {
            var collider = transform.gameObject.AddComponent<CapsuleCollider>();
            var localChildOffset = ResolvePrimaryChildOffset(transform);
            var length = localChildOffset.magnitude;

            if (length < Mathf.Epsilon)
            {
                collider.direction = 2;
                collider.radius = Mathf.Max(minimumCapsuleRadius, fallbackRadius);
                collider.height = Mathf.Max(minimumCapsuleHeight, fallbackRadius * 2f);
                collider.center = Vector3.zero;
                return collider;
            }

            collider.direction = ResolveDominantAxis(localChildOffset);
            collider.height = Mathf.Max(minimumCapsuleHeight, length);
            collider.radius = Mathf.Max(minimumCapsuleRadius, Mathf.Min(fallbackRadius, length * capsuleRadiusRatio));
            collider.center = localChildOffset * 0.5f;
            return collider;
        }

        private static BoxCollider CreateBoxCollider(Transform transform, float x, float y, float z, Vector3 center)
        {
            var collider = transform.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(x, y, z);
            collider.center = center;
            return collider;
        }

        private static SphereCollider CreateSphereCollider(Transform transform, float radius, Vector3 center)
        {
            var collider = transform.gameObject.AddComponent<SphereCollider>();
            collider.radius = radius;
            collider.center = center;
            return collider;
        }

        private BoxCollider CreateFootCollider(Transform transform)
        {
            var collider = transform.gameObject.AddComponent<BoxCollider>();
            var localChildOffset = ResolvePrimaryChildOffset(transform);
            var length = Mathf.Max(footWidth, localChildOffset.magnitude + footLengthPadding);
            var forwardSign = localChildOffset.z < 0f ? -1f : 1f;

            collider.size = new Vector3(footWidth, footHeight, length);
            collider.center = new Vector3(0f, footVerticalOffset, forwardSign * length * 0.25f);
            return collider;
        }

        private static Vector3 ResolveAngularLimits(CharacterRigBone bone)
        {
            return bone switch
            {
                CharacterRigBone.Spine => new Vector3(20f, 15f, 15f),
                CharacterRigBone.Spine1 => new Vector3(20f, 15f, 15f),
                CharacterRigBone.Spine2 => new Vector3(25f, 20f, 20f),
                CharacterRigBone.Neck => new Vector3(25f, 20f, 20f),
                CharacterRigBone.Head => new Vector3(30f, 25f, 25f),
                CharacterRigBone.LeftShoulder => new Vector3(35f, 30f, 30f),
                CharacterRigBone.RightShoulder => new Vector3(35f, 30f, 30f),
                CharacterRigBone.LeftArm => new Vector3(45f, 35f, 35f),
                CharacterRigBone.RightArm => new Vector3(45f, 35f, 35f),
                CharacterRigBone.LeftForeArm => new Vector3(55f, 20f, 20f),
                CharacterRigBone.RightForeArm => new Vector3(55f, 20f, 20f),
                CharacterRigBone.LeftUpLeg => new Vector3(40f, 25f, 25f),
                CharacterRigBone.RightUpLeg => new Vector3(40f, 25f, 25f),
                CharacterRigBone.LeftLeg => new Vector3(50f, 10f, 10f),
                CharacterRigBone.RightLeg => new Vector3(50f, 10f, 10f),
                CharacterRigBone.LeftFoot => new Vector3(25f, 15f, 15f),
                CharacterRigBone.RightFoot => new Vector3(25f, 15f, 15f),
                _ => new Vector3(25f, 25f, 25f)
            };
        }

        private static int ResolveDominantAxis(Vector3 localDirection)
        {
            var absolute = new Vector3(
                Mathf.Abs(localDirection.x),
                Mathf.Abs(localDirection.y),
                Mathf.Abs(localDirection.z));

            if (absolute.x >= absolute.y && absolute.x >= absolute.z)
            {
                return 0;
            }

            if (absolute.y >= absolute.x && absolute.y >= absolute.z)
            {
                return 1;
            }

            return 2;
        }

        private static Vector3 ResolvePrimaryChildOffset(Transform transform)
        {
            if (transform.childCount == 0)
            {
                return Vector3.zero;
            }

            var furthestOffset = Vector3.zero;
            var furthestDistance = 0f;

            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                var offset = child.localPosition;
                var distance = offset.sqrMagnitude;
                if (distance > furthestDistance)
                {
                    furthestOffset = offset;
                    furthestDistance = distance;
                }
            }

            return furthestOffset;
        }

        private void IgnoreInternalCollisions()
        {
            for (var i = 0; i < rig.Segments.Count; i++)
            {
                var first = rig.Segments[i]?.primaryCollider;
                if (first == null)
                {
                    continue;
                }

                for (var j = i + 1; j < rig.Segments.Count; j++)
                {
                    var second = rig.Segments[j]?.primaryCollider;
                    if (second == null)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(first, second, true);
                }
            }
        }

        private static void DestroyComponentImmediate(Object component)
        {
            if (component == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(component);
                return;
            }

            Object.DestroyImmediate(component);
        }
    }
}
