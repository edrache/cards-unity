using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>A fallen cave boulder that becomes movable after its first landing.</summary>
    [DisallowMultipleComponent]
    public sealed class CaveBoulder : MonoBehaviour
    {
        private const int CastCapacity = 64;
        private static readonly List<CaveBoulder> activeBoulders = new List<CaveBoulder>();

        private readonly RaycastHit[] castHits = new RaycastHit[CastCapacity];
        private CaveRockfallTrap trap;
        private Transform player;
        private CharacterController playerController;
        private SphereCollider sphere;
        private Rigidbody body;
        private Collider[] ignoredOwnerColliders;
        private float radius;
        private float fallAge;
        private float fallenDistance;
        private float settleTime;
        private Vector3 previousPhysicsPosition;
        private bool initialFall;
        private bool hasLanded;
        private bool settled;
        private bool restoringOwnerCollision;

        public static IReadOnlyList<CaveBoulder> ActiveBoulders => activeBoulders;
        public bool CanPickUp => isActiveAndEnabled && hasLanded && settled && !IsHeld;
        public bool IsHeld { get; private set; }
        public Transform Holder { get; private set; }
        public Vector3 ReachPoint => transform.position;
        public float Radius => radius;
        internal bool HasLanded => hasLanded && settled;

        private void OnEnable()
        {
            if (!activeBoulders.Contains(this)) activeBoulders.Add(this);
        }

        private void OnDisable()
        {
            RestoreOwnerCollisions();
            activeBoulders.Remove(this);
        }

        internal void Initialize(CaveRockfallTrap owner, Transform target, float worldRadius,
            Mesh mesh, Material material, PhysicsMaterial physicsMaterial)
        {
            trap = owner;
            player = target;
            playerController = player != null ? player.GetComponent<CharacterController>() : null;
            radius = Mathf.Max(0.2f, worldRadius);
            transform.localScale = Vector3.one * radius;

            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;

            sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.98f;
            sphere.sharedMaterial = physicsMaterial;

            body = gameObject.AddComponent<Rigidbody>();
            body.mass = Mathf.Max(4f, radius * radius * radius * 32f);
            body.linearDamping = 1.15f;
            body.angularDamping = 4.5f;
            body.maxAngularVelocity = 4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            initialFall = true;
            previousPhysicsPosition = transform.position;
        }

        private void FixedUpdate()
        {
            if (restoringOwnerCollision) TryRestoreOwnerCollisionsWhenClear();
            if (IsHeld || body == null) return;

            Vector3 position = body.position;
            if (initialFall)
            {
                fallAge += Time.fixedDeltaTime;
                fallenDistance += Mathf.Max(0f, previousPhysicsPosition.y - position.y);
                DetectPlayerAlongPath(previousPhysicsPosition, position);
                previousPhysicsPosition = position;
            }

            if (!hasLanded) return;
            if (body.IsSleeping() || (body.linearVelocity.sqrMagnitude < 0.025f
                && body.angularVelocity.sqrMagnitude < 0.25f))
                settleTime += Time.fixedDeltaTime;
            else
                settleTime = 0f;

            if (settleTime >= 0.4f) FreezeAsBlocker();
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleCollision(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            HandleCollision(collision);
        }

        private void HandleCollision(Collision collision)
        {
            if (!initialFall || IsHeld) return;
            bool hitPlayer = IsPlayerCollider(collision.collider);
            if (hitPlayer)
            {
                trap?.TryHitPlayer(this);
                return;
            }

            if (fallAge < 0.12f || fallenDistance < Mathf.Max(0.18f, radius * 0.35f)) return;
            CaveBoulder supportingBoulder = collision.collider.GetComponentInParent<CaveBoulder>();
            if (supportingBoulder != null && supportingBoulder != this && !supportingBoulder.hasLanded) return;
            if (supportingBoulder == null && collision.rigidbody != null && !collision.rigidbody.isKinematic) return;
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.y <= 0.35f) continue;
                initialFall = false;
                hasLanded = true;
                settleTime = 0f;
                return;
            }
        }

        private void DetectPlayerAlongPath(Vector3 from, Vector3 to)
        {
            if (player == null || playerController == null || !playerController.enabled) return;
            Vector3 center = player.transform.TransformPoint(playerController.center);
            Vector3 up = player.transform.up;
            Vector3 scale = player.transform.lossyScale;
            float controllerRadius = playerController.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfAxis = Mathf.Max(0f, playerController.height * Mathf.Abs(scale.y) * 0.5f - controllerRadius);
            Vector3 bottom = center - up * halfAxis;
            Vector3 top = center + up * halfAxis;
            float combinedRadius = radius + controllerRadius;
            if (SegmentDistanceSquared(from, to, bottom, top) <= combinedRadius * combinedRadius)
                trap?.TryHitPlayer(this);
        }

        private bool IsPlayerCollider(Collider candidate)
        {
            return candidate != null && player != null
                && (candidate.transform == player || candidate.transform.IsChildOf(player));
        }

        /// <summary>Begins carrying without moving the boulder through surrounding geometry.</summary>
        public bool TryCarry(Transform holder)
        {
            if (!CanPickUp || holder == null || body == null || sphere == null) return false;
            IsHeld = true;
            Holder = holder;
            restoringOwnerCollision = false;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;

            CharacterController ownerController = holder.GetComponentInParent<CharacterController>();
            Transform ownerRoot = ownerController != null ? ownerController.transform : holder.root;
            ignoredOwnerColliders = ownerRoot.GetComponentsInChildren<Collider>();
            for (int i = 0; i < ignoredOwnerColliders.Length; i++)
                if (ignoredOwnerColliders[i] != null && ignoredOwnerColliders[i] != sphere)
                    Physics.IgnoreCollision(sphere, ignoredOwnerColliders[i], true);
            return true;
        }

        /// <summary>Moves a carried boulder only when the entire swept path is unobstructed.</summary>
        public bool MoveCarried(Vector3 worldPosition, Quaternion rotation)
        {
            if (!IsHeld || Holder == null || body == null || sphere == null) return false;
            Vector3 start = body.position;
            Vector3 delta = worldPosition - start;
            float distance = delta.magnitude;
            if (distance > 0.0001f)
            {
                Vector3 direction = delta / distance;
                int count = Physics.SphereCastNonAlloc(start, radius * 0.92f, direction, castHits,
                    distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (count == castHits.Length) return false;
                for (int i = 0; i < count; i++)
                {
                    Collider hit = castHits[i].collider;
                    if (ShouldIgnoreDuringCarry(hit)) continue;
                    // Contact with the supporting floor must not prevent lifting or sliding a landed rock.
                    if (castHits[i].distance <= 0.02f && castHits[i].normal.y > 0.5f && direction.y >= -0.05f)
                        continue;
                    return false;
                }
            }

            int overlapCount = Physics.OverlapSphereNonAlloc(worldPosition, radius * 0.96f,
                CarryOverlapBuffer.Hits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (overlapCount == CarryOverlapBuffer.Hits.Length) return false;
            for (int i = 0; i < overlapCount; i++)
            {
                Collider obstacle = CarryOverlapBuffer.Hits[i];
                if (ShouldIgnoreDuringCarry(obstacle)) continue;
                if (Physics.ComputePenetration(sphere, worldPosition, rotation, obstacle,
                    obstacle.transform.position, obstacle.transform.rotation, out _, out float depth)
                    && depth > 0.01f)
                    return false;
            }

            body.position = worldPosition;
            body.rotation = rotation;
            return true;
        }

        /// <summary>Releases the boulder in place and restores owner collisions once separated.</summary>
        public void Drop()
        {
            if (!IsHeld || body == null) return;
            IsHeld = false;
            Holder = null;
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            settleTime = 0f;
            settled = false;
            restoringOwnerCollision = ignoredOwnerColliders != null && ignoredOwnerColliders.Length > 0;
            TryRestoreOwnerCollisionsWhenClear();
        }

        private bool ShouldIgnoreDuringCarry(Collider candidate)
        {
            if (candidate == null || candidate == sphere || candidate.transform.IsChildOf(transform)) return true;
            if (ignoredOwnerColliders == null) return false;
            for (int i = 0; i < ignoredOwnerColliders.Length; i++)
                if (candidate == ignoredOwnerColliders[i]) return true;
            return false;
        }

        private void TryRestoreOwnerCollisionsWhenClear()
        {
            if (!restoringOwnerCollision || sphere == null)
            {
                if (!restoringOwnerCollision) ignoredOwnerColliders = null;
                return;
            }

            for (int i = 0; i < ignoredOwnerColliders.Length; i++)
            {
                Collider ownerCollider = ignoredOwnerColliders[i];
                if (ownerCollider == null || !ownerCollider.enabled) continue;
                if (Physics.ComputePenetration(sphere, transform.position, transform.rotation,
                    ownerCollider, ownerCollider.transform.position, ownerCollider.transform.rotation,
                    out _, out float depth) && depth > 0.005f)
                    return;
            }
            RestoreOwnerCollisions();
        }

        private void RestoreOwnerCollisions()
        {
            if (sphere != null && ignoredOwnerColliders != null)
                for (int i = 0; i < ignoredOwnerColliders.Length; i++)
                    if (ignoredOwnerColliders[i] != null && ignoredOwnerColliders[i] != sphere)
                        Physics.IgnoreCollision(sphere, ignoredOwnerColliders[i], false);
            ignoredOwnerColliders = null;
            restoringOwnerCollision = false;
        }

        private void FreezeAsBlocker()
        {
            if (body == null || body.isKinematic || IsHeld) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
            settled = true;
        }

        private static float SegmentDistanceSquared(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1;
            Vector3 d2 = q2 - p2;
            Vector3 r = p1 - p2;
            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);
            float s;
            float t;

            if (a <= Mathf.Epsilon && e <= Mathf.Epsilon) return r.sqrMagnitude;
            if (a <= Mathf.Epsilon)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= Mathf.Epsilon)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denominator = a * e - b * b;
                    s = denominator > Mathf.Epsilon ? Mathf.Clamp01((b * f - c * e) / denominator) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            Vector3 c1 = p1 + d1 * s;
            Vector3 c2 = p2 + d2 * t;
            return (c1 - c2).sqrMagnitude;
        }

        private static class CarryOverlapBuffer
        {
            internal static readonly Collider[] Hits = new Collider[CastCapacity];
        }
    }
}
