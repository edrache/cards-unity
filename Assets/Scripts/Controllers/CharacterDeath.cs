using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Stops player actions on death, then presents the final defeat overlay.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterHealth))]
    public sealed class CharacterDeath : MonoBehaviour
    {
        [Header("Balance")]
        [Tooltip("Optional shared player balance. Existing Defeat Delay is used when empty.")]
        [SerializeField] private PlayerGameplayBalanceProfile balanceProfile;
        [Header("Local fallback — Defeat")]
        [SerializeField, Min(0f)] private float defeatDelay = 3f;

        private CharacterHealth health;
        private CharacterInventory inventory;
        private float elapsed;
        private float previousTimeScale = 1f;
        private bool ownsTimePause;

        public bool IsGameOver { get; private set; }
        public bool IsDefeatVisible { get; private set; }
        public PlayerGameplayBalanceProfile BalanceProfile => balanceProfile;

        private void Awake()
        {
            health = GetComponent<CharacterHealth>();
            inventory = GetComponent<CharacterInventory>();
        }

        private void OnEnable()
        {
            if (health == null) health = GetComponent<CharacterHealth>();
            if (health != null)
            {
                health.Died -= HandleDeath;
                health.Died += HandleDeath;
                if (health.IsDead) HandleDeath();
            }
        }

        private void Update() => Tick(Time.unscaledDeltaTime);

        /// <summary>Advances the unscaled delay before the defeat screen appears.</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (!IsGameOver || IsDefeatVisible) return;
            elapsed += Mathf.Max(0f, unscaledDeltaTime);
            if (elapsed < (balanceProfile != null ? balanceProfile.Death.DefeatDelay : defeatDelay)) return;

            if (inventory == null) inventory = GetComponent<CharacterInventory>();
            inventory?.ShowDefeatSummary();
            IsDefeatVisible = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            ownsTimePause = true;
        }

        private void HandleDeath()
        {
            if (IsGameOver) return;
            IsGameOver = true;
            elapsed = 0f;

            GetComponent<TorchAttack>()?.CancelAttack();
            GetComponent<TorchInteraction>()?.CancelInteraction();
            GetComponent<CharacterKnockdown>()?.TryKnockDown();

            var character = GetComponent<ProceduralCharacter>();
            if (character != null) character.enabled = false;
            var interaction = GetComponent<TorchInteraction>();
            if (interaction != null) interaction.enabled = false;
            var attack = GetComponent<TorchAttack>();
            if (attack != null) attack.enabled = false;
        }

        private void RestoreTimeScale()
        {
            if (!ownsTimePause) return;
            Time.timeScale = previousTimeScale;
            ownsTimePause = false;
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= HandleDeath;
            RestoreTimeScale();
        }

        private void OnDestroy() => RestoreTimeScale();

#if UNITY_EDITOR
        internal void CopyLegacyBalanceTo(PlayerDeathBalance destination)
        {
            destination.Capture(defeatDelay);
        }

        internal void AssignBalanceProfile(PlayerGameplayBalanceProfile profile)
        {
            balanceProfile = profile;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
