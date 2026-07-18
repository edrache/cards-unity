using CardsUnity.Controllers;
using CardsUnity.Runtime;
using UnityEngine;

namespace CardsUnity.Debugging
{
    public sealed class BalanceDebugGizmos : MonoBehaviour
    {
        [SerializeField] private CharacterPhysicsRig physicsRig;
        [SerializeField] private CenterOfMassTracker centerOfMassTracker;
        [SerializeField] private BalanceController balanceController;
        [SerializeField] private float supportRadius = 0.1f;
        [SerializeField] private bool drawRigSegments = true;
        [SerializeField] private bool drawJointLinks = true;
        [SerializeField] private float segmentMarkerRadius = 0.04f;

        private void OnDrawGizmosSelected()
        {
            if (centerOfMassTracker == null)
            {
                DrawRig();
                return;
            }

            Gizmos.color = GetStateColor();
            Gizmos.DrawSphere(centerOfMassTracker.WorldCenterOfMass, supportRadius);
            DrawRig();
        }

        private Color GetStateColor()
        {
            if (balanceController == null)
            {
                return Color.white;
            }

            return balanceController.CurrentState switch
            {
                Data.BalanceState.Stable => Color.green,
                Data.BalanceState.Recovering => Color.yellow,
                Data.BalanceState.NeedsStep => new Color(1f, 0.5f, 0f),
                Data.BalanceState.Falling => Color.red,
                _ => Color.white
            };
        }

        private void DrawRig()
        {
            if (physicsRig == null || !drawRigSegments)
            {
                return;
            }

            if (physicsRig.PhysicalRoot != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(physicsRig.PhysicalRoot.position, supportRadius * 0.75f);
            }

            foreach (var segment in physicsRig.Segments)
            {
                if (segment?.transform == null)
                {
                    continue;
                }

                Gizmos.color = ResolveSegmentColor(segment);
                Gizmos.DrawSphere(segment.transform.position, segmentMarkerRadius);

                if (!drawJointLinks)
                {
                    continue;
                }

                var parent = segment.transform.parent;
                if (parent == null)
                {
                    continue;
                }

                Gizmos.DrawLine(parent.position, segment.transform.position);
            }
        }

        private static Color ResolveSegmentColor(CharacterBodySegment segment)
        {
            if (!segment.HasPhysicsBody)
            {
                return Color.red;
            }

            if (!segment.HasCollider)
            {
                return new Color(1f, 0.4f, 0f);
            }

            if (segment.bone != Data.CharacterRigBone.Hips && !segment.HasJoint)
            {
                return Color.yellow;
            }

            return Color.cyan;
        }
    }
}
