using System.Collections.Generic;
using Obi;
using UnityEngine;

namespace CardsUnity
{
    [RequireComponent(typeof(ObiActor))]
    public class ObiSockSleeper : MonoBehaviour
    {
        [Tooltip("Particle speed below which it counts as at rest.")]
        [SerializeField] private float sleepVelocityThreshold = 0.05f;

        [Tooltip("Seconds all particles must stay below the threshold before sleeping.")]
        [SerializeField] private float minRestTimeBeforeSleep = 0.5f;

        [Tooltip("Radius around a newly-grabbed sock's particles in which sleeping socks wake up.")]
        [SerializeField] private float wakeUpRadius = 0.4f;

        [Tooltip("Grace period after waking before the sleep check resumes. 0 disables it.")]
        [SerializeField] private float minActiveTimeAfterWake = 1f;

        public static readonly List<ObiSockSleeper> All = new List<ObiSockSleeper>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll()
        {
            All.Clear();
        }

        private ObiActor actor;
        private SockSleepState state;
        private float[] originalInvMasses;
        private bool frozen;
        private bool isHeld;

        private void Awake()
        {
            actor = GetComponent<ObiActor>();
            state = new SockSleepState(
                sleepVelocityThreshold,
                minRestTimeBeforeSleep,
                wakeUpRadius,
                minActiveTimeAfterWake);
            All.Add(this);
        }

        private void OnDestroy()
        {
            All.Remove(this);
        }

        private void FixedUpdate()
        {
            if (isHeld || actor == null || !actor.isLoaded || actor.solver == null)
                return;

            if (state.Tick(GetMaxParticleSpeed(), Time.fixedDeltaTime))
                Freeze();
        }

        public void OnGrabbed()
        {
            isHeld = true;
            WakeUp();
        }

        public void OnReleased()
        {
            isHeld = false;
        }

        public void WakeUp()
        {
            Unfreeze();
            state.WakeUp();
        }

        public static void WakeNearby(ObiSockSleeper source)
        {
            if (source == null || source.actor == null || !source.actor.isLoaded || source.actor.solver == null)
                return;

            Vector3 sourceCentroid = source.GetCentroid();
            float sqrRadius = source.state.WakeUpRadius * source.state.WakeUpRadius;

            for (int i = 0; i < All.Count; i++)
            {
                ObiSockSleeper sleeper = All[i];
                if (sleeper == null || sleeper == source || sleeper.state.CurrentState != SockSleepState.State.Asleep)
                    continue;

                if ((sleeper.GetCentroid() - sourceCentroid).sqrMagnitude <= sqrRadius)
                    sleeper.WakeUp();
            }
        }

        private float GetMaxParticleSpeed()
        {
            ObiNativeVector4List velocities = actor.solver.velocities;
            int count = actor.particleCount;
            float maxSqrSpeed = 0f;

            for (int i = 0; i < count; i++)
            {
                Vector4 velocity = velocities[actor.solverIndices[i]];
                float sqrSpeed = velocity.x * velocity.x + velocity.y * velocity.y + velocity.z * velocity.z;
                if (sqrSpeed > maxSqrSpeed)
                    maxSqrSpeed = sqrSpeed;
            }

            return Mathf.Sqrt(maxSqrSpeed);
        }

        private Vector3 GetCentroid()
        {
            if (actor == null || !actor.isLoaded || actor.particleCount == 0)
                return transform.position;

            Vector3 sum = Vector3.zero;
            int count = actor.particleCount;

            for (int i = 0; i < count; i++)
                sum += actor.GetParticlePosition(actor.solverIndices[i]);

            return sum / count;
        }

        private void Freeze()
        {
            if (frozen || actor == null || !actor.isLoaded || actor.solver == null)
                return;

            frozen = true;

            ObiNativeFloatList invMasses = actor.solver.invMasses;
            int count = actor.particleCount;
            originalInvMasses = new float[count];

            for (int i = 0; i < count; i++)
            {
                int solverIndex = actor.solverIndices[i];
                originalInvMasses[i] = invMasses[solverIndex];
                invMasses[solverIndex] = 0f;
            }
        }

        private void Unfreeze()
        {
            if (!frozen || actor == null || !actor.isLoaded || actor.solver == null || originalInvMasses == null)
                return;

            frozen = false;

            ObiNativeFloatList invMasses = actor.solver.invMasses;
            int count = Mathf.Min(actor.particleCount, originalInvMasses.Length);

            for (int i = 0; i < count; i++)
                invMasses[actor.solverIndices[i]] = originalInvMasses[i];
        }
    }
}
