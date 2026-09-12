using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>
    /// Shared player balance values. Components keep their serialized values as an exact fallback
    /// when no profile is assigned; rig references and authored pose values remain on each instance.
    /// </summary>
    [CreateAssetMenu(fileName = "Player Gameplay Balance", menuName = "Cards Unity/Balance/Player Gameplay")]
    public sealed class PlayerGameplayBalanceProfile : ScriptableObject
    {
        [SerializeField] private PlayerMovementBalance movement = new();
        [SerializeField] private PlayerHealthBalance health = new();
        [SerializeField] private PlayerStressBalance stress = new();
        [SerializeField] private PlayerKnockdownBalance knockdown = new();
        [SerializeField] private PlayerAttackBalance attack = new();
        [SerializeField] private PlayerInteractionBalance interaction = new();
        [SerializeField] private PlayerDeathBalance death = new();

        public PlayerMovementBalance Movement => movement;
        public PlayerHealthBalance Health => health;
        public PlayerStressBalance Stress => stress;
        public PlayerKnockdownBalance Knockdown => knockdown;
        public PlayerAttackBalance Attack => attack;
        public PlayerInteractionBalance Interaction => interaction;
        public PlayerDeathBalance Death => death;

        private void OnValidate()
        {
            movement ??= new PlayerMovementBalance();
            health ??= new PlayerHealthBalance();
            stress ??= new PlayerStressBalance();
            knockdown ??= new PlayerKnockdownBalance();
            attack ??= new PlayerAttackBalance();
            interaction ??= new PlayerInteractionBalance();
            death ??= new PlayerDeathBalance();
            movement.Validate();
            health.Validate();
            stress.Validate();
            knockdown.Validate();
            attack.Validate();
            interaction.Validate();
            death.Validate();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Captures every player balance group from the component values currently serialized on
        /// <paramref name="playerRoot"/>. The profile is left untouched when a required component is missing.
        /// </summary>
        public bool CaptureFrom(GameObject playerRoot, out string warning)
        {
            if (!TryGetPlayerComponents(playerRoot, out var character, out var playerHealth,
                    out var playerStress, out var playerKnockdown, out var torchAttack,
                    out var torchInteraction, out var characterDeath, out warning)) return false;

            UnityEditor.Undo.RecordObject(this, "Capture Player Gameplay Balance");
            character.CopyLegacyBalanceTo(movement);
            playerHealth.CopyLegacyBalanceTo(health);
            playerStress.CopyLegacyBalanceTo(stress);
            playerKnockdown.CopyLegacyBalanceTo(knockdown);
            torchAttack.CopyLegacyBalanceTo(attack);
            torchInteraction.CopyLegacyBalanceTo(interaction);
            characterDeath.CopyLegacyBalanceTo(death);
            OnValidate();
            UnityEditor.EditorUtility.SetDirty(this);
            return true;
        }

        /// <summary>Assigns this profile to the complete player component group.</summary>
        public bool ApplyTo(GameObject playerRoot, out string warning)
        {
            if (!TryGetPlayerComponents(playerRoot, out var character, out var playerHealth,
                    out var playerStress, out var playerKnockdown, out var torchAttack,
                    out var torchInteraction, out var characterDeath, out warning)) return false;

            var conflicts = new List<string>();
            CheckConflict(character.BalanceProfile, nameof(ProceduralCharacter), conflicts);
            CheckConflict(playerHealth.BalanceProfile, nameof(CharacterHealth), conflicts);
            CheckConflict(playerStress.BalanceProfile, nameof(CharacterStress), conflicts);
            CheckConflict(playerKnockdown.BalanceProfile, nameof(CharacterKnockdown), conflicts);
            CheckConflict(torchAttack.BalanceProfile, nameof(TorchAttack), conflicts);
            CheckConflict(torchInteraction.BalanceProfile, nameof(TorchInteraction), conflicts);
            CheckConflict(characterDeath.BalanceProfile, nameof(CharacterDeath), conflicts);

            UnityEditor.Undo.RecordObjects(new UnityEngine.Object[]
            {
                character, playerHealth, playerStress, playerKnockdown,
                torchAttack, torchInteraction, characterDeath
            }, "Assign Player Gameplay Balance");
            character.AssignBalanceProfile(this);
            playerHealth.AssignBalanceProfile(this);
            playerStress.AssignBalanceProfile(this);
            playerKnockdown.AssignBalanceProfile(this);
            torchAttack.AssignBalanceProfile(this);
            torchInteraction.AssignBalanceProfile(this);
            characterDeath.AssignBalanceProfile(this);

            warning = conflicts.Count == 0 ? string.Empty
                : "Replaced different player profiles on: " + string.Join(", ", conflicts) + ".";
            return true;
        }

        private void CheckConflict(PlayerGameplayBalanceProfile assigned, string component, List<string> conflicts)
        {
            if (assigned != null && assigned != this) conflicts.Add(component);
        }

        private static bool TryGetPlayerComponents(GameObject root, out ProceduralCharacter character,
            out CharacterHealth healthComponent, out CharacterStress stressComponent,
            out CharacterKnockdown knockdownComponent, out TorchAttack attackComponent,
            out TorchInteraction interactionComponent, out CharacterDeath deathComponent,
            out string warning)
        {
            character = root != null ? root.GetComponent<ProceduralCharacter>() : null;
            healthComponent = root != null ? root.GetComponent<CharacterHealth>() : null;
            stressComponent = root != null ? root.GetComponent<CharacterStress>() : null;
            knockdownComponent = root != null ? root.GetComponent<CharacterKnockdown>() : null;
            attackComponent = root != null ? root.GetComponent<TorchAttack>() : null;
            interactionComponent = root != null ? root.GetComponent<TorchInteraction>() : null;
            deathComponent = root != null ? root.GetComponent<CharacterDeath>() : null;
            var missing = new List<string>();
            if (character == null) missing.Add(nameof(ProceduralCharacter));
            if (healthComponent == null) missing.Add(nameof(CharacterHealth));
            if (stressComponent == null) missing.Add(nameof(CharacterStress));
            if (knockdownComponent == null) missing.Add(nameof(CharacterKnockdown));
            if (attackComponent == null) missing.Add(nameof(TorchAttack));
            if (interactionComponent == null) missing.Add(nameof(TorchInteraction));
            if (deathComponent == null) missing.Add(nameof(CharacterDeath));
            warning = missing.Count == 0 ? string.Empty
                : "Player root is missing required components: " + string.Join(", ", missing) + ".";
            return missing.Count == 0;
        }
#endif
    }

    [Serializable]
    public sealed class PlayerMovementBalance
    {
        [SerializeField, Range(0.01f, 0.5f)] private float idleThreshold = 0.08f;
        [SerializeField, Range(0.02f, 0.99f)] private float walkThreshold = 0.35f;
        [SerializeField, Min(0f)] private float walkSpeed = 3.5f;
        [SerializeField, Min(0f)] private float acceleration = 9f;
        [SerializeField, Min(0f)] private float braking = 7f;
        [SerializeField, Min(0f)] private float runSpeed = 6f;
        [SerializeField, Min(0f)] private float turnSpeed = 540f;
        [Tooltip("Movement speed multiplier while knocked down.")]
        [SerializeField, Range(0f, 1f)] private float knockedDownSpeedMultiplier = 0.4f;

        public float IdleThreshold => idleThreshold;
        public float WalkThreshold => walkThreshold;
        public float WalkSpeed => walkSpeed;
        public float Acceleration => acceleration;
        public float Braking => braking;
        public float RunSpeed => runSpeed;
        public float TurnSpeed => turnSpeed;
        public float KnockedDownSpeedMultiplier => knockedDownSpeedMultiplier;

        internal void Capture(float idle, float walk, float speed, float accelerate, float brake,
            float run, float turn)
        {
            idleThreshold = idle;
            walkThreshold = walk;
            walkSpeed = speed;
            acceleration = accelerate;
            braking = brake;
            runSpeed = run;
            turnSpeed = turn;
        }

        internal void Validate()
        {
            idleThreshold = Mathf.Clamp(idleThreshold, 0.01f, 0.5f);
            walkThreshold = Mathf.Clamp(walkThreshold, idleThreshold + 0.01f, 0.99f);
            walkSpeed = Mathf.Max(0f, walkSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            braking = Mathf.Max(0f, braking);
            runSpeed = Mathf.Max(0f, runSpeed);
            turnSpeed = Mathf.Max(0f, turnSpeed);
            knockedDownSpeedMultiplier = Mathf.Clamp01(knockedDownSpeedMultiplier);
        }
    }

    [Serializable]
    public sealed class PlayerHealthBalance
    {
        [SerializeField, Range(1, 8)] private int slotCount = 3;
        [SerializeField, Min(0.01f)] private float refillSecondsPerSlot = 10f;
        public int SlotCount => slotCount;
        public float RefillSecondsPerSlot => refillSecondsPerSlot;
        internal void Capture(int slots, float refill) { slotCount = slots; refillSecondsPerSlot = refill; }
        internal void Validate()
        {
            slotCount = Mathf.Clamp(slotCount, 1, 8);
            refillSecondsPerSlot = Mathf.Max(0.01f, refillSecondsPerSlot);
        }
    }

    [Serializable]
    public sealed class PlayerStressBalance
    {
        [SerializeField, Range(0f, 100f)] private float initialStress;
        [SerializeField, Min(0f)] private float encounterStress = 8f;
        [SerializeField, Min(0f)] private float hitStress = 25f;
        [SerializeField, Min(0.1f)] private float encounterRange = 7f;
        [SerializeField, Min(1f)] private float encounterResetTime = 10f;
        [SerializeField, Min(0f)] private float recoveryPerSecond;
        [SerializeField, Min(0f)] private float recoveryDelay = 10f;
        public float InitialStress => initialStress;
        public float EncounterStress => encounterStress;
        public float HitStress => hitStress;
        public float EncounterRange => encounterRange;
        public float EncounterResetTime => encounterResetTime;
        public float RecoveryPerSecond => recoveryPerSecond;
        public float RecoveryDelay => recoveryDelay;
        internal void Capture(float initial, float encounter, float hit, float range, float reset, float recovery, float delay)
        {
            initialStress = initial;
            encounterStress = encounter;
            hitStress = hit;
            encounterRange = range;
            encounterResetTime = reset;
            recoveryPerSecond = recovery;
            recoveryDelay = delay;
        }
        internal void Validate()
        {
            initialStress = Mathf.Clamp(initialStress, 0f, 100f);
            encounterStress = Mathf.Max(0f, encounterStress);
            hitStress = Mathf.Max(0f, hitStress);
            encounterRange = Mathf.Max(0.1f, encounterRange);
            encounterResetTime = Mathf.Max(1f, encounterResetTime);
            recoveryPerSecond = Mathf.Max(0f, recoveryPerSecond);
            recoveryDelay = Mathf.Max(0f, recoveryDelay);
        }
    }

    [Serializable]
    public sealed class PlayerKnockdownBalance
    {
        [SerializeField, Min(0.05f)] private float fallDuration = 0.25f;
        [SerializeField, Min(0f)] private float kneelDuration = 1.4f;
        [SerializeField, Min(0.05f)] private float standDuration = 0.65f;
        [SerializeField, Min(0f)] private float immunityAfterStanding = 1.5f;
        public float FallDuration => fallDuration;
        public float KneelDuration => kneelDuration;
        public float StandDuration => standDuration;
        public float ImmunityAfterStanding => immunityAfterStanding;
        internal void Capture(float fall, float kneel, float stand, float immunity)
        {
            fallDuration = fall; kneelDuration = kneel; standDuration = stand; immunityAfterStanding = immunity;
        }
        internal void Validate()
        {
            fallDuration = Mathf.Max(0.05f, fallDuration);
            kneelDuration = Mathf.Max(0f, kneelDuration);
            standDuration = Mathf.Max(0.05f, standDuration);
            immunityAfterStanding = Mathf.Max(0f, immunityAfterStanding);
        }
    }

    [Serializable]
    public sealed class PlayerAttackBalance
    {
        [SerializeField, Range(0.02f, 1f)] private float windupDuration = 0.14f;
        [SerializeField, Range(0.02f, 1f)] private float strikeDuration = 0.10f;
        [SerializeField, Range(0.02f, 2f)] private float recoverDuration = 0.30f;
        public float WindupDuration => windupDuration;
        public float StrikeDuration => strikeDuration;
        public float RecoverDuration => recoverDuration;
        internal void Capture(float windup, float strike, float recover)
        { windupDuration = windup; strikeDuration = strike; recoverDuration = recover; }
        internal void Validate()
        {
            windupDuration = Mathf.Clamp(windupDuration, 0.02f, 1f);
            strikeDuration = Mathf.Clamp(strikeDuration, 0.02f, 1f);
            recoverDuration = Mathf.Clamp(recoverDuration, 0.02f, 2f);
        }
    }

    [Serializable]
    public sealed class PlayerInteractionBalance
    {
        [SerializeField, Range(0.4f, 2f)] private float pickupRange = 1f;
        [SerializeField, Range(0.3f, 2f)] private float pickupDuration = 0.75f;
        [SerializeField, Range(0.15f, 1f)] private float recoveryDuration = 0.45f;
        public float PickupRange => pickupRange;
        public float PickupDuration => pickupDuration;
        public float RecoveryDuration => recoveryDuration;
        internal void Capture(float range, float pickup, float recovery)
        { pickupRange = range; pickupDuration = pickup; recoveryDuration = recovery; }
        internal void Validate()
        {
            pickupRange = Mathf.Clamp(pickupRange, 0.4f, 2f);
            pickupDuration = Mathf.Clamp(pickupDuration, 0.3f, 2f);
            recoveryDuration = Mathf.Clamp(recoveryDuration, 0.15f, 1f);
        }
    }

    [Serializable]
    public sealed class PlayerDeathBalance
    {
        [SerializeField, Min(0f)] private float defeatDelay = 3f;
        public float DefeatDelay => defeatDelay;
        internal void Capture(float delay) => defeatDelay = delay;
        internal void Validate() => defeatDelay = Mathf.Max(0f, defeatDelay);
    }
}
