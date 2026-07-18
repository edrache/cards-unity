using System;
using System.Collections.Generic;
using System.Text;
using CardsUnity.Config;
using CardsUnity.Data;
using CardsUnity.Runtime;
using UnityEngine;

namespace CardsUnity.Controllers
{
    public sealed class ActiveRagdollController : MonoBehaviour
    {
        private const float DefaultJointSpring = 500f;
        private const float DefaultJointDamper = 50f;
        private const float DefaultMaxJointForce = 500f;
        private const float DefaultBodyTorqueSpring = 40f;
        private const float DefaultBodyTorqueDamper = 4f;
        private const float DefaultMaxBodyTorque = 80f;
        private const float DefaultRootUprightSpring = 120f;
        private const float DefaultRootUprightDamper = 12f;
        private const float DefaultMaxRootUprightTorque = 160f;
        private const float DefaultRootPositionSpring = 120f;
        private const float DefaultRootPositionDamper = 18f;
        private const float DefaultMaxRootPositionForce = 250f;
        private const float DefaultRootHeightSpring = 220f;
        private const float DefaultRootHeightDamper = 24f;
        private const float DefaultMaxRootHeightForce = 350f;
        private const float DefaultFootSupportSpring = 100f;
        private const float DefaultFootSupportDamper = 12f;
        private const float DefaultMaxFootSupportForce = 160f;
        private const float DefaultFootProbeHeight = 0.5f;
        private const float DefaultFootProbeDistance = 2f;
        private const float DefaultGroundedFootOffset = 0.03f;

        [Serializable]
        private sealed class JointPoseBinding
        {
            public Transform transform;
            public Transform sourceTransform;
            public ConfigurableJoint joint;
            public Rigidbody body;
            public Rigidbody parentBody;
            public Quaternion initialLocalRotation;
        }

        [SerializeField] private ActiveRagdollTuning tuning;
        [SerializeField] private CharacterPhysicsRig physicsRig;
        private readonly List<JointPoseBinding> jointBindings = new();
        private Quaternion rootReferenceRotation = Quaternion.identity;
        private Transform poseSourceRoot;
        private Animator poseSourceAnimator;
        private Transform rootSourceTransform;
        private Rigidbody leftFootBody;
        private Rigidbody rightFootBody;
        private Transform leftFootSourceTransform;
        private Transform rightFootSourceTransform;

        public bool IsActive { get; private set; } = true;

        private void Awake()
        {
            IsActive = tuning == null || tuning.startActive;
        }

        private void OnEnable()
        {
            EnsurePoseSource();
            RebuildJointBindings();

            if (ShouldCapturePoseOnEnable())
            {
                CaptureReferencePose();
            }

            if (ShouldAlignPhysicalRigOnEnable())
            {
                SnapPhysicalRigToPoseSource();
            }

            SyncAnimatorState();
            RefreshConfiguredJoints();
        }

        private void FixedUpdate()
        {
            if (!IsActive)
            {
                return;
            }

            ApplyReferencePose();
            ApplyFootSupportForce(leftFootBody, leftFootSourceTransform);
            ApplyFootSupportForce(rightFootBody, rightFootSourceTransform);
            ApplyRootPositionForce();
            ApplyRootHeightSupport();
            ApplyRootUprightTorque();
        }

        public void SetActiveMode(bool isActive)
        {
            IsActive = isActive;
            EnsurePoseSource();

            if (IsActive && ShouldAlignPhysicalRigOnEnable())
            {
                SnapPhysicalRigToPoseSource();
            }

            SyncAnimatorState();
            RefreshConfiguredJoints();
        }

        public JointDrive CreateAngularDrive()
        {
            var spring = tuning != null ? tuning.jointSpring : DefaultJointSpring;
            var damper = tuning != null ? tuning.jointDamper : DefaultJointDamper;
            var maxForce = tuning != null ? tuning.maxJointForce : DefaultMaxJointForce;

            return new JointDrive
            {
                positionSpring = spring,
                positionDamper = damper,
                maximumForce = IsActive ? maxForce : 0f
            };
        }

        [ContextMenu("Refresh Configured Joints")]
        public void RefreshConfiguredJoints()
        {
            if (physicsRig == null)
            {
                return;
            }

            if (jointBindings.Count == 0)
            {
                RebuildJointBindings();
            }

            var drive = CreateAngularDrive();
            foreach (var binding in jointBindings)
            {
                var joint = binding?.joint;
                if (joint == null)
                {
                    continue;
                }

                joint.rotationDriveMode = RotationDriveMode.Slerp;
                joint.slerpDrive = drive;
                joint.angularXDrive = drive;
                joint.angularYZDrive = drive;
            }
        }

        [ContextMenu("Capture Reference Pose")]
        public void CaptureReferencePose()
        {
            if (jointBindings.Count == 0)
            {
                RebuildJointBindings();
            }

            if (physicsRig?.RootBody != null)
            {
                rootReferenceRotation = poseSourceRoot != null
                    ? poseSourceRoot.rotation
                    : physicsRig.RootBody.rotation;
            }

            if (physicsRig?.PhysicalRoot != null)
            {
                rootSourceTransform = FindSourceTransform(physicsRig.PhysicalRoot);
            }
        }

        [ContextMenu("Rebuild Joint Bindings")]
        public void RebuildJointBindings()
        {
            jointBindings.Clear();

            if (physicsRig == null)
            {
                return;
            }

            foreach (var segment in physicsRig.Segments)
            {
                if (segment?.joint == null || segment.transform == null)
                {
                    continue;
                }

                var sourceTransform = FindSourceTransform(segment.transform);

                jointBindings.Add(new JointPoseBinding
                {
                    transform = segment.transform,
                    sourceTransform = sourceTransform,
                    joint = segment.joint,
                    body = segment.body,
                    parentBody = segment.joint.connectedBody,
                    initialLocalRotation = segment.transform.localRotation
                });
            }

            leftFootBody = ResolveFootBody(CharacterRigBone.LeftFoot);
            rightFootBody = ResolveFootBody(CharacterRigBone.RightFoot);
            leftFootSourceTransform = FindSourceTransformForBone(CharacterRigBone.LeftFoot);
            rightFootSourceTransform = FindSourceTransformForBone(CharacterRigBone.RightFoot);
        }

        [ContextMenu("Log Pose Binding Report")]
        public void LogPoseBindingReport()
        {
            if (jointBindings.Count == 0)
            {
                RebuildJointBindings();
            }

            var report = new StringBuilder();
            report.AppendLine($"{name}: pose binding report");

            foreach (var segment in physicsRig.Segments)
            {
                if (segment?.transform == null)
                {
                    continue;
                }

                var sourceTransform = FindSourceTransform(segment.transform);
                var hasSource = sourceTransform != null;
                var worldOffset = hasSource
                    ? Vector3.Distance(segment.transform.position, sourceTransform.position)
                    : -1f;

                report.Append("- ")
                    .Append(segment.bone)
                    .Append(" | physical: ")
                    .Append(segment.transform.name)
                    .Append(" | source: ")
                    .Append(hasSource ? sourceTransform.name : "MISSING")
                    .Append(" | world offset: ")
                    .Append(hasSource ? worldOffset.ToString("F3") : "n/a")
                    .AppendLine();
            }

            Debug.Log(report.ToString(), this);
        }

        [ContextMenu("Snap Physical Rig To Pose Source")]
        public void SnapPhysicalRigToPoseSource()
        {
            if (physicsRig == null)
            {
                return;
            }

            if (jointBindings.Count == 0)
            {
                RebuildJointBindings();
            }

            foreach (var segment in physicsRig.Segments)
            {
                if (segment?.transform == null)
                {
                    continue;
                }

                var sourceTransform = FindSourceTransform(segment.transform);
                if (sourceTransform == null)
                {
                    continue;
                }

                segment.transform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);

                if (segment.body != null)
                {
                    segment.body.linearVelocity = Vector3.zero;
                    segment.body.angularVelocity = Vector3.zero;
                    segment.body.position = sourceTransform.position;
                    segment.body.rotation = sourceTransform.rotation;
                    segment.body.WakeUp();
                }
            }

            Physics.SyncTransforms();
        }

        private void ApplyReferencePose()
        {
            if (jointBindings.Count == 0)
            {
                return;
            }

            var poseBlend = tuning != null ? tuning.poseBlend : 1f;
            foreach (var binding in jointBindings)
            {
                if (binding?.joint == null || binding.transform == null)
                {
                    continue;
                }

                var desiredLocalRotation = binding.sourceTransform != null
                    ? binding.sourceTransform.localRotation
                    : binding.initialLocalRotation;

                var targetRotation = Quaternion.Slerp(
                    binding.transform.localRotation,
                    desiredLocalRotation,
                    poseBlend);

                binding.joint.SetTargetRotationLocal(targetRotation, binding.initialLocalRotation);
                ApplyBodyTorque(binding, targetRotation);
            }
        }

        private void ApplyBodyTorque(JointPoseBinding binding, Quaternion targetLocalRotation)
        {
            if (binding.body == null || binding.parentBody == null)
            {
                return;
            }

            var targetWorldRotation = binding.parentBody.rotation * targetLocalRotation;
            var deltaRotation = targetWorldRotation * Quaternion.Inverse(binding.body.rotation);
            deltaRotation.ToAngleAxis(out var angleDegrees, out var axis);

            if (float.IsNaN(axis.x) || float.IsNaN(axis.y) || float.IsNaN(axis.z))
            {
                return;
            }

            if (angleDegrees > 180f)
            {
                angleDegrees -= 360f;
            }

            if (Mathf.Abs(angleDegrees) < 0.01f)
            {
                return;
            }

            var spring = tuning != null ? tuning.bodyTorqueSpring : DefaultBodyTorqueSpring;
            var damper = tuning != null ? tuning.bodyTorqueDamper : DefaultBodyTorqueDamper;
            var maxTorque = tuning != null ? tuning.maxBodyTorque : DefaultMaxBodyTorque;

            var correctiveTorque = axis.normalized * (angleDegrees * Mathf.Deg2Rad * spring);
            var dampingTorque = binding.body.angularVelocity * damper;
            var finalTorque = Vector3.ClampMagnitude(correctiveTorque - dampingTorque, maxTorque);

            binding.body.AddTorque(finalTorque, ForceMode.Acceleration);
        }

        private void ApplyRootUprightTorque()
        {
            if (physicsRig?.RootBody == null)
            {
                return;
            }

            if (rootSourceTransform == null && physicsRig.PhysicalRoot != null)
            {
                rootSourceTransform = FindSourceTransform(physicsRig.PhysicalRoot);
            }

            var rootBody = physicsRig.RootBody;
            var currentUp = rootBody.transform.up;
            var targetRotation = rootSourceTransform != null
                ? rootSourceTransform.rotation
                : (poseSourceRoot != null ? poseSourceRoot.rotation : rootReferenceRotation);
            var targetUp = targetRotation * Vector3.up;
            var uprightDelta = Quaternion.FromToRotation(currentUp, targetUp);

            uprightDelta.ToAngleAxis(out var angleDegrees, out var axis);
            if (float.IsNaN(axis.x) || float.IsNaN(axis.y) || float.IsNaN(axis.z))
            {
                return;
            }

            if (angleDegrees > 180f)
            {
                angleDegrees -= 360f;
            }

            if (Mathf.Abs(angleDegrees) < 0.01f)
            {
                return;
            }

            var spring = tuning != null ? tuning.rootUprightSpring : DefaultRootUprightSpring;
            var damper = tuning != null ? tuning.rootUprightDamper : DefaultRootUprightDamper;
            var maxTorque = tuning != null ? tuning.maxRootUprightTorque : DefaultMaxRootUprightTorque;

            var correctiveTorque = axis.normalized * (angleDegrees * Mathf.Deg2Rad * spring);
            var dampingTorque = rootBody.angularVelocity * damper;
            var finalTorque = Vector3.ClampMagnitude(correctiveTorque - dampingTorque, maxTorque);

            rootBody.AddTorque(finalTorque, ForceMode.Acceleration);
        }

        private void ApplyRootPositionForce()
        {
            if (physicsRig?.RootBody == null)
            {
                return;
            }

            if (rootSourceTransform == null && physicsRig.PhysicalRoot != null)
            {
                rootSourceTransform = FindSourceTransform(physicsRig.PhysicalRoot);
            }

            if (rootSourceTransform == null)
            {
                return;
            }

            var rootBody = physicsRig.RootBody;
            var positionError = rootSourceTransform.position - rootBody.worldCenterOfMass;
            positionError = Vector3.ProjectOnPlane(positionError, Vector3.up);
            if (positionError.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var spring = tuning != null ? tuning.rootPositionSpring : DefaultRootPositionSpring;
            var damper = tuning != null ? tuning.rootPositionDamper : DefaultRootPositionDamper;
            var maxForce = tuning != null ? tuning.maxRootPositionForce : DefaultMaxRootPositionForce;

            var correctiveForce = positionError * spring;
            var dampingForce = rootBody.linearVelocity * damper;
            var finalForce = Vector3.ClampMagnitude(correctiveForce - dampingForce, maxForce);

            rootBody.AddForce(finalForce, ForceMode.Acceleration);
        }

        private void ApplyRootHeightSupport()
        {
            if (physicsRig?.RootBody == null)
            {
                return;
            }

            if (rootSourceTransform == null && physicsRig.PhysicalRoot != null)
            {
                rootSourceTransform = FindSourceTransform(physicsRig.PhysicalRoot);
            }

            if (rootSourceTransform == null)
            {
                return;
            }

            var rootBody = physicsRig.RootBody;
            var heightError = rootSourceTransform.position.y - rootBody.worldCenterOfMass.y;
            if (heightError <= 0.001f)
            {
                return;
            }

            var spring = tuning != null ? tuning.rootHeightSpring : DefaultRootHeightSpring;
            var damper = tuning != null ? tuning.rootHeightDamper : DefaultRootHeightDamper;
            var maxForce = tuning != null ? tuning.maxRootHeightForce : DefaultMaxRootHeightForce;

            var upwardVelocity = Vector3.Dot(rootBody.linearVelocity, Vector3.up);
            var correctiveForce = Vector3.up * (heightError * spring);
            var dampingForce = Vector3.up * (upwardVelocity * damper);
            var finalForce = Vector3.ClampMagnitude(correctiveForce - dampingForce, maxForce);

            rootBody.AddForce(finalForce, ForceMode.Acceleration);
        }

        private void ApplyFootSupportForce(Rigidbody footBody, Transform sourceFoot)
        {
            if (footBody == null || sourceFoot == null)
            {
                return;
            }

            var probeHeight = tuning != null ? tuning.footProbeHeight : DefaultFootProbeHeight;
            var probeDistance = tuning != null ? tuning.footProbeDistance : DefaultFootProbeDistance;
            var groundedOffset = tuning != null ? tuning.groundedFootOffset : DefaultGroundedFootOffset;
            var groundMask = tuning != null ? (int)tuning.supportGroundMask : Physics.DefaultRaycastLayers;

            var probeOrigin = sourceFoot.position + Vector3.up * probeHeight;
            if (!Physics.Raycast(probeOrigin, Vector3.down, out var hit, probeHeight + probeDistance, groundMask))
            {
                return;
            }

            var targetPosition = hit.point + Vector3.up * groundedOffset;
            var positionError = targetPosition - footBody.worldCenterOfMass;

            var spring = tuning != null ? tuning.footSupportSpring : DefaultFootSupportSpring;
            var damper = tuning != null ? tuning.footSupportDamper : DefaultFootSupportDamper;
            var maxForce = tuning != null ? tuning.maxFootSupportForce : DefaultMaxFootSupportForce;

            var correctiveForce = positionError * spring;
            var dampingForce = footBody.linearVelocity * damper;
            var finalForce = Vector3.ClampMagnitude(correctiveForce - dampingForce, maxForce);

            footBody.AddForce(finalForce, ForceMode.Acceleration);
        }

        private void SyncAnimatorState()
        {
            if (physicsRig?.Animator == null)
            {
                return;
            }

            var disableAnimator = tuning == null || tuning.disableAnimatorWhileActive;
            physicsRig.Animator.enabled = !(IsActive && disableAnimator);

            if (poseSourceAnimator != null)
            {
                poseSourceAnimator.enabled = IsActive;
            }
        }

        private bool ShouldCapturePoseOnEnable()
        {
            return tuning == null || tuning.capturePoseOnEnable;
        }

        private bool ShouldAlignPhysicalRigOnEnable()
        {
            return tuning == null || tuning.alignPhysicalRigOnEnable;
        }

        private void EnsurePoseSource()
        {
            if (poseSourceRoot != null && poseSourceAnimator != null)
            {
                return;
            }

            if (physicsRig?.PhysicalRoot == null || physicsRig.Animator == null)
            {
                return;
            }

            var sourceTemplateRoot = physicsRig.PhysicalRoot.parent;
            if (sourceTemplateRoot == null)
            {
                return;
            }

            var existing = transform.Find($"{sourceTemplateRoot.name}_PoseSource");
            if (existing != null)
            {
                poseSourceRoot = existing;
                poseSourceAnimator = existing.GetComponent<Animator>();
                return;
            }

            var poseSourceObject = Instantiate(sourceTemplateRoot.gameObject, transform);
            poseSourceObject.name = $"{sourceTemplateRoot.name}_PoseSource";
            poseSourceObject.transform.SetLocalPositionAndRotation(
                sourceTemplateRoot.localPosition,
                sourceTemplateRoot.localRotation);
            poseSourceObject.transform.localScale = sourceTemplateRoot.localScale;

            StripPhysicsComponents(poseSourceObject);
            DisableRenderers(poseSourceObject);

            poseSourceAnimator = poseSourceObject.GetComponent<Animator>();
            if (poseSourceAnimator == null)
            {
                poseSourceAnimator = poseSourceObject.AddComponent<Animator>();
            }

            poseSourceAnimator.avatar = physicsRig.Animator.avatar;
            poseSourceAnimator.runtimeAnimatorController = physicsRig.Animator.runtimeAnimatorController;
            poseSourceAnimator.applyRootMotion = false;
            poseSourceAnimator.updateMode = AnimatorUpdateMode.Fixed;
            poseSourceAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            poseSourceRoot = poseSourceObject.transform;
        }

        private Transform FindSourceTransform(Transform targetTransform)
        {
            if (poseSourceRoot == null || physicsRig?.PhysicalRoot == null)
            {
                return null;
            }

            var templateRoot = physicsRig.PhysicalRoot.parent;
            if (templateRoot == null)
            {
                return null;
            }

            var relativePath = BuildRelativePath(templateRoot, targetTransform);
            if (string.IsNullOrEmpty(relativePath))
            {
                return poseSourceRoot;
            }

            return poseSourceRoot.Find(relativePath);
        }

        private Transform FindSourceTransformForBone(CharacterRigBone bone)
        {
            if (physicsRig != null && physicsRig.TryGetSegment(bone, out var segment) && segment?.transform != null)
            {
                return FindSourceTransform(segment.transform);
            }

            return null;
        }

        private Rigidbody ResolveFootBody(CharacterRigBone bone)
        {
            if (physicsRig != null && physicsRig.TryGetSegment(bone, out var segment))
            {
                return segment?.body;
            }

            return null;
        }

        private static string BuildRelativePath(Transform root, Transform target)
        {
            if (root == null || target == null)
            {
                return string.Empty;
            }

            if (root == target)
            {
                return string.Empty;
            }

            var names = new List<string>();
            var current = target;
            while (current != null && current != root)
            {
                names.Add(current.name);
                current = current.parent;
            }

            if (current != root)
            {
                return string.Empty;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static void StripPhysicsComponents(GameObject root)
        {
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
        }

        private static void DisableRenderers(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                renderer.enabled = false;
            }
        }

        private static void DestroyComponentImmediate(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(component);
                return;
            }

            DestroyImmediate(component);
        }
    }
}
