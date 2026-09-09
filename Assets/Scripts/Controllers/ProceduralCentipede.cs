using UnityEngine;
using System.Collections.Generic;

namespace CardsUnity.Controllers
{
    /// <summary>A single leg planting itself on the surface, reported by <see cref="ProceduralCentipede"/>.</summary>
    public readonly struct CentipedeLegStep
    {
        public readonly int Segment;
        public readonly int Side;
        public readonly Vector3 Position;

        public CentipedeLegStep(int segment, int side, Vector3 position)
        {
            Segment = segment;
            Side = side;
            Position = position;
        }
    }

    /// <summary>A surface-crawling stalker with a generated rig and distance-driven legs.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed partial class ProceduralCentipede : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField, Range(3, 48)] private int segmentCount = 14;
        [Tooltip("Uniform size multiplier. Segment count independently controls body length.")]
        [SerializeField, Range(0.25f, 3f)] private float size = 1f;
        [SerializeField] private Material shellMaterial;
        [SerializeField] private Material legMaterial;
        [Header("Hunting")]
        [SerializeField] private Transform target;
        [SerializeField, Min(0.1f)] private float stalkSpeed = 1.2f;
        [SerializeField, Min(0.1f)] private float fleeSpeed = 4.5f;
        [SerializeField, Min(0.1f)] private float stopDistance = 1.2f;
        [SerializeField, Min(1f)] private float turnSpeed = 300f;
        [Header("Natural variation")]
        [Tooltip("Zero restores uniform behaviour. Each instance has independent smooth steering and speed noise.")]
        [SerializeField, Range(0f, 1f)] private float behaviourVariation = 0.8f;
        [Tooltip("Zero chooses a unique runtime seed, including for duplicated prefabs. Nonzero seeds allow repeatable checks.")]
        [SerializeField] private int randomSeed;
        [SerializeField, Range(0f, 60f)] private float wanderAngle = 32f;
        [SerializeField, Range(0.05f, 2f)] private float noiseFrequency = 0.45f;
        [SerializeField, Range(0f, 0.5f)] private float speedVariation = 0.3f;
        [Header("Movement recovery")]
        [SerializeField, Min(0.2f)] private float stuckCheckInterval = 0.7f;
        [SerializeField, Min(0.2f)] private float recoveryDuration = 1.5f;
        [Header("Fear of light")]
        [Tooltip("Respond to the faint outer torch light, before the bright centre. Gameplay exposure, not a rendered pixel tone.")]
        [SerializeField, Min(0.0001f)] private float dimLightThreshold = 0.005f;
        [Tooltip("Approximate local light exposure that triggers flight. Moonlight is ignored.")]
        [SerializeField, Min(0.001f)] private float fearThreshold = 0.12f;
        [SerializeField, Range(0.1f, 0.9f)] private float safeLightRatio = 0.4f;
        [SerializeField, Min(0f)] private float hideDuration = 2.5f;
        [Tooltip("Colliders that block movement, ground probes and light. Triggers are ignored.")]
        [SerializeField] private LayerMask environmentMask = ~0;

        public enum BehaviourState { Stalking, Fleeing, Hiding, Dormant, Enraged, WindingUp, Leaping }
        public BehaviourState State { get; private set; }
        public float Exposure { get; private set; }
        public void SetTarget(Transform value) => target = value;
        private ProceduralCave homeCave;
        private int homeRoom = -1;
        private bool alerted;
        public bool IsAlerted => homeCave == null || alerted;
        public void SetCaveHome(ProceduralCave cave, int room)
        {
            homeCave = cave;
            homeRoom = room;
            alerted = false;
            State = BehaviourState.Dormant;
        }
        public int SegmentCount => segmentCount;
        public float Size => size;
        /// <summary>The hunted transform, resolved on the first tick when left empty.</summary>
        public Transform Target => target;

        /// <summary>Raised for every individual leg contact, driven by travelled distance.</summary>
        public event System.Action<CentipedeLegStep> LegStep;

        // Contact poses are recorded along the head path so the tail takes the same corners.
        private readonly List<Pose> surfaceTrail = new List<Pose>(1024);
        private readonly Dictionary<MeshCollider, CrawlSurfaceMesh> visibleSurfaces = new Dictionary<MeshCollider, CrawlSurfaceMesh>();
        private Transform rig;
        private Transform[] segments, upperLegs, lowerLegs;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private Light[] lights;
        private float lightRefresh, darkTime, phase;
        private int builtCount;
        private float builtSize;
        private Material builtShell, builtLeg;
        private Vector3 escapeDirection;
        private System.Random behaviourRandom;
        private int initializedSeed;
        private float behaviourTime, noiseOffset, tempo, personalitySpeed, gaitOffset;
        private float currentHideDuration;
        private Vector3 progressOrigin, recoveryDirection;
        private float progressTime, recoveryRemaining;
        private int recoveryAttempts;
        public bool IsRecovering => recoveryRemaining > 0f;

        private float RandomRange(float min, float max) => Mathf.Lerp(min, max, (float)behaviourRandom.NextDouble());

        private void InitializeBehaviour()
        {
            // Do not touch Unity's shared Random state: other creatures and gameplay remain independent.
            initializedSeed = randomSeed;
            behaviourRandom = new System.Random(randomSeed != 0 ? randomSeed : GetInstanceID());
            noiseOffset = RandomRange(10f, 9000f);
            tempo = RandomRange(0.7f, 1.35f);
            personalitySpeed = RandomRange(-0.5f, 0.5f);
            gaitOffset = RandomRange(0f, Mathf.PI * 2f);
            behaviourTime = 0f;
            currentHideDuration = hideDuration;
            progressOrigin = transform.position;
            progressTime = 0f;
            recoveryRemaining = 0f;
        }

        private float BehaviourNoise(float channel)
        {
            return Mathf.Clamp(Mathf.PerlinNoise(noiseOffset + channel,
                behaviourTime * noiseFrequency * tempo) * 2f - 1f, -1f, 1f);
        }

        private void OnValidate()
        {
            segmentCount = Mathf.Clamp(segmentCount, 3, 48);
            size = Mathf.Clamp(size, 0.25f, 3f);
            stalkSpeed = Mathf.Max(0.1f, stalkSpeed);
            fleeSpeed = Mathf.Max(stalkSpeed, fleeSpeed);
            fearThreshold = Mathf.Max(0.001f, fearThreshold);
        }

        private void OnEnable()
        {
            lightRefresh = 0f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += UpdateEditorPreview;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= UpdateEditorPreview;
#endif
            ResetAttack();
            ClearRig();
        }

#if UNITY_EDITOR
        private void UpdateEditorPreview()
        {
            if (this != null && !Application.isPlaying && !UnityEditor.EditorApplication.isCompiling
                && !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode
                && !UnityEditor.EditorUtility.IsPersistent(this)) Update();
        }
#endif
        private void OnDestroy() { ClearRig(); }

        private void Update()
        {
            if (rig == null || builtCount != segmentCount || !Mathf.Approximately(builtSize, size)
                || builtShell != shellMaterial || builtLeg != legMaterial) Rebuild();
            if (Application.isPlaying) Tick(Time.deltaTime);
            else PoseLegs();
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (rig == null) Rebuild();
            if (behaviourRandom == null || initializedSeed != randomSeed) InitializeBehaviour();
            behaviourTime += dt;
            if (target == null)
            {
                var player = FindFirstObjectByType<ProceduralCharacter>();
                if (player != null) target = player.transform;
            }
            lightRefresh -= dt;
            if (lights == null || lightRefresh <= 0f)
            {
                lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                lightRefresh = 1f;
                RefreshVisibleSurfaces();
            }
            Exposure = 0f;
            for (int i = 0; i < segments.Length; i++)
                Exposure = Mathf.Max(Exposure, SampleLight(segments[i].position));
            float reactionThreshold = Mathf.Min(fearThreshold, dimLightThreshold);
            if (homeCave != null && !alerted && (Exposure >= reactionThreshold
                || (target != null && homeCave.IsInRoom(target.position, homeRoom))))
                alerted = true;
            if (homeCave != null && !alerted)
            {
                State = BehaviourState.Dormant;
                return;
            }
            if (State == BehaviourState.Dormant) State = BehaviourState.Stalking;
            float headExposure = SampleLight(transform.position + transform.up * 0.22f * size);
            if (TickAttack(dt, headExposure, reactionThreshold)) return;
            if (State != BehaviourState.Enraged && headExposure >= reactionThreshold)
            {
                State = BehaviourState.Fleeing;
                darkTime = 0f;
            }
            else if (State == BehaviourState.Fleeing && headExposure < reactionThreshold * safeLightRatio)
            {
                State = BehaviourState.Hiding;
                currentHideDuration = hideDuration * (1f + behaviourVariation * RandomRange(-0.45f, 0.65f));
                darkTime = 0f;
            }
            else if (State == BehaviourState.Hiding)
            {
                darkTime += dt;
                if (darkTime >= currentHideDuration) State = BehaviourState.Stalking;
            }

            Vector3 desired = Vector3.zero;
            float speed = stalkSpeed;
            if (State == BehaviourState.Fleeing)
            {
                Vector3 away = Vector3.zero;
                foreach (var light in lights)
                {
                    if (!IsThreat(light)) continue;
                    Vector3 delta = transform.position - light.transform.position;
                    delta = Vector3.ProjectOnPlane(delta, transform.up);
                    away += delta.normalized * SampleSingleLight(light, transform.position + transform.up * 0.2f * size);
                }
                if (away.sqrMagnitude < 0.001f)
                    away = Vector3.ProjectOnPlane(escapeDirection, transform.up);
                if (away.sqrMagnitude < 0.001f) away = -transform.forward;
                desired = away.normalized;
                speed = fleeSpeed;
            }
            else if (target != null)
            {
                desired = State == BehaviourState.Enraged
                    ? Vector3.ProjectOnPlane(target.position - transform.position, transform.up).normalized
                    : ShadowPursuitDirection(reactionThreshold * safeLightRatio);
                if (State == BehaviourState.Enraged) speed = fleeSpeed;
            }

            bool descending = homeCave != null && State != BehaviourState.Fleeing && transform.up.y < 0.65f;
            if (descending) desired = Vector3.ProjectOnPlane(Vector3.down, transform.up).normalized;
            if (desired.sqrMagnitude > 0.001f)
            {
                bool fleeing = State == BehaviourState.Fleeing;
                // Small escape deviations keep the light gradient dominant; stalking can meander more.
                float angle = wanderAngle * behaviourVariation * BehaviourNoise(0f) * (fleeing ? 0.2f : 1f);
                if (!fleeing) angle *= Mathf.Clamp01(desired.magnitude / (2f * size));
                desired = Quaternion.AngleAxis(angle, transform.up) * desired;
                float variation = behaviourVariation * speedVariation
                    * Mathf.Clamp(personalitySpeed + BehaviourNoise(37f), -1f, 1f);
                speed *= 1f + variation * (fleeing ? 0.35f : 1f);
            }
            // Alerted creatures keep patrolling even while cooling down in darkness.
            if (desired.sqrMagnitude < 0.001f) desired = transform.forward;
            UpdateRecovery(dt);
            if (recoveryRemaining > 0f)
            {
                desired = Vector3.ProjectOnPlane(recoveryDirection, transform.up).normalized;
                if (desired.sqrMagnitude < 0.001f) desired = -transform.forward;
            }

            Vector3 rigPosition = rig.position;
            Quaternion rigRotation = rig.rotation;
            Vector3 before = transform.position;
            if (desired.sqrMagnitude > 0.001f)
            {
                desired = ChooseDirection(desired.normalized, speed * dt);
                if (desired.sqrMagnitude > 0f)
                {
                    if (State == BehaviourState.Fleeing) escapeDirection = desired;
                    Vector3 up = transform.up;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(desired, up), turnSpeed * (State == BehaviourState.Fleeing || IsRecovering ? 2f : 1f) * dt);
                    // Bound probe distance even during a long frame; never bridge a gap in one step.
                    int steps = Mathf.Max(1, Mathf.CeilToInt(speed * dt / (0.06f * size)));
                    for (int j = 0; j < steps; j++)
                    {
                        if (!TrySurfaceStep(transform.position, transform.rotation, speed * dt / steps, out Pose next)) break;
                        // Turn along the shadow edge before the next step enters frightening light.
                        if (State != BehaviourState.Fleeing && State != BehaviourState.Enraged && SampleLight(next.position
                            + next.rotation * Vector3.up * 0.22f * size) > Mathf.Max(Exposure, Mathf.Min(fearThreshold, dimLightThreshold) * safeLightRatio))
                            break;
                        transform.SetPositionAndRotation(next.position, next.rotation);
                        RecordContact();
                    }
                }
            }
            // Keep the body in world space while the head advances.
            rig.SetPositionAndRotation(rigPosition, rigRotation);
            float distance = Vector3.Distance(before, transform.position);
            float previousPhase = phase;
            phase += distance / (0.55f * size) * Mathf.PI * 2f;
            ReportLegSteps(previousPhase);
            RecordContact();
            FollowBody();
            PoseLegs();
        }

        private Vector3 ShadowPursuitDirection(float safeExposure)
        {
            Vector3 up = transform.up;
            Vector3 outward = Vector3.ProjectOnPlane(transform.position - target.position, up);
            float distance = outward.magnitude;
            outward = distance > 0.001f ? outward / distance : transform.forward;
            // Search from the prey outward for the nearest dark point on this radial line.
            // Recompute against the moving torch, including collider shadows.
            Vector3 centre = transform.position - outward * distance + up * 0.22f * size;
            float limit = stopDistance * size + 2f;
            foreach (var light in lights)
                if (IsThreat(light) && Vector3.Distance(centre, light.transform.position) < light.range + stopDistance * size)
                    limit = Mathf.Max(limit, Vector3.Distance(centre, light.transform.position) + light.range);
            float radius = stopDistance * size;
            float spacing = Mathf.Max(0.2f, (limit - radius) / 48f);
            for (; radius < limit; radius += spacing)
                if (SampleLight(centre + outward * radius) < safeExposure * 0.75f) break;
            radius += 0.2f * size;
            float error = distance - radius;
            float side = personalitySpeed < 0f ? -1f : 1f;
            Vector3 tangent = Vector3.Cross(up, outward) * side;
            // Approach decisively from far away, then orbit instead of running into the light.
            return (-outward * Mathf.Clamp(error / Mathf.Max(size, 0.5f), -1.5f, 1.5f)
                + tangent * Mathf.Clamp01(1.5f - Mathf.Abs(error) / Mathf.Max(size, 0.5f))).normalized;
        }

        private void UpdateRecovery(float dt)
        {
            recoveryRemaining = Mathf.Max(0f, recoveryRemaining - dt);
            progressTime += dt;
            if (progressTime < stuckCheckInterval) return;
            float progress = Vector3.Distance(progressOrigin, transform.position);
            progressTime = 0f;
            progressOrigin = transform.position;
            if (progress > 0.16f * size)
            {
                if (recoveryRemaining <= 0f) recoveryAttempts = 0;
                return;
            }
            recoveryAttempts++;
            // Commit to an alternate heading long enough to get around a rock/corner.
            float side = recoveryAttempts % 2 == 0 ? -1f : 1f;
            recoveryDirection = Quaternion.AngleAxis(side * 110f, transform.up) * transform.forward;
            if (recoveryAttempts >= 2 && surfaceTrail.Count > 2)
            {
                Vector3 previous = surfaceTrail[Mathf.Min(surfaceTrail.Count - 1, 20)].position;
                Vector3 back = previous - transform.position;
                if (back.sqrMagnitude > 0.001f) recoveryDirection = back.normalized;
            }
            recoveryRemaining = recoveryDuration;
        }

        private Vector3 ChooseDirection(Vector3 desired, float step)
        {
            Vector3 best = Vector3.zero;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < 16; i++)
            {
                Vector3 direction = Quaternion.AngleAxis(i * 22.5f, transform.up) * desired;
                if (!TrySurfaceStep(transform.position, Quaternion.LookRotation(direction, transform.up),
                    Mathf.Min(step, 0.06f * size), out var next)) continue;
                // Look far enough ahead to steer around walls instead of repeatedly facing a rejected step.
                Pose ahead = next;
                bool clear = true;
                for (int j = 0; j < 5; j++)
                {
                    if (!TrySurfaceStep(ahead.position, ahead.rotation, 0.06f * size, out var probe)) { clear = false; break; }
                    ahead = probe;
                }
                float score = Vector3.Dot(direction, desired) + 0.2f * Vector3.Dot(direction, transform.forward);
                if (!clear) score -= 3f;
                if (homeCave != null) score += Mathf.Clamp01((ahead.rotation * Vector3.up).y) * 1.5f;
                float aheadLight = SampleLight(ahead.position + ahead.rotation * Vector3.up * 0.22f * size);
                if (State == BehaviourState.Fleeing)
                    score -= aheadLight * 4f;
                else if (State != BehaviourState.Enraged)
                {
                    // Normalize against the same faint-light threshold used for fear: raw exposure
                    // is too small to compete with heading scores near the torch's outer edge.
                    float safeExposure = Mathf.Max(0.00001f, Mathf.Min(fearThreshold, dimLightThreshold) * safeLightRatio);
                    score -= Mathf.Clamp(aheadLight / safeExposure, 0f, 4f) * 4f;
                }
                if (score > bestScore) { bestScore = score; best = direction; }
            }
            return best;
        }

        private bool SurfaceRay(Vector3 origin, Vector3 direction, float length, out RaycastHit nearest)
        {
            nearest = default;
            int count = Physics.RaycastNonAlloc(origin, direction, hits, length, environmentMask, QueryTriggerInteraction.Ignore);
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                // Characters are obstacles/targets, not climbable terrain.
                if (hit.transform.IsChildOf(transform) || hit.collider is CharacterController
                    || hit.transform.GetComponentInParent<ProceduralCentipede>() != null) continue;
                if (hit.collider is MeshCollider meshCollider && visibleSurfaces.ContainsKey(meshCollider)) continue;
                if (hit.distance >= closest) continue;
                closest = hit.distance;
                nearest = hit;
            }
            foreach (var surface in visibleSurfaces.Values)
            {
                if (surface.Collider == null || (environmentMask.value & (1 << surface.Collider.gameObject.layer)) == 0) continue;
                if (!surface.Raycast(origin, direction, Mathf.Min(length, closest), out var hit)) continue;
                closest = hit.distance;
                nearest = hit;
            }
            return closest < float.PositiveInfinity;
        }

        private void RefreshVisibleSurfaces()
        {
            // Cave cutaways deliberately keep taller collision walls for the player.
            // Crawl on the rendered mesh instead, without adding colliders or altering physics layers.
            var updated = new Dictionary<MeshCollider, CrawlSurfaceMesh>();
            foreach (var cave in FindObjectsByType<ProceduralCave>(FindObjectsSortMode.None))
            {
                if (cave.gameObject.scene != gameObject.scene) continue;
                if (homeCave == null && cave.RoomCenters.Count > 0)
                {
                    Vector3 local = cave.transform.InverseTransformPoint(transform.position);
                    int nearestRoom = 0;
                    float nearestDistance = float.PositiveInfinity;
                    for (int i = 0; i < cave.RoomCenters.Count; i++)
                    {
                        float distance = Vector2.SqrMagnitude(cave.RoomCenters[i] - new Vector2(local.x, local.z));
                        if (distance >= nearestDistance) continue;
                        nearestRoom = i;
                        nearestDistance = distance;
                    }
                    SetCaveHome(cave, nearestRoom);
                }
                foreach (var collider in cave.GetComponentsInChildren<MeshCollider>())
                {
                    var filter = collider.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null || filter.sharedMesh == collider.sharedMesh) continue;
                    if (!visibleSurfaces.TryGetValue(collider, out var surface) || surface.Mesh != filter.sharedMesh)
                        surface = new CrawlSurfaceMesh(collider, filter.sharedMesh);
                    updated.Add(collider, surface);
                }
            }
            visibleSurfaces.Clear();
            foreach (var pair in updated) visibleSurfaces.Add(pair.Key, pair.Value);
        }

        private bool TrySurfaceStep(Vector3 from, Quaternion rotation, float distance, out Pose pose)
        {
            pose = new Pose(from, rotation);
            Vector3 up = rotation * Vector3.up, forward = rotation * Vector3.forward;
            float skin = 0.025f * size;
            Vector3 destination = from + forward * distance;
            RaycastHit hit;
            // Concave junction: transport the heading onto the wall in front.
            if (SurfaceRay(from + up * skin, forward, distance + skin, out hit)
                && Vector3.Dot(hit.normal, forward) < -0.1f)
            {
                Vector3 heading = Quaternion.FromToRotation(up, hit.normal) * forward;
                pose = new Pose(hit.point, Quaternion.LookRotation(heading, hit.normal));
                return AllowedSurface(pose);
            }
            // Follow the current plane, including slopes and curved mesh triangles.
            if (SurfaceRay(destination + up * (0.12f * size), -up, 0.24f * size, out hit))
            {
                Vector3 heading = Vector3.ProjectOnPlane(forward, hit.normal).normalized;
                if (heading.sqrMagnitude < 0.001f) return false;
                pose = new Pose(hit.point, Quaternion.LookRotation(heading, hit.normal));
                return AllowedSurface(pose);
            }
            // Convex edge: look back underneath the lip for the adjoining surface.
            if (SurfaceRay(destination - up * skin + forward * skin, -forward, distance + skin * 3f, out hit))
            {
                Vector3 heading = Quaternion.FromToRotation(up, hit.normal) * forward;
                pose = new Pose(hit.point, Quaternion.LookRotation(heading, hit.normal));
                return AllowedSurface(pose);
            }
            return false;
        }

        private bool AllowedSurface(Pose pose)
        {
            if (homeCave == null) return true;
            Vector3 normal = pose.rotation * Vector3.up;
            if (!homeCave.IsInteriorCrawlSurface(pose.position, normal, out bool boundary)) return false;
            // A creature already on an inner wall may descend after the danger passes.
            return !boundary || normal.y > 0.65f || State == BehaviourState.Fleeing || transform.up.y < 0.65f;
        }

        private void RecordContact()
        {
            var pose = new Pose(transform.position, transform.rotation);
            if (surfaceTrail.Count > 0 && Vector3.Distance(surfaceTrail[0].position, pose.position) < 0.01f * size)
                surfaceTrail[0] = pose;
            else surfaceTrail.Insert(0, pose);
            float length = 0f;
            for (int i = 1; i < surfaceTrail.Count; i++)
            {
                length += Vector3.Distance(surfaceTrail[i - 1].position, surfaceTrail[i].position);
                if (length <= (segmentCount + 1) * 0.3f * size) continue;
                if (i + 1 < surfaceTrail.Count) surfaceTrail.RemoveRange(i + 1, surfaceTrail.Count - i - 1);
                break;
            }
        }

        private static bool IsThreat(Light light) => light != null && light.isActiveAndEnabled
            && light.intensity > 0f && light.range > 0f && (light.type == LightType.Point || light.type == LightType.Spot);

        public float SampleLight(Vector3 point)
        {
            float exposure = 0f;
            if (lights == null) lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var light in lights) exposure += SampleSingleLight(light, point);
            return exposure;
        }

        private float SampleSingleLight(Light light, Vector3 point)
        {
            if (!IsThreat(light) || (light.cullingMask & (1 << gameObject.layer)) == 0) return 0f;
            Vector3 delta = light.transform.position - point;
            float distance = delta.magnitude;
            if (distance >= light.range) return 0f;
            if (light.type == LightType.Spot && Vector3.Angle(light.transform.forward, -delta) > light.spotAngle * 0.5f) return 0f;
            if (light.shadows != LightShadows.None && distance > 0.001f)
            {
                int count = Physics.RaycastNonAlloc(point, delta / distance, hits, distance,
                    environmentMask, QueryTriggerInteraction.Ignore);
                var torch = light.GetComponentInParent<HandheldTorch>();
                Transform emitter = torch != null ? torch.transform : light.transform;
                for (int i = 0; i < count; i++)
                {
                    Transform blocker = hits[i].transform;
                    // Ignore the emitter itself, not its holder: the character casts a gameplay shadow.
                    if (blocker.IsChildOf(transform) || blocker.IsChildOf(emitter)) continue;
                    return 0f;
                }
            }
            float falloff = 1f - distance / light.range;
            return light.intensity * falloff * falloff;
        }

        private void FollowBody()
        {
            if (surfaceTrail.Count == 0) RecordContact();
            int cursor = 0;
            float travelled = 0f;
            for (int i = 0; i < segments.Length; i++)
            {
                float wanted = i * 0.3f * size;
                while (cursor + 1 < surfaceTrail.Count)
                {
                    float length = Vector3.Distance(surfaceTrail[cursor].position, surfaceTrail[cursor + 1].position);
                    if (travelled + length >= wanted) break;
                    travelled += length;
                    cursor++;
                }
                Pose a = surfaceTrail[cursor], b = surfaceTrail[Mathf.Min(cursor + 1, surfaceTrail.Count - 1)];
                float span = Vector3.Distance(a.position, b.position);
                float t = span > 0.00001f ? Mathf.Clamp01((wanted - travelled) / span) : 0f;
                Quaternion orientation = Quaternion.Slerp(a.rotation, b.rotation, t);
                Vector3 contact = Vector3.Lerp(a.position, b.position, t);
                segments[i].SetPositionAndRotation(contact + orientation * Vector3.up * (0.22f * size), orientation);
            }
        }

        // A leg stays planted while sin(cycle) is negative, so its touchdown is the moment its own
        // cycle crosses pi. Every leg shares the distance-driven phase and differs by a fixed offset,
        // so counting the crossings needs no per-leg state.
        private void ReportLegSteps(float previousPhase)
        {
            if (LegStep == null || lowerLegs == null) return;
            const float turn = Mathf.PI * 2f;
            for (int i = 0; i < segmentCount; i++)
                for (int side = 0; side < 2; side++)
                {
                    float offset = gaitOffset * behaviourVariation - i * 0.75f + side * Mathf.PI - Mathf.PI;
                    if (Mathf.FloorToInt((previousPhase + offset) / turn)
                        == Mathf.FloorToInt((phase + offset) / turn)) continue;
                    int index = i * 2 + side;
                    Transform leg = index < lowerLegs.Length ? lowerLegs[index] : null;
                    LegStep.Invoke(new CentipedeLegStep(i, side,
                        leg != null ? leg.position : transform.position));
                }
        }

        private void PoseLegs()
        {
            for (int i = 0; i < segmentCount; i++)
                for (int side = 0; side < 2; side++)
                {
                    float sign = side == 0 ? -1f : 1f;
                    float cycle = phase + gaitOffset * behaviourVariation - i * 0.75f + side * Mathf.PI;
                    Vector3 hip = new Vector3(sign * 0.15f, -0.01f, 0f);
                    Vector3 knee = new Vector3(sign * 0.34f, 0.02f + Mathf.Max(0f, Mathf.Sin(cycle)) * 0.07f, Mathf.Cos(cycle) * 0.10f);
                    Vector3 foot = new Vector3(sign * 0.48f, -0.21f + Mathf.Max(0f, Mathf.Sin(cycle)) * 0.14f, Mathf.Cos(cycle) * 0.17f - 0.10f);
                    SetRod(upperLegs[i * 2 + side], hip * size, knee * size, 0.045f * size);
                    SetRod(lowerLegs[i * 2 + side], knee * size, foot * size, 0.028f * size);
                }
        }

        private void Rebuild()
        {
            ClearRig();
            surfaceTrail.Clear();
            RefreshVisibleSurfaces();
            builtCount = segmentCount; builtSize = size; builtShell = shellMaterial; builtLeg = legMaterial;
            rig = new GameObject("Procedural Centipede Rig").transform;
            rig.SetParent(transform, false);
            rig.gameObject.hideFlags = HideFlags.DontSave;
            segments = new Transform[segmentCount];
            upperLegs = new Transform[segmentCount * 2]; lowerLegs = new Transform[segmentCount * 2];
            for (int i = 0; i < segmentCount; i++)
            {
                var segment = new GameObject($"Segment {i + 1:00}").transform;
                segment.gameObject.hideFlags = HideFlags.DontSave;
                segment.SetParent(rig, false);
                segment.localPosition = new Vector3(0f, 0.22f * size, -i * 0.3f * size);
                segments[i] = segment;
                Vector3 contact = transform.position - transform.forward * (i * 0.3f * size);
                Quaternion orientation = transform.rotation;
                if (SurfaceRay(contact + transform.up * size, -transform.up, 2f * size, out var initialHit))
                {
                    contact = initialHit.point;
                    orientation = Quaternion.FromToRotation(transform.up, initialHit.normal) * orientation;
                }
                surfaceTrail.Add(new Pose(contact, orientation));
                float taper = Mathf.Lerp(1f, 0.55f, Mathf.Pow((float)i / (segmentCount - 1), 3f));
                var shell = Part("Armor", segment, PrimitiveType.Sphere, shellMaterial);
                shell.localScale = new Vector3(0.38f * taper, 0.23f * taper, i == 0 ? 0.42f : 0.34f) * size;
                var ridge = Part("Dorsal plate", segment, PrimitiveType.Cube, shellMaterial);
                ridge.localPosition = new Vector3(0f, 0.09f, -0.02f) * size;
                ridge.localScale = new Vector3(0.24f * taper, 0.055f, 0.23f) * size;
                for (int side = 0; side < 2; side++)
                {
                    int index = i * 2 + side;
                    upperLegs[index] = Part("Leg upper", segment, PrimitiveType.Cylinder, legMaterial);
                    lowerLegs[index] = Part("Leg tip", segment, PrimitiveType.Cylinder, legMaterial);
                    if (i != 0) continue;
                    float sign = side == 0 ? -1f : 1f;
                    var antenna = Part("Antenna", segment, PrimitiveType.Cylinder, legMaterial);
                    SetRod(antenna, new Vector3(sign * 0.1f, 0.03f, 0.12f) * size,
                        new Vector3(sign * 0.25f, 0.1f, 0.65f) * size, 0.022f * size);
                    var fang = Part("Forcipule", segment, PrimitiveType.Cylinder, legMaterial);
                    SetRod(fang, new Vector3(sign * 0.14f, -0.06f, 0.12f) * size,
                        new Vector3(sign * 0.07f, -0.1f, 0.34f) * size, 0.045f * size);
                }
            }
            PoseLegs();
        }

        private static Transform Part(string name, Transform parent, PrimitiveType type, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.hideFlags = HideFlags.DontSave;
            part.transform.SetParent(parent, false);
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        private static void SetRod(Transform rod, Vector3 start, Vector3 end, float width)
        {
            rod.localPosition = (start + end) * 0.5f;
            rod.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
            rod.localScale = new Vector3(width, (end - start).magnitude * 0.5f, width);
        }

        private void ClearRig()
        {
            if (rig == null) return;
            if (Application.isPlaying) Destroy(rig.gameObject); else DestroyImmediate(rig.gameObject);
            rig = null;
        }
    }
}
