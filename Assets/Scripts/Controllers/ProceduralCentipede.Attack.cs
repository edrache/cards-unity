using UnityEngine;

namespace CardsUnity.Controllers
{
    public sealed partial class ProceduralCentipede
    {
        [Header("Light provoked attack")]
        [SerializeField, Min(0.1f)] private float lightPatience = 3f;
        [SerializeField, Min(0.5f)] private float attackRange = 3.5f;
        [SerializeField, Min(0.1f)] private float attackWindup = 0.65f;
        [SerializeField, Min(0.1f)] private float leapDuration = 0.45f;
        [SerializeField, Min(0f)] private float leapHeight = 0.8f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 4f;
        private float lightTime, attackTime, cooldown, leapDistance;
        private bool attackHit;
        public float LightExposureTime => lightTime;
        public bool IsAttacking => State == BehaviourState.WindingUp || State == BehaviourState.Leaping;

        private void ResetAttack()
        {
            lightTime = attackTime = cooldown = 0f;
            if (IsAttacking || State == BehaviourState.Enraged) State = BehaviourState.Stalking;
        }

        private void FinishAttack()
        {
            State = BehaviourState.Hiding;
            darkTime = lightTime = attackTime = 0f;
            cooldown = Mathf.Max(0.1f, attackCooldown);
            currentHideDuration = hideDuration;
            progressOrigin = transform.position;
            progressTime = recoveryRemaining = 0f;
            FollowBody();
            PoseLegs();
        }

        private bool TickAttack(float dt, float exposure, float threshold)
        {
            cooldown = Mathf.Max(0f, cooldown - dt);
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                if (IsAttacking || State == BehaviourState.Enraged) FinishAttack();
                lightTime = 0f;
                return false;
            }
            if (!IsAttacking)
            {
                // Brief shadows dissipate frustration rather than resetting it every frame.
                lightTime = exposure >= threshold ? lightTime + dt : Mathf.Max(0f, lightTime - dt);
                if (cooldown <= 0f && lightTime >= Mathf.Max(0.1f, lightPatience)) State = BehaviourState.Enraged;
                if (State != BehaviourState.Enraged) return false;
                var victim = target.GetComponent<CharacterKnockdown>();
                if (victim != null && !victim.CanBeHit) return false;
                Vector3 delta = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
                if (delta.magnitude > attackRange || transform.up.y < 0.65f || delta.sqrMagnitude < 0.001f) return false;
                if (SurfaceRay(transform.position + Vector3.up * 0.5f, delta.normalized, delta.magnitude, out _)) return false;
                State = BehaviourState.WindingUp;
                attackTime = 0f;
                attackHit = false;
                recoveryRemaining = 0f;
            }
            float remaining = dt;
            // Substeps keep both collision and phase transitions reliable on long frames.
            while (remaining > 0f && IsAttacking)
            {
                float step = Mathf.Min(remaining, 1f / 120f);
                remaining -= step;
                attackTime += step;
                if (State == BehaviourState.WindingUp)
                {
                    Vector3 direction = Vector3.ProjectOnPlane(target.position - transform.position, transform.up);
                    if (direction.sqrMagnitude > 0.001f)
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction, transform.up), turnSpeed * step);
                    RecordContact();
                    if (attackTime >= Mathf.Max(0.1f, attackWindup))
                    {
                        State = BehaviourState.Leaping;
                        attackTime = 0f;
                        leapDistance = Mathf.Min(attackRange + 0.5f, direction.magnitude + 0.4f);
                    }
                }
                else
                {
                    float distance = leapDistance * step / Mathf.Max(0.1f, leapDuration);
                    Vector3 oldPosition = rig.position;
                    Quaternion oldRotation = rig.rotation;
                    // The contact path remains on valid terrain; the rendered body follows an arc.
                    if (!TrySurfaceStep(transform.position, transform.rotation, distance, out Pose next)
                        || (next.rotation * Vector3.up).y < 0.65f)
                    { FinishAttack(); break; }
                    Vector3 castOrigin = transform.position + Vector3.up * (0.22f * size + leapHeight * Mathf.Sin(Mathf.PI * Mathf.Clamp01(attackTime / Mathf.Max(0.1f, leapDuration))));
                    bool blocked = false;
                    foreach (var hit in Physics.SphereCastAll(castOrigin, 0.15f * size, transform.forward,
                                 distance, environmentMask, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(target)
                            || hit.transform.GetComponentInParent<ProceduralCentipede>() != null) continue;
                        if (hit.normal.y < 0.65f) { blocked = true; break; }
                    }
                    if (blocked) { FinishAttack(); break; }
                    transform.SetPositionAndRotation(next.position, next.rotation);
                    rig.SetPositionAndRotation(oldPosition, oldRotation);
                    RecordContact();
                    ApplyAttackPose();
                    var controller = target.GetComponent<CharacterController>();
                    Vector3 head = segments[0].position;
                    if (!attackHit && controller != null && controller.enabled
                        && Vector3.Distance(controller.ClosestPoint(head), head) <= 0.3f * size)
                    {
                        attackHit = true;
                        target.GetComponent<CharacterKnockdown>()?.TryKnockDown();
                    }
                    if (attackTime >= Mathf.Max(0.1f, leapDuration)) FinishAttack();
                }
            }
            if (IsAttacking) ApplyAttackPose();
            return true;
        }

        private void ApplyAttackPose()
        {
            FollowBody();
            float windup = State == BehaviourState.WindingUp
                ? Mathf.SmoothStep(0f, 1f, attackTime / Mathf.Max(0.1f, attackWindup))
                : 1f - Mathf.Clamp01(attackTime / Mathf.Max(0.1f, leapDuration));
            float arc = State == BehaviourState.Leaping
                ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(attackTime / Mathf.Max(0.1f, leapDuration))) * leapHeight : 0f;
            int front = Mathf.Min(5, segments.Length - 1);
            for (int i = front - 1; i >= 0; i--)
            {
                float influence = 1f - (float)i / front;
                segments[i].rotation *= Quaternion.Euler(-55f * windup * influence, 0f, 0f);
                // Bend a chain of fixed-length links instead of separating the armor plates.
                Vector3 tangent = (segments[i].forward + segments[i + 1].forward).normalized;
                Vector3 curved = segments[i + 1].position + tangent * (0.3f * size);
                segments[i].position = Vector3.Lerp(segments[i].position, curved, windup);
            }
            for (int i = 0; i < segments.Length; i++) segments[i].position += Vector3.up * arc;
            PoseLegs();
        }
    }
}
