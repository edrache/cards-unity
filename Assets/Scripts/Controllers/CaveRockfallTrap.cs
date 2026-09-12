using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>A one-shot warning and cave-in sequence placed along a corridor interval.</summary>
    [DisallowMultipleComponent]
    public sealed class CaveRockfallTrap : MonoBehaviour
    {
        public enum RockfallState
        {
            Dormant,
            Warning,
            Collapsing,
            Spent
        }

        private struct BoulderPlan
        {
            public Vector3 floorPosition;
            public float radius;
            public Quaternion rotation;
            public float spawnTime;
        }

        private readonly List<CaveBoulder> spawnedBoulders = new List<CaveBoulder>();
        private ProceduralCave cave;
        private CaveRockfallFeedback feedback;
        private Vector3[] floorPath;
        private Transform player;
        private Material boulderMaterial;
        private Mesh boulderMesh;
        private PhysicsMaterial boulderPhysicsMaterial;
        private ParticleSystem pebbles;
        private System.Random random;
        private BoulderPlan[] plans;
        private float width;
        private float ceilingHeight;
        private float warningSeconds;
        private float collapseSeconds;
        private float idlePebbleRate;
        private float activePebbleRate;
        private float pebbleLifetime;
        private float minimumPebbleSize;
        private float maximumPebbleSize;
        private float minimumBoulderRadius;
        private float maximumBoulderRadius;
        private float stateTime;
        private float pebbleAccumulator;
        private int nextBoulder;
        private bool configured;
        private bool damageSpent;

        public RockfallState State { get; private set; }
        public IReadOnlyList<Vector3> FloorPath => floorPath ?? Array.Empty<Vector3>();
        public IReadOnlyList<CaveBoulder> SpawnedBoulders => spawnedBoulders;
        public int SpawnedBoulderCount => spawnedBoulders.Count;

        public void Configure(ProceduralCave owner, Vector3[] path, float corridorWidth, Transform target,
            float height, float warningDuration, float collapseDuration, float ambientPebbles,
            float warningPebbles, float particleLifetime, Material material, int seed,
            float minPebbleSize = 0.025f, float maxPebbleSize = 0.085f,
            float minBoulderRadius = 0.55f, float maxBoulderRadius = 0.67f)
        {
            cave = owner;
            feedback = owner != null ? owner.GetComponent<CaveRockfallFeedback>() : null;
            floorPath = path != null ? (Vector3[])path.Clone() : Array.Empty<Vector3>();
            player = target;
            width = Mathf.Max(1f, corridorWidth);
            ceilingHeight = Mathf.Max(1f, height);
            warningSeconds = Mathf.Max(0.05f, warningDuration);
            collapseSeconds = Mathf.Max(0.05f, collapseDuration);
            idlePebbleRate = Mathf.Max(0f, ambientPebbles);
            activePebbleRate = Mathf.Max(idlePebbleRate, warningPebbles);
            pebbleLifetime = Mathf.Max(0.1f, particleLifetime);
            minimumPebbleSize = Mathf.Max(0.001f, minPebbleSize);
            maximumPebbleSize = Mathf.Max(minimumPebbleSize, maxPebbleSize);
            minimumBoulderRadius = Mathf.Max(0.1f, minBoulderRadius);
            maximumBoulderRadius = Mathf.Max(minimumBoulderRadius, maxBoulderRadius);
            boulderMaterial = material;
            random = new System.Random(seed);
            State = RockfallState.Dormant;
            stateTime = 0f;
            pebbleAccumulator = 0f;
            nextBoulder = 0;
            damageSpent = false;
            configured = floorPath.Length >= 2;

            if (!configured) return;
            CreateOwnedResources();
            plans = CreateBoulderPlans();
            if (Application.isPlaying) CreatePebbleSystem();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Advances the one-shot state machine and may be called directly by focused checks.</summary>
        public void Tick(float dt)
        {
            if (!configured || dt <= 0f) return;
            dt = Mathf.Max(0f, dt);

            if (State == RockfallState.Dormant && PlayerEnteredInterval())
            {
                State = RockfallState.Warning;
                stateTime = 0f;
            }

            float emissionRate = idlePebbleRate;
            switch (State)
            {
                case RockfallState.Warning:
                    stateTime += dt;
                    emissionRate = Mathf.Lerp(idlePebbleRate, activePebbleRate,
                        Mathf.Clamp01(stateTime / warningSeconds));
                    if (stateTime >= warningSeconds)
                    {
                        float collapseRemainder = stateTime - warningSeconds;
                        State = RockfallState.Collapsing;
                        stateTime = collapseRemainder;
                        SamplePath(PathLength() * 0.5f, out Vector3 collapsePosition, out _);
                        feedback?.PlayCollapse(transform, collapsePosition, player);
                        SpawnDueBoulders();
                    }
                    break;
                case RockfallState.Collapsing:
                    stateTime += dt;
                    emissionRate = activePebbleRate;
                    SpawnDueBoulders();
                    float fallTimeout = Mathf.Sqrt(2f * ceilingHeight
                        / Mathf.Max(0.01f, Mathf.Abs(Physics.gravity.y))) + 4f;
                    if (nextBoulder >= plans.Length && (AllBouldersLanded()
                        || stateTime >= collapseSeconds + fallTimeout))
                    {
                        State = RockfallState.Spent;
                        stateTime = 0f;
                    }
                    break;
                case RockfallState.Spent:
                    emissionRate = 0f;
                    break;
            }

            EmitPebbles(emissionRate, dt);
        }

        internal void NotifyBoulderLanded(CaveBoulder source)
        {
            if (source != null) feedback?.PlayLanding(source.transform.position, player);
        }

        internal void TryHitPlayer(CaveBoulder source)
        {
            if (damageSpent || source == null || State != RockfallState.Collapsing || player == null) return;
            CharacterHealth health = player.GetComponent<CharacterHealth>();
            if (health == null || health.IsDead || player.GetComponent<CaveExit>()?.IsCompleted == true) return;
            if (!health.TryTakeDamage()) return;

            damageSpent = true;
            player.GetComponent<CharacterStress>()?.RegisterHit();
            if (!health.IsDead) player.GetComponent<CharacterKnockdown>()?.TryKnockDown();
        }

        private bool PlayerEnteredInterval()
        {
            if (player == null || player.GetComponent<CharacterHealth>()?.IsDead == true
                || player.GetComponent<CaveExit>()?.IsCompleted == true)
                return false;
            Vector2 point = new Vector2(player.position.x, player.position.z);
            float threshold = width * 0.5f;
            float best = float.PositiveInfinity;
            for (int i = 1; i < floorPath.Length; i++)
            {
                Vector2 a = new Vector2(floorPath[i - 1].x, floorPath[i - 1].z);
                Vector2 b = new Vector2(floorPath[i].x, floorPath[i].z);
                Vector2 segment = b - a;
                float t = segment.sqrMagnitude > 0.0001f
                    ? Mathf.Clamp01(Vector2.Dot(point - a, segment) / segment.sqrMagnitude) : 0f;
                best = Mathf.Min(best, (point - (a + segment * t)).sqrMagnitude);
            }
            return best <= threshold * threshold;
        }

        private BoulderPlan[] CreateBoulderPlans()
        {
            float length = PathLength();
            int across = Mathf.Clamp(Mathf.RoundToInt(width / 1.1f), 2, 3);
            int rows = Mathf.Clamp(Mathf.CeilToInt(length / 2f), 1, 3);
            float maximumRadius = Mathf.Min(maximumBoulderRadius, width / (2f * (across - 0.15f)));
            float minimumRadius = Mathf.Min(minimumBoulderRadius, maximumRadius);
            float sideExtent = Mathf.Max(0f, width * 0.5f - maximumRadius - 0.06f);
            var result = new BoulderPlan[across * rows];
            int index = 0;
            for (int row = 0; row < rows; row++)
            {
                float along = length * 0.5f + (row - (rows - 1) * 0.5f) * 1.05f;
                SamplePath(Mathf.Clamp(along, 0f, length), out Vector3 floor, out Vector3 tangent);
                Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                if (side.sqrMagnitude < 0.5f) side = Vector3.right;
                for (int column = 0; column < across; column++)
                {
                    float acrossT = across == 1 ? 0f : column / (float)(across - 1) * 2f - 1f;
                    float radius = NextFloat(minimumRadius, maximumRadius);
                    Vector3 position = floor + side * (acrossT * sideExtent + NextFloat(-0.025f, 0.025f));
                    result[index++] = new BoulderPlan
                    {
                        floorPosition = position,
                        radius = radius,
                        rotation = Quaternion.Euler(NextFloat(0f, 360f), NextFloat(0f, 360f), NextFloat(0f, 360f)),
                        spawnTime = 0f
                    };
                }
            }

            // Shuffle the spatial order while keeping timing deterministic for the trap seed.
            for (int i = result.Length - 1; i > 0; i--)
            {
                int swap = random.Next(i + 1);
                BoulderPlan value = result[i];
                result[i] = result[swap];
                result[swap] = value;
            }
            for (int i = 0; i < result.Length; i++)
            {
                BoulderPlan plan = result[i];
                plan.spawnTime = result.Length <= 1 ? 0f : collapseSeconds * i / (result.Length - 1f);
                result[i] = plan;
            }
            return result;
        }

        private void SpawnDueBoulders()
        {
            while (nextBoulder < plans.Length && stateTime + 0.0001f >= plans[nextBoulder].spawnTime)
            {
                BoulderPlan plan = plans[nextBoulder++];
                var boulderObject = new GameObject($"Fallen Boulder {nextBoulder}") { hideFlags = HideFlags.DontSave };
                boulderObject.transform.SetParent(transform, false);
                Vector3 spawn = plan.floorPosition + Vector3.up * Mathf.Max(plan.radius + 0.2f,
                    ceilingHeight - plan.radius - 0.08f);
                boulderObject.transform.SetPositionAndRotation(spawn, plan.rotation);
                var boulder = boulderObject.AddComponent<CaveBoulder>();
                boulder.Initialize(this, player, plan.radius, boulderMesh, boulderMaterial, boulderPhysicsMaterial);
                spawnedBoulders.Add(boulder);
            }
        }

        private bool AllBouldersLanded()
        {
            if (spawnedBoulders.Count != plans.Length) return false;
            for (int i = 0; i < spawnedBoulders.Count; i++)
                if (spawnedBoulders[i] != null && !spawnedBoulders[i].HasLanded) return false;
            return true;
        }

        private void CreateOwnedResources()
        {
            boulderMesh = BuildLowPolyRockMesh();
            boulderMesh.name = "Rockfall Low Poly Boulder";
            boulderMesh.hideFlags = HideFlags.DontSave;
            boulderPhysicsMaterial = new PhysicsMaterial("Rockfall High Friction")
            {
                dynamicFriction = 0.92f,
                staticFriction = 1f,
                bounciness = 0.02f,
                frictionCombine = PhysicsMaterialCombine.Maximum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
                hideFlags = HideFlags.DontSave
            };
        }

        private void CreatePebbleSystem()
        {
            var pebbleObject = new GameObject("Warning Pebbles") { hideFlags = HideFlags.DontSave };
            pebbleObject.transform.SetParent(transform, false);
            pebbles = pebbleObject.AddComponent<ParticleSystem>();
            var main = pebbles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = pebbleLifetime;
            main.startSpeed = 0f;
            main.startSize = 0.06f;
            main.gravityModifier = 1.25f;
            main.maxParticles = Mathf.Clamp(Mathf.CeilToInt(activePebbleRate * pebbleLifetime * 1.5f), 32, 384);

            var emission = pebbles.emission;
            emission.enabled = false;
            var shape = pebbles.shape;
            shape.enabled = false;
            var collision = pebbles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.High;
            collision.collidesWith = Physics.DefaultRaycastLayers;
            collision.dampen = 0.65f;
            collision.bounce = 0.08f;
            collision.lifetimeLoss = 0.4f;

            var color = pebbles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.55f, 0.5f, 0.44f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var size = pebbles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.72f, 1f), new Keyframe(1f, 0f)));

            var particleRenderer = pebbleObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            particleRenderer.mesh = boulderMesh;
            particleRenderer.sharedMaterial = boulderMaterial;
            pebbles.Play();
        }

        private void EmitPebbles(float rate, float dt)
        {
            if (pebbles == null || rate <= 0f) return;
            pebbleAccumulator += rate * dt;
            int count = Mathf.Min(64, Mathf.FloorToInt(pebbleAccumulator));
            pebbleAccumulator -= count;
            float length = PathLength();
            for (int i = 0; i < count; i++)
            {
                SamplePath(NextFloat(0f, length), out Vector3 floor, out Vector3 tangent);
                Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                var emit = new ParticleSystem.EmitParams
                {
                    position = floor + side * NextFloat(-width * 0.42f, width * 0.42f)
                        + Vector3.up * NextFloat(ceilingHeight * 0.72f, ceilingHeight * 0.98f),
                    velocity = new Vector3(NextFloat(-0.2f, 0.2f), NextFloat(-0.55f, -0.1f), NextFloat(-0.2f, 0.2f)),
                    startSize = NextFloat(minimumPebbleSize, maximumPebbleSize),
                    startLifetime = pebbleLifetime * NextFloat(0.7f, 1.1f),
                    rotation3D = new Vector3(NextFloat(0f, 360f), NextFloat(0f, 360f), NextFloat(0f, 360f))
                };
                pebbles.Emit(emit, 1);
            }
        }

        private float PathLength()
        {
            float result = 0f;
            for (int i = 1; i < floorPath.Length; i++) result += Vector3.Distance(floorPath[i - 1], floorPath[i]);
            return result;
        }

        private void SamplePath(float distance, out Vector3 position, out Vector3 tangent)
        {
            float remaining = Mathf.Max(0f, distance);
            for (int i = 1; i < floorPath.Length; i++)
            {
                Vector3 segment = floorPath[i] - floorPath[i - 1];
                float length = segment.magnitude;
                if (remaining <= length || i == floorPath.Length - 1)
                {
                    tangent = length > 0.0001f ? segment / length : Vector3.forward;
                    position = Vector3.Lerp(floorPath[i - 1], floorPath[i],
                        length > 0.0001f ? Mathf.Clamp01(remaining / length) : 0f);
                    return;
                }
                remaining -= length;
            }
            position = floorPath[0];
            tangent = Vector3.forward;
        }

        private float NextFloat(float minimum, float maximum)
        {
            return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        }

        private static Mesh BuildLowPolyRockMesh()
        {
            float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] baseVertices =
            {
                new Vector3(-1, phi, 0), new Vector3(1, phi, 0), new Vector3(-1, -phi, 0), new Vector3(1, -phi, 0),
                new Vector3(0, -1, phi), new Vector3(0, 1, phi), new Vector3(0, -1, -phi), new Vector3(0, 1, -phi),
                new Vector3(phi, 0, -1), new Vector3(phi, 0, 1), new Vector3(-phi, 0, -1), new Vector3(-phi, 0, 1)
            };
            int[] faces =
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
                1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
                4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };
            var vertices = new Vector3[faces.Length];
            var triangles = new int[faces.Length];
            for (int i = 0; i < faces.Length; i++)
            {
                Vector3 normalized = baseVertices[faces[i]].normalized;
                // A fixed asymmetric profile prevents the shared mesh from reading as a perfect sphere.
                float scale = 0.96f + Mathf.Abs(normalized.x * 0.025f + normalized.y * 0.015f - normalized.z * 0.02f);
                vertices[i] = Vector3.Scale(normalized * scale, new Vector3(1f, 0.96f, 0.98f));
                triangles[i] = i;
            }
            var mesh = new Mesh { vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDrawGizmosSelected()
        {
            if (floorPath == null || floorPath.Length < 2) return;
            Gizmos.color = State == RockfallState.Dormant
                ? new Color(0.8f, 0.65f, 0.2f, 0.65f) : new Color(0.85f, 0.2f, 0.1f, 0.75f);
            for (int i = 1; i < floorPath.Length; i++) Gizmos.DrawLine(floorPath[i - 1], floorPath[i]);
            for (int i = 0; i < floorPath.Length; i++) Gizmos.DrawWireSphere(floorPath[i], width * 0.5f);
        }

        private void OnDestroy()
        {
            if (boulderMesh != null)
            {
                if (Application.isPlaying) Destroy(boulderMesh); else DestroyImmediate(boulderMesh);
            }
            if (boulderPhysicsMaterial != null)
            {
                if (Application.isPlaying) Destroy(boulderPhysicsMaterial); else DestroyImmediate(boulderPhysicsMaterial);
            }
        }
    }
}
