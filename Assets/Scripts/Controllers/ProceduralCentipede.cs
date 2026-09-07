using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>A ground-dwelling stalker with a generated rig and distance-driven legs.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ProceduralCentipede : MonoBehaviour
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
        [Tooltip("Zero restores uniform behaviour. Each instance has independent smooth noise and pauses.")]
        [SerializeField, Range(0f, 1f)] private float behaviourVariation = 0.8f;
        [Tooltip("Zero chooses a unique runtime seed, including for duplicated prefabs. Nonzero seeds allow repeatable checks.")]
        [SerializeField] private int randomSeed;
        [SerializeField, Range(0f, 60f)] private float wanderAngle = 32f;
        [SerializeField, Range(0.05f, 2f)] private float noiseFrequency = 0.45f;
        [SerializeField, Range(0f, 0.5f)] private float speedVariation = 0.3f;
        [Tooltip("Average seconds of stalking between brief exploratory pauses. Flight never pauses.")]
        [SerializeField, Min(0.5f)] private float pauseInterval = 4f;
        [SerializeField, Range(0f, 2f)] private float pauseDuration = 0.65f;
        [Header("Fear of light")]
        [Tooltip("Approximate local light exposure that triggers flight. Moonlight is ignored.")]
        [SerializeField, Min(0.001f)] private float fearThreshold = 0.12f;
        [SerializeField, Range(0.1f, 0.9f)] private float safeLightRatio = 0.4f;
        [SerializeField, Min(0f)] private float hideDuration = 2.5f;
        [Tooltip("Colliders that block movement, ground probes and light. Triggers are ignored.")]
        [SerializeField] private LayerMask environmentMask = ~0;

        public enum BehaviourState { Stalking, Fleeing, Hiding }
        public BehaviourState State { get; private set; }
        public float Exposure { get; private set; }
        public int SegmentCount => segmentCount;
        public float Size => size;

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
        private float pauseRemaining, timeUntilPause, currentHideDuration;

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
            pauseRemaining = 0f;
            timeUntilPause = RandomRange(0.3f, 1.7f) * pauseInterval;
            currentHideDuration = hideDuration;
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
            }
            Exposure = 0f;
            // A torch reaching the tail must frighten the whole animal too.
            for (int i = 0; i < segments.Length; i++)
                Exposure = Mathf.Max(Exposure, SampleLight(segments[i].position));
            if (Exposure >= fearThreshold)
            {
                if (State != BehaviourState.Fleeing)
                {
                    pauseRemaining = 0f;
                    timeUntilPause = pauseInterval * RandomRange(0.5f, 1.5f);
                }
                State = BehaviourState.Fleeing;
                darkTime = 0f;
            }
            else if (State == BehaviourState.Fleeing && Exposure < fearThreshold * safeLightRatio)
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
                    delta.y = 0f;
                    away += delta.normalized * SampleSingleLight(light, transform.position + Vector3.up * 0.2f * size);
                }
                if (away.sqrMagnitude < 0.001f)
                    away = escapeDirection.sqrMagnitude > 0f ? escapeDirection : -transform.forward;
                desired = away.normalized;
                speed = fleeSpeed;
            }
            else if (State == BehaviourState.Stalking && target != null)
            {
                desired = target.position - transform.position;
                desired.y = 0f;
                if (desired.magnitude <= stopDistance * size) desired = Vector3.zero;
            }

            if (desired.sqrMagnitude > 0.001f)
            {
                bool fleeing = State == BehaviourState.Fleeing;
                // Small escape deviations keep the light gradient dominant; stalking can meander more.
                float angle = wanderAngle * behaviourVariation * BehaviourNoise(0f) * (fleeing ? 0.2f : 1f);
                if (!fleeing) angle *= Mathf.Clamp01(desired.magnitude / (2f * size));
                desired = Quaternion.Euler(0f, angle, 0f) * desired;
                float variation = behaviourVariation * speedVariation
                    * Mathf.Clamp(personalitySpeed + BehaviourNoise(37f), -1f, 1f);
                speed *= 1f + variation * (fleeing ? 0.35f : 1f);
                if (!fleeing && behaviourVariation > 0f)
                {
                    if (pauseRemaining > 0f)
                    {
                        pauseRemaining = Mathf.Max(0f, pauseRemaining - dt);
                        desired = Vector3.zero;
                    }
                    else
                    {
                        timeUntilPause -= dt;
                        if (timeUntilPause <= 0f)
                        {
                            pauseRemaining = pauseDuration * behaviourVariation * RandomRange(0.4f, 1.6f);
                            timeUntilPause = pauseInterval * RandomRange(0.5f, 1.8f);
                            if (pauseRemaining > 0f) desired = Vector3.zero;
                        }
                    }
                }
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
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(desired), turnSpeed * (State == BehaviourState.Fleeing ? 2f : 1f) * dt);
                    Vector3 step = transform.forward * (speed * dt);
                    if (CanMove(transform.position, step, out Vector3 ground)) transform.position = ground;
                }
            }
            // Keep the body in world space while the head advances.
            rig.SetPositionAndRotation(rigPosition, rigRotation);
            float distance = Vector3.Distance(before, transform.position);
            phase += distance / (0.55f * size) * Mathf.PI * 2f;
            FollowBody();
            PoseLegs();
        }

        private Vector3 ChooseDirection(Vector3 desired, float step)
        {
            Vector3 best = Vector3.zero;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < 16; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, i * 22.5f, 0f) * desired;
                if (!CanMove(transform.position, direction * Mathf.Max(step, 0.65f * size), out var ground)) continue;
                float score = Vector3.Dot(direction, desired) + 0.2f * Vector3.Dot(direction, transform.forward);
                if (State == BehaviourState.Fleeing)
                    score -= SampleLight(ground + direction * size + Vector3.up * 0.2f * size) * 4f;
                if (score > bestScore) { bestScore = score; best = direction; }
            }
            return best;
        }

        private bool CanMove(Vector3 from, Vector3 step, out Vector3 ground)
        {
            ground = from;
            float radius = 0.19f * size;
            if (step.sqrMagnitude < 0.000001f) return false;
            int count = Physics.SphereCastNonAlloc(from + Vector3.up * (radius + 0.08f * size), radius,
                step.normalized, hits, step.magnitude, environmentMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!hits[i].transform.IsChildOf(transform) && hits[i].normal.y < 0.65f) return false;
            if (!Physics.Raycast(from + step + Vector3.up * size, Vector3.down, out var hit,
                2f * size, environmentMask, QueryTriggerInteraction.Ignore)) return false;
            if (hit.normal.y < 0.7f || Mathf.Abs(hit.point.y - from.y) > 0.35f * size) return false;
            ground = hit.point;
            return true;
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
                for (int i = 0; i < count; i++)
                {
                    Transform blocker = hits[i].transform;
                    if (blocker.IsChildOf(transform) || light.transform.IsChildOf(blocker)) continue;
                    return 0f;
                }
            }
            float falloff = 1f - distance / light.range;
            return light.intensity * falloff * falloff;
        }

        private void FollowBody()
        {
            segments[0].position = transform.position + Vector3.up * (0.22f * size);
            segments[0].rotation = transform.rotation;
            for (int i = 1; i < segments.Length; i++)
            {
                Vector3 delta = segments[i - 1].position - segments[i].position;
                delta.y = 0f;
                if (delta.sqrMagnitude < 0.00001f) delta = segments[i - 1].forward;
                Vector3 position = segments[i - 1].position - delta.normalized * (0.3f * size);
                if (Physics.Raycast(position + Vector3.up * size, Vector3.down, out var hit,
                    size * 2f, environmentMask, QueryTriggerInteraction.Ignore)) position.y = hit.point.y + 0.22f * size;
                segments[i].SetPositionAndRotation(position, Quaternion.LookRotation(delta));
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
