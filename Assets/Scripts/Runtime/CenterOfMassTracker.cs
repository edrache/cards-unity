using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.Runtime
{
    public sealed class CenterOfMassTracker : MonoBehaviour
    {
        [SerializeField] private CharacterPhysicsRig rig;
        [SerializeField] private List<Rigidbody> bodies = new();
        private readonly List<Rigidbody> cachedBodies = new();

        public Vector3 WorldCenterOfMass { get; private set; }
        public Vector3 Velocity { get; private set; }
        public float HorizontalSpeed => new Vector2(Velocity.x, Velocity.z).magnitude;

        private void FixedUpdate()
        {
            Recalculate();
        }

        public void Recalculate()
        {
            var activeBodies = ResolveBodies();
            if (activeBodies.Count == 0)
            {
                WorldCenterOfMass = transform.position;
                Velocity = Vector3.zero;
                return;
            }

            var totalMass = 0f;
            var weightedPosition = Vector3.zero;
            var weightedVelocity = Vector3.zero;

            foreach (var body in activeBodies)
            {
                if (body == null)
                {
                    continue;
                }

                totalMass += body.mass;
                weightedPosition += body.worldCenterOfMass * body.mass;
                weightedVelocity += body.linearVelocity * body.mass;
            }

            if (totalMass <= Mathf.Epsilon)
            {
                WorldCenterOfMass = transform.position;
                Velocity = Vector3.zero;
                return;
            }

            WorldCenterOfMass = weightedPosition / totalMass;
            Velocity = weightedVelocity / totalMass;
        }

        private IReadOnlyList<Rigidbody> ResolveBodies()
        {
            if (rig != null && rig.HasRig)
            {
                rig.GetBodies(cachedBodies);
                return cachedBodies;
            }

            return bodies;
        }
    }
}
