using UnityEngine;

namespace CardsUnity.Controllers
{
    // A single foot contact reported by the gait. Amounts are blended style weights, not raw input.
    public readonly struct GaitFootstep
    {
        public readonly bool LeftFoot;
        public readonly float Intensity;
        public readonly float SneakAmount;
        public readonly float RunAmount;
        public readonly Vector3 Position;

        public GaitFootstep(bool leftFoot, float intensity, float sneakAmount, float runAmount, Vector3 position)
        {
            LeftFoot = leftFoot;
            Intensity = intensity;
            SneakAmount = sneakAmount;
            RunAmount = runAmount;
            Position = position;
        }
    }

    public sealed class CartoonCharacterGait : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftThigh, rightThigh;
        [SerializeField] private Transform leftCalf, rightCalf;
        [SerializeField] private Transform leftFoot, rightFoot;
        [SerializeField] private Transform leftArm, rightArm;
        [SerializeField] private Transform leftForearm, rightForearm;
        [SerializeField] private float thighLength = 0.44f;
        [SerializeField] private float calfLength = 0.44f;
        [SerializeField] private float hipHeight = 0.88f;
        [SerializeField] private float hipHalfWidth = 0.19f;
        [SerializeField] private float armLag = 0.10f;

        [Header("Cartoon body motion")]
        [Tooltip("Maximum rhythmic side lean in degrees at running speed. Zero disables rhythmic leaning.")]
        [SerializeField, Range(0f, 35f)] private float sideLeanAngle = 18f;
        [Tooltip("Sideways torso travel in metres at running speed.")]
        [SerializeField, Range(0f, 0.15f)] private float sideSwayDistance = 0.055f;
        [Tooltip("Peak-to-peak vertical body travel in metres while walking.")]
        [SerializeField, Range(0f, 0.3f)] private float walkBounceHeight = 0.12f;
        [Tooltip("Peak-to-peak vertical body travel in metres while running.")]
        [SerializeField, Range(0f, 0.4f)] private float runBounceHeight = 0.24f;
        [Tooltip("Seconds of body motion smoothing. Lower values keep a stronger bounce.")]
        [SerializeField, Range(0.015f, 0.15f)] private float bodyMotionSmoothTime = 0.045f;
        [Tooltip("Extra side lean from changes of direction, in degrees per metre per second squared.")]
        [SerializeField, Range(0f, 2f)] private float turnLeanStrength = 0.9f;

        [Header("Posture and steps")]
        [Tooltip("Forward torso lean at full running speed, in degrees. The head stays level.")]
        [SerializeField, Range(0f, 45f)] private float forwardLeanAngle = 13f;
        [Tooltip("Distance travelled per individual walking step, in metres. Also controls cadence.")]
        [SerializeField, Range(0.2f, 1f)] private float walkStepLength = 0.7f;
        [Tooltip("Distance travelled per individual running step, in metres. Also controls cadence.")]
        [SerializeField, Range(0.3f, 1.3f)] private float runStepLength = 0.95f;
        [Tooltip("Visual airborne lift of the whole character during a running step, in metres. Does not jump the collider.")]
        [SerializeField, Range(0f, 0.5f)] private float runHopHeight = 0.12f;
        [Tooltip("Forward elevation of the upper arms, in degrees.")]
        [SerializeField, Range(0f, 70f)] private float armRaiseAngle = 8f;
        [Tooltip("Additional outward elevation of the upper arms, in degrees.")]
        [SerializeField, Range(0f, 90f)] private float armOutwardAngle = 8f;

        [Header("Independent forearm controls")]
        [Tooltip("Base elbow bend relative to the upper arm, in degrees.")]
        [SerializeField, Range(0f, 135f)] private float forearmRaiseAngle = 15f;
        [Tooltip("Additional elbow bend at full speed. Zero keeps the selected base bend.")]
        [SerializeField, Range(0f, 90f)] private float forearmSpeedBend = 60f;
        [Tooltip("Additional elbow articulation during an arm swing.")]
        [SerializeField, Range(0f, 1f)] private float forearmSwingBend = 0.28f;

        [Header("Style mixer - normalized weights")]
        [Tooltip("Set one weight to 1 and the others to 0 for a pure style. Multiple weights blend. All zero uses Walk.")]
        [SerializeField, Range(0f, 1f)] private float walk = 1f;
        [SerializeField, Range(0f, 1f)] private float doubleBounceWalk;
        [SerializeField, Range(0f, 1f)] private float strut;
        [SerializeField, Range(0f, 1f)] private float shuffle;
        [SerializeField, Range(0f, 1f)] private float sneak;
        [SerializeField, Range(0f, 1f)] private float run;
        [SerializeField, Range(0f, 1f)] private float jump;
        [SerializeField, Range(0f, 1f)] private float fastRun;
        [SerializeField, Range(0f, 1f)] private float tipToe;
        [SerializeField, Range(0f, 1f)] private float skip;
        [Tooltip("Exhausted knee walking; posture remains low while stationary.")]
        [SerializeField, Range(0f, 1f)] private float kneeWalk;
        [Tooltip("Prone crawling with alternating pulls; leaves the torch arm available.")]
        [SerializeField, Range(0f, 1f)] private float crawl;
        [SerializeField, Range(0.05f, 1f)] private float styleTransitionTime = 0.2f;

        [Header("Movement irregularity")]
        [Tooltip("Strength of smooth motion variation. Zero restores a regular gait.")]
        [SerializeField, Range(0f, 1f)] private float noiseAmount = 0.2f;
        [Tooltip("Speed of the continuous noise, in cycles per second.")]
        [SerializeField, Range(0.1f, 4f)] private float noiseFrequency = 1.3f;
        [SerializeField, Range(0f, 100f)] private float noiseSeed = 17f;

        private ProceduralSpine spine;
        private CharacterController characterController;
        private bool terrainGrounded;
        private readonly RaycastHit[] terrainHits = new RaycastHit[32];
        private float cycle, intensity, intensityVelocity;
        private Vector3 lean, leanVelocity;
        public float InteractionCrouch { get; set; }
        public float KnockdownWeight { get; set; }
        private readonly float[] locomotionWeights = new float[12];
        private Vector3 bodyOrigin;
        private Vector3 bodyMotion, bodyMotionVelocity;
        private float sideRoll, sideRollVelocity;
        private float leftSwing, rightSwing, leftSwingVelocity, rightSwingVelocity;
        private float noiseTime, flightLift, flightVelocity;
        private float leftArmNoise, rightArmNoise;
        private readonly float[] styleWeights = new float[12];
        private readonly float[] savedWalkingWeights = new float[12];
        private bool runningRequested, sneakingRequested, walkingRequested, automaticStyleActive;
        private GaitStylePose style;
        private float poseActivity;
        public float LowPostureWeight => styleWeights[10] + styleWeights[11];
        public float CrawlWeight => styleWeights[11];
        private double continuousCycle;
        private int lastStepIndex = int.MinValue;

        // Raised once per foot contact, driven by travelled distance rather than by time.
        public event System.Action<GaitFootstep> Footstep;

        private void Awake()
        {
            spine = GetComponent<ProceduralSpine>();
            characterController = GetComponent<CharacterController>();
            if (body != null) bodyOrigin = body.localPosition;
            UpdateStyleWeights(100f);
        }

        public void SetArmOutwardAngle(float angle)
        {
            armOutwardAngle = Mathf.Clamp(angle, 0f, 90f);
        }

        public void SetRigDimensions(float thigh, float calf, float hip, float halfWidth, Vector3 origin)
        {
            thighLength = thigh;
            calfLength = calf;
            hipHeight = hip;
            hipHalfWidth = halfWidth;
            bodyOrigin = origin;
        }

        public void Animate(Vector3 velocity, Vector3 acceleration, float distance, bool grounded, float maximumSpeed, float deltaTime = -1f)
        {
            float dt = deltaTime >= 0f ? deltaTime : Time.deltaTime;
            if (dt <= 0f || body == null) return;
            terrainGrounded = grounded;
            float speed = velocity.magnitude;
            intensity = Mathf.SmoothDamp(intensity, grounded ? Mathf.Clamp01(speed / Mathf.Max(maximumSpeed, 0.1f)) : 0f,
                ref intensityVelocity, 0.12f, Mathf.Infinity, dt);
            float activity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(intensity * 5f));
            UpdateStyleWeights(dt);
            System.Array.Copy(styleWeights, locomotionWeights, styleWeights.Length);
            for (int i = 0; i < styleWeights.Length; i++)
                styleWeights[i] = Mathf.Lerp(styleWeights[i], i == 10 ? 1f : 0f, Mathf.Clamp01(KnockdownWeight));
            style = GaitStylePose.Blend(styleWeights, cycle);
            poseActivity = activity;
            float runBlend = style.RunAmount;
            float stride = 2f * Mathf.Lerp(walkStepLength, runStepLength, runBlend);
            stride = Mathf.Max(0.1f, stride * style.StrideScale);
            float advance = distance / stride;
            cycle = Mathf.Repeat(cycle + advance, 1f);
            ReportFootsteps(advance, grounded, activity);
            style = GaitStylePose.Blend(styleWeights, cycle);
            float wave = Mathf.Sin(cycle * Mathf.PI * 2f);
            noiseTime += dt * noiseFrequency;
            float irregularity = noiseAmount * activity;
            Vector3 noise = new Vector3(Noise(0f), Noise(11f), Noise(23f)) * irregularity;
            leftArmNoise = Noise(37f) * irregularity * 10f;
            rightArmNoise = Noise(53f) * irregularity * 10f;
            Vector3 localAcceleration = transform.InverseTransformDirection(Vector3.ClampMagnitude(acceleration, 16f));
            Vector3 targetLean = new Vector3(intensity * forwardLeanAngle + localAcceleration.z * 0.8f + style.Lean * activity * (spine != null ? 0.35f : 1f),
                -wave * intensity * 7f, -localAcceleration.x * turnLeanStrength);
            targetLean += noise * 5f;
            lean = Vector3.SmoothDamp(lean, targetLean, ref leanVelocity, 0.18f, Mathf.Infinity, dt);
            float motionWeight = activity * Mathf.Lerp(0.65f, 1f, intensity);
            sideRoll = Mathf.SmoothDamp(sideRoll, -wave * sideLeanAngle * motionWeight * style.SwayScale,
                ref sideRollVelocity, bodyMotionSmoothTime, Mathf.Infinity, dt);
            // Contact -> down -> passing -> up, repeated for the opposite foot.
            // The harmonic adds a soft secondary bounce without sharp curve corners.
            float stepPhase = cycle * Mathf.PI * 4f;
            float bounceWave = 0.5f + 0.46f * Mathf.Sin(stepPhase) + 0.04f * Mathf.Sin(stepPhase * 2f);
            float bounceHeight = Mathf.Lerp(walkBounceHeight, runBounceHeight, runBlend) * activity;
            float compression = -0.035f * intensity - bounceWave * bounceHeight * style.BounceScale + style.BodyY * activity;
            // Lift feet and pelvis together: airborne motion must not stretch the IK chains.
            float hopWave = Mathf.Max(0f, -Mathf.Sin(stepPhase));
            float targetFlight = (hopWave * hopWave * runHopHeight * runBlend + style.Flight) * activity;
            flightLift = Mathf.SmoothDamp(flightLift, targetFlight, ref flightVelocity, bodyMotionSmoothTime, Mathf.Infinity, dt);
            float breathing = Mathf.Sin(Time.time * 2.5f) * 0.008f * (1f - activity);
            Vector3 targetMotion = new Vector3(wave * sideSwayDistance * motionWeight, compression + breathing, 0f);
            targetMotion += new Vector3(noise.x * 0.015f, noise.y * 0.02f, 0f);
            bodyMotion = Vector3.SmoothDamp(bodyMotion, targetMotion, ref bodyMotionVelocity, bodyMotionSmoothTime, Mathf.Infinity, dt);
            // Persistent posture is independent of movement activity. Interaction adds only
            // the remaining crouch, so a prone character never sinks through the floor.
            float low = Mathf.Clamp01(LowPostureWeight);
            float crouch = InteractionCrouch * (1f - low + 0.35f * styleWeights[10]);
            float loweredHip = styleWeights[10] * (hipHeight - thighLength * 0.92f - 0.12f)
                + styleWeights[11] * (hipHeight - 0.32f);
            body.localPosition = bodyOrigin + bodyMotion * (1f - low * 0.8f)
                + Vector3.up * (flightLift * (1f - low) - loweredHip - hipHeight * 0.72f * crouch);
            Vector3 bodyAngles = lean * (1f - low) + Vector3.forward * sideRoll
                + Vector3.right * (35f * crouch + (12f + 28f * InteractionCrouch) * styleWeights[10] + 78f * styleWeights[11]);
            body.localRotation = Quaternion.Euler(bodyAngles);
            if (spine != null) spine.Animate(style, activity, dt);
            if (head != null && spine == null)
                head.rotation = transform.rotation;

            Leg(leftThigh, leftCalf, leftFoot, -1f, cycle, stride, activity);
            Leg(rightThigh, rightCalf, rightFoot, 1f, Mathf.Repeat(cycle + 0.5f, 1f), stride, activity);
            float amplitude = Mathf.Lerp(12f, 62f, intensity) * activity * style.ArmSwingScale;
            leftSwing = Mathf.SmoothDamp(leftSwing, -wave * amplitude + leftArmNoise, ref leftSwingVelocity, armLag, Mathf.Infinity, dt);
            rightSwing = Mathf.SmoothDamp(rightSwing, wave * amplitude + rightArmNoise, ref rightSwingVelocity, armLag, Mathf.Infinity, dt);
            Arm(leftArm, leftForearm, -1f, leftSwing, dt);
            Arm(rightArm, rightForearm, 1f, rightSwing, dt);
            CrawlArm(leftArm, leftForearm, cycle, activity);
            CrawlArm(rightArm, rightForearm, Mathf.Repeat(cycle + 0.5f, 1f), activity);
            System.Array.Copy(locomotionWeights, styleWeights, styleWeights.Length);
        }

        // The left leg runs on `cycle` and the right one half a cycle later, and each leg plants its
        // foot as its own phase wraps to zero. One step therefore happens every half cycle: even
        // half-cycle indices belong to the left foot, odd ones to the right.
        private void ReportFootsteps(float advance, bool grounded, float activity)
        {
            continuousCycle += advance;
            int step = (int)System.Math.Floor(continuousCycle * 2.0);
            if (step == lastStepIndex) return;
            bool firstEvaluation = lastStepIndex == int.MinValue;
            lastStepIndex = step;
            if (LowPostureWeight > 0.5f || firstEvaluation || !grounded || activity < 0.05f || Footstep == null) return;
            bool isLeft = (step & 1) == 0;
            Transform contact = isLeft ? leftFoot : rightFoot;
            Footstep.Invoke(new GaitFootstep(isLeft, Mathf.Clamp01(intensity),
                styleWeights[4] + styleWeights[8], styleWeights[5] + styleWeights[7],
                contact != null ? contact.position : transform.position));
        }

        private void UpdateStyleWeights(float dt)
        {
            UpdateAutomaticStyle(dt);
            float total = walk + doubleBounceWalk + strut + shuffle + sneak + run + jump + fastRun + tipToe + skip + kneeWalk + crawl;
            float alpha = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, styleTransitionTime));
            for (int i = 0; i < styleWeights.Length; i++)
            {
                float value;
                switch (i)
                {
                    case 0: value = walk; break;
                    case 1: value = doubleBounceWalk; break;
                    case 2: value = strut; break;
                    case 3: value = shuffle; break;
                    case 4: value = sneak; break;
                    case 5: value = run; break;
                    case 6: value = jump; break;
                    case 7: value = fastRun; break;
                    case 8: value = tipToe; break;
                    case 9: value = skip; break;
                    case 10: value = kneeWalk; break;
                    default: value = crawl; break;
                }
                float target = total > 0.0001f ? value / total : (i == 0 ? 1f : 0f);
                styleWeights[i] = Mathf.Lerp(styleWeights[i], target, alpha);
            }
        }

        public void SetRunning(bool running)
        {
            SetLocomotionStyle(running, false);
        }

        public void SetLocomotionStyle(bool running, bool sneaking, bool walking = false)
        {
            if ((running || sneaking || walking) && !automaticStyleActive)
            {
                for (int i = 0; i < savedWalkingWeights.Length; i++)
                    savedWalkingWeights[i] = GetStyleWeight(i);
                automaticStyleActive = true;
            }
            runningRequested = running;
            sneakingRequested = sneaking;
            walkingRequested = walking;
        }

        private void UpdateAutomaticStyle(float dt)
        {
            if (!automaticStyleActive) return;
            float alpha = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, styleTransitionTime));
            bool settled = true;
            for (int i = 0; i < savedWalkingWeights.Length; i++)
            {
                float target = runningRequested ? (i == 5 ? 1f : 0f) : sneakingRequested ? (i == 4 ? 1f : 0f) : walkingRequested ? (i == 0 ? 1f : 0f) : savedWalkingWeights[i];
                float value = Mathf.Lerp(GetStyleWeight(i), target, alpha);
                if (Mathf.Abs(value - target) < 0.0001f) value = target;
                else settled = false;
                SetStyleWeight(i, value);
            }
            if (!runningRequested && !sneakingRequested && !walkingRequested && settled) automaticStyleActive = false;
        }

        private float GetStyleWeight(int index)
        {
            switch (index)
            {
                case 0: return walk;
                case 1: return doubleBounceWalk;
                case 2: return strut;
                case 3: return shuffle;
                case 4: return sneak;
                case 5: return run;
                case 6: return jump;
                case 7: return fastRun;
                case 8: return tipToe;
                case 9: return skip;
                case 10: return kneeWalk;
                default: return crawl;
            }
        }

        private void SetStyleWeight(int index, float value)
        {
            switch (index)
            {
                case 0: walk = value; break;
                case 1: doubleBounceWalk = value; break;
                case 2: strut = value; break;
                case 3: shuffle = value; break;
                case 4: sneak = value; break;
                case 5: run = value; break;
                case 6: jump = value; break;
                case 7: fastRun = value; break;
                case 8: tipToe = value; break;
                case 9: skip = value; break;
                case 10: kneeWalk = value; break;
                case 11: crawl = value; break;
            }
        }

        private void OnDisable()
        {
            if (automaticStyleActive)
                for (int i = 0; i < savedWalkingWeights.Length; i++)
                    SetStyleWeight(i, savedWalkingWeights[i]);
            runningRequested = false;
            sneakingRequested = false;
            walkingRequested = false;
            automaticStyleActive = false;
        }

        private float Noise(float channel)
        {
            return Mathf.PerlinNoise(noiseSeed + channel, noiseTime) * 2f - 1f;
        }

        private void Arm(Transform upper, Transform lower, float side, float swing, float dt)
        {
            if (upper == null || lower == null) return;
            upper.localRotation = Quaternion.Euler(swing - armRaiseAngle + style.ArmPitch * poseActivity, side * intensity * 8f,
                side * (armOutwardAngle + intensity * 18f + Mathf.Abs(swing) * 0.12f));
            float elbow = Mathf.Clamp(forearmRaiseAngle + intensity * forearmSpeedBend
                + Mathf.Max(0f, -swing) * forearmSwingBend + style.Elbow * poseActivity, 0f, 145f);
            lower.localRotation = Quaternion.Slerp(lower.localRotation,
                Quaternion.Euler(-elbow, 0f, 0f), 1f - Mathf.Exp(-12f * dt));
        }

        private void CrawlArm(Transform upper, Transform lower, float phase, float activity)
        {
            if (upper == null || lower == null || CrawlWeight <= 0f) return;
            float pull = Mathf.Sin(phase * Mathf.PI * 2f) * activity;
            // World-relative arms support the horizontal torso. The holding/reach
            // overlays run later and can take either pose without changing the gait.
            upper.rotation = Quaternion.Slerp(upper.rotation,
                transform.rotation * Quaternion.Euler(-48f - 28f * pull, 0f,
                    upper == leftArm ? -24f : 24f), CrawlWeight);
            lower.localRotation = Quaternion.Slerp(lower.localRotation,
                Quaternion.Euler(-65f + 25f * pull, 0f, 0f), CrawlWeight);
        }

        private float SampleFootGround(float x, float z)
        {
            // Ignore the character, triggers and steep faces. Probe below root level too,
            // so the trailing leg can remain on the lower tread after the capsule rises.
            float extent = calfLength + 0.15f;
            Vector3 origin = transform.TransformPoint(new Vector3(x, extent, z));
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, terrainHits,
                extent * 2f * transform.lossyScale.y, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            float height = 0f;
            for (int i = 0; i < count; i++)
            {
                var hit = terrainHits[i];
                if (hit.transform.IsChildOf(transform) || hit.distance >= nearest
                    || Vector3.Angle(hit.normal, Vector3.up) > characterController.slopeLimit) continue;
                float localHeight = transform.InverseTransformPoint(hit.point).y;
                if (localHeight > calfLength + 0.01f) continue;
                nearest = hit.distance;
                height = localHeight;
            }
            return height;
        }

        private void Leg(Transform thigh, Transform calf, Transform foot, float side, float phase,
            float stride, float activity)
        {
            if (thigh == null || calf == null || foot == null) return;
            // First half: foot travels backward on the floor. Second half: lifted recovery.
            float travel = stride * 0.25f * activity;
            float z, lift;
            if (phase < 0.5f)
            {
                z = Mathf.Lerp(travel, -travel, phase * 2f);
                lift = 0f;
            }
            else
            {
                float t = (phase - 0.5f) * 2f;
                z = Mathf.Lerp(-travel, travel, Mathf.SmoothStep(0f, 1f, t));
                lift = Mathf.Sin(t * Mathf.PI) * Mathf.Lerp(0.08f, 0.29f, intensity) * activity;
            }
            lift *= style.FootLiftScale;
            float toePitch = 0f;
            GaitStylePose.AdjustFoot(styleWeights, cycle, side, travel, activity, ref z, ref lift, ref toePitch);
            // Keep the thigh attached to the moving pelvis as the torso rolls and bounces.
            Vector3 hip = transform.InverseTransformPoint(body.TransformPoint(
                new Vector3(side * hipHalfWidth, hipHeight - bodyOrigin.y, 0f)));
            Vector3 ankle = new Vector3(side * (hipHalfWidth + intensity * 0.025f), 0.09f + lift + flightLift, z);
            if (terrainGrounded && characterController != null)
            {
                float ground = SampleFootGround(ankle.x, z);
                if (phase >= 0.5f && activity > 0.01f)
                {
                    // Look across the recovery path before the toe reaches the riser.
                    float obstacle = ground;
                    for (int sample = 0; sample <= 4; sample++)
                        obstacle = Mathf.Max(obstacle, SampleFootGround(ankle.x,
                            Mathf.Lerp(z, travel + 0.12f, sample / 4f)));
                    float recovery = (phase - 0.5f) * 2f;
                    float clearance = Mathf.SmoothStep(0f, 1f, Mathf.Min(recovery * 5f, 1f));
                    ground = Mathf.Max(ground, obstacle * clearance);
                }
                ankle.y += ground;
            }
            Vector3 delta = ankle - hip;
            float reach = Mathf.Clamp(delta.magnitude, 0.05f, thighLength + calfLength - 0.002f);
            Vector3 direction = delta.normalized;
            float along = (thighLength * thighLength - calfLength * calfLength + reach * reach) / (2f * reach);
            float height = Mathf.Sqrt(Mathf.Max(0f, thighLength * thighLength - along * along));
            Vector3 bend = Vector3.ProjectOnPlane(Vector3.forward, direction).normalized;
            Vector3 knee = hip + direction * along + bend * height;
            Vector3 solvedAnkle = hip + direction * reach;
            float low = Mathf.Clamp01(LowPostureWeight);
            if (low > 0f)
            {
                float crawlBlend = CrawlWeight / low;
                Vector3 lowKnee = new Vector3(side * (hipHalfWidth + 0.03f),
                    0.12f + lift * 0.3f, z + Mathf.Lerp(0.2f, 0.35f, crawlBlend));
                if (terrainGrounded && characterController != null)
                    lowKnee.y += SampleFootGround(lowKnee.x, lowKnee.z);
                float contactHeight = lowKnee.y;
                // The knee leads; the shin trails flat instead of solving a standing foot plant.
                Vector3 kneeDirection = (lowKnee - hip).normalized;
                lowKnee = hip + kneeDirection * thighLength;
                if (lowKnee.y < contactHeight)
                {
                    float vertical = Mathf.Clamp(hip.y - contactHeight, -thighLength, thighLength);
                    Vector3 horizontal = Vector3.ProjectOnPlane(kneeDirection, Vector3.up).normalized;
                    lowKnee = hip + horizontal * Mathf.Sqrt(Mathf.Max(0f, thighLength * thighLength - vertical * vertical))
                        - Vector3.up * vertical;
                }
                Vector3 lowAnkle = lowKnee + new Vector3(0f, 0.03f, -calfLength).normalized * calfLength;
                knee = Vector3.Lerp(knee, lowKnee, low);
                solvedAnkle = Vector3.Lerp(solvedAnkle, lowAnkle, low);
                // Keep both bone lengths intact through standing/low-pose blends.
                knee = hip + (knee - hip).normalized * thighLength;
                solvedAnkle = knee + (solvedAnkle - knee).normalized * calfLength;
                toePitch = Mathf.Lerp(toePitch, -65f, low);
            }
            thigh.localPosition = hip;
            thigh.rotation = transform.rotation * Quaternion.FromToRotation(Vector3.down, knee - hip);
            calf.position = transform.TransformPoint(knee);
            calf.rotation = transform.rotation * Quaternion.FromToRotation(Vector3.down, solvedAnkle - knee);
            foot.position = transform.TransformPoint(solvedAnkle);
            foot.rotation = transform.rotation * Quaternion.Euler(-lift * 65f + toePitch, side * 5f, 0f);
        }
    }
}
