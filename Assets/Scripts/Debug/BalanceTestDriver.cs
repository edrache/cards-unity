using System.Collections.Generic;
using CardsUnity.Data;
using CardsUnity.Runtime;
using UnityEngine;

namespace CardsUnity.Debugging
{
    public sealed class BalanceTestDriver : MonoBehaviour
    {
        [SerializeField] private CharacterPhysicsRig physicsRig;
        [SerializeField] private CharacterRigBone targetBone = CharacterRigBone.Hips;
        [SerializeField] private Vector3 pushDirection = Vector3.forward;
        [SerializeField] private float pushForce = 50f;
        [SerializeField] private KeyCode pushKey = KeyCode.P;
        [SerializeField] private bool disableAnimatorBeforePush = true;
        [SerializeField] private ForceMode forceMode = ForceMode.VelocityChange;
        [SerializeField] private bool logPushEvents = true;

        private readonly List<Rigidbody> bodies = new();

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (Input.GetKeyDown(pushKey))
            {
                ApplyPush();
            }
        }

        [ContextMenu("Apply Push")]
        public void ApplyPush()
        {
            if (physicsRig == null)
            {
                if (logPushEvents)
                {
                    Debug.LogWarning("BalanceTestDriver: missing CharacterPhysicsRig reference.", this);
                }

                return;
            }

            if (disableAnimatorBeforePush && physicsRig.Animator != null && physicsRig.Animator.enabled)
            {
                physicsRig.Animator.enabled = false;
            }

            physicsRig.GetBodies(bodies);
            for (var i = 0; i < bodies.Count; i++)
            {
                bodies[i].WakeUp();
            }

            var targetBody = ResolveTargetBody();
            if (targetBody == null)
            {
                if (logPushEvents)
                {
                    Debug.LogWarning($"BalanceTestDriver: no Rigidbody found for target bone {targetBone}.", this);
                }

                return;
            }

            var direction = pushDirection.sqrMagnitude > Mathf.Epsilon
                ? pushDirection.normalized
                : Vector3.forward;

            targetBody.AddForce(direction * pushForce, forceMode);

            if (logPushEvents)
            {
                Debug.Log(
                    $"BalanceTestDriver: pushed {targetBone} with {forceMode} force {pushForce} in direction {direction}.",
                    this);
            }
        }

        [ContextMenu("Re-Enable Animator")]
        public void ReEnableAnimator()
        {
            if (physicsRig?.Animator != null)
            {
                physicsRig.Animator.enabled = true;
            }
        }

        private Rigidbody ResolveTargetBody()
        {
            if (physicsRig.TryGetSegment(targetBone, out var segment) && segment?.body != null)
            {
                return segment.body;
            }

            return physicsRig.RootBody;
        }
    }
}
