using System;
using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Shared behaviour and attack tuning for procedurally rigged centipedes.</summary>
    [CreateAssetMenu(fileName = "Centipede Balance", menuName = "Cards Unity/Balance/Centipede")]
    public sealed class CentipedeBalanceProfile : ScriptableObject
    {
        [SerializeField] private CentipedeHuntingBalance hunting = new();
        [SerializeField] private CentipedeVariationBalance variation = new();
        [SerializeField] private CentipedeRecoveryBalance recovery = new();
        [SerializeField] private CentipedeLightBalance light = new();
        [SerializeField] private CentipedeAttackBalance attack = new();

        public CentipedeHuntingBalance Hunting => hunting;
        public CentipedeVariationBalance Variation => variation;
        public CentipedeRecoveryBalance Recovery => recovery;
        public CentipedeLightBalance Light => light;
        public CentipedeAttackBalance Attack => attack;

        private void OnValidate()
        {
            hunting ??= new CentipedeHuntingBalance();
            variation ??= new CentipedeVariationBalance();
            recovery ??= new CentipedeRecoveryBalance();
            light ??= new CentipedeLightBalance();
            attack ??= new CentipedeAttackBalance();
            hunting.Validate(); variation.Validate(); recovery.Validate(); light.Validate(); attack.Validate();
        }

#if UNITY_EDITOR
        public bool CaptureFrom(ProceduralCentipede source, out string warning)
        {
            if (source == null)
            {
                warning = "Select a ProceduralCentipede to capture.";
                return false;
            }
            UnityEditor.Undo.RecordObject(this, "Capture Centipede Balance");
            source.CopyLegacyBalanceTo(hunting, variation, recovery, light, attack);
            OnValidate();
            UnityEditor.EditorUtility.SetDirty(this);
            warning = string.Empty;
            return true;
        }

        public bool ApplyTo(ProceduralCentipede target, out string warning)
        {
            if (target == null)
            {
                warning = "Select a ProceduralCentipede to assign.";
                return false;
            }
            warning = target.BalanceProfile != null && target.BalanceProfile != this
                ? "Replaced a different centipede balance profile." : string.Empty;
            UnityEditor.Undo.RecordObject(target, "Assign Centipede Balance");
            target.AssignBalanceProfile(this);
            return true;
        }
#endif
    }

    [Serializable]
    public sealed class CentipedeHuntingBalance
    {
        [SerializeField, Min(0.1f)] private float stalkSpeed = 1.2f;
        [SerializeField, Min(0.1f)] private float fleeSpeed = 4.5f;
        [SerializeField, Min(0.1f)] private float stopDistance = 1.2f;
        [SerializeField, Min(1f)] private float turnSpeed = 300f;
        public float StalkSpeed => stalkSpeed;
        public float FleeSpeed => fleeSpeed;
        public float StopDistance => stopDistance;
        public float TurnSpeed => turnSpeed;
        internal void Capture(float stalk, float flee, float stop, float turn)
        { stalkSpeed = stalk; fleeSpeed = flee; stopDistance = stop; turnSpeed = turn; }
        internal void Validate()
        {
            stalkSpeed = Mathf.Max(0.1f, stalkSpeed);
            fleeSpeed = Mathf.Max(stalkSpeed, fleeSpeed);
            stopDistance = Mathf.Max(0.1f, stopDistance);
            turnSpeed = Mathf.Max(1f, turnSpeed);
        }
    }

    [Serializable]
    public sealed class CentipedeVariationBalance
    {
        [SerializeField, Range(0f, 1f)] private float amount = 0.8f;
        [SerializeField, Range(0f, 60f)] private float wanderAngle = 32f;
        [SerializeField, Range(0.05f, 2f)] private float noiseFrequency = 0.45f;
        [SerializeField, Range(0f, 0.5f)] private float speedVariation = 0.3f;
        public float Amount => amount;
        public float WanderAngle => wanderAngle;
        public float NoiseFrequency => noiseFrequency;
        public float SpeedVariation => speedVariation;
        internal void Capture(float variation, float wander, float frequency, float speed)
        { amount = variation; wanderAngle = wander; noiseFrequency = frequency; speedVariation = speed; }
        internal void Validate()
        {
            amount = Mathf.Clamp01(amount);
            wanderAngle = Mathf.Clamp(wanderAngle, 0f, 60f);
            noiseFrequency = Mathf.Clamp(noiseFrequency, 0.05f, 2f);
            speedVariation = Mathf.Clamp(speedVariation, 0f, 0.5f);
        }
    }

    [Serializable]
    public sealed class CentipedeRecoveryBalance
    {
        [SerializeField, Min(0.2f)] private float stuckCheckInterval = 0.7f;
        [SerializeField, Min(0.2f)] private float recoveryDuration = 1.5f;
        public float StuckCheckInterval => stuckCheckInterval;
        public float RecoveryDuration => recoveryDuration;
        internal void Capture(float interval, float duration)
        { stuckCheckInterval = interval; recoveryDuration = duration; }
        internal void Validate()
        {
            stuckCheckInterval = Mathf.Max(0.2f, stuckCheckInterval);
            recoveryDuration = Mathf.Max(0.2f, recoveryDuration);
        }
    }

    [Serializable]
    public sealed class CentipedeLightBalance
    {
        [SerializeField, Min(0.0001f)] private float dimLightThreshold = 0.005f;
        [SerializeField, Min(0.001f)] private float fearThreshold = 0.12f;
        [SerializeField, Range(0.1f, 0.9f)] private float safeLightRatio = 0.4f;
        [SerializeField, Min(0f)] private float hideDuration = 2.5f;
        public float DimLightThreshold => dimLightThreshold;
        public float FearThreshold => fearThreshold;
        public float SafeLightRatio => safeLightRatio;
        public float HideDuration => hideDuration;
        internal void Capture(float dim, float fear, float safeRatio, float hide)
        { dimLightThreshold = dim; fearThreshold = fear; safeLightRatio = safeRatio; hideDuration = hide; }
        internal void Validate()
        {
            dimLightThreshold = Mathf.Max(0.0001f, dimLightThreshold);
            fearThreshold = Mathf.Max(0.001f, fearThreshold);
            safeLightRatio = Mathf.Clamp(safeLightRatio, 0.1f, 0.9f);
            hideDuration = Mathf.Max(0f, hideDuration);
        }
    }

    [Serializable]
    public sealed class CentipedeAttackBalance
    {
        [SerializeField, Min(0.1f)] private float lightPatience = 3f;
        [SerializeField, Min(0.5f)] private float attackRange = 3.5f;
        [SerializeField, Min(0.1f)] private float attackWindup = 0.65f;
        [SerializeField, Min(0.1f)] private float leapDuration = 0.45f;
        [SerializeField, Min(0f)] private float leapHeight = 0.8f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 4f;
        [SerializeField, Min(0f)] private float darknessAttackThreshold = 0.02f;
        [SerializeField, Min(0.1f)] private float darknessPatience = 0.75f;
        public float LightPatience => lightPatience;
        public float AttackRange => attackRange;
        public float AttackWindup => attackWindup;
        public float LeapDuration => leapDuration;
        public float LeapHeight => leapHeight;
        public float AttackCooldown => attackCooldown;
        public float DarknessAttackThreshold => darknessAttackThreshold;
        public float DarknessPatience => darknessPatience;
        internal void Capture(float patience, float range, float windup, float duration, float height,
            float cooldown, float darknessThreshold, float darknessDelay)
        {
            lightPatience = patience; attackRange = range; attackWindup = windup;
            leapDuration = duration; leapHeight = height; attackCooldown = cooldown;
            darknessAttackThreshold = darknessThreshold; darknessPatience = darknessDelay;
        }
        internal void Validate()
        {
            lightPatience = Mathf.Max(0.1f, lightPatience);
            attackRange = Mathf.Max(0.5f, attackRange);
            attackWindup = Mathf.Max(0.1f, attackWindup);
            leapDuration = Mathf.Max(0.1f, leapDuration);
            leapHeight = Mathf.Max(0f, leapHeight);
            attackCooldown = Mathf.Max(0.1f, attackCooldown);
            darknessAttackThreshold = Mathf.Max(0f, darknessAttackThreshold);
            darknessPatience = Mathf.Max(0.1f, darknessPatience);
        }
    }
}
