using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>
    /// Risks a health hit when the player keeps a burned-out torch in complete darkness.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterHealth))]
    public sealed class CharacterDarknessDamage : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CharacterHealth health;
        [SerializeField] private PlayerLightExposure lightExposure;
        [SerializeField] private CharacterStress stress;

        [Header("Darkness damage")]
        [Tooltip("Seconds spent continuously eligible before each damage roll.")]
        [SerializeField, Min(0.01f)] private float rollInterval = 5f;
        [Tooltip("A roll succeeds when one of this many equally likely outcomes is selected.")]
        [SerializeField, Min(1)] private int damageChanceDenominator = 6;
        [Tooltip("Maximum gameplay light exposure still treated as complete darkness.")]
        [SerializeField, Min(0f)] private float fullDarknessExposure = 0.0001f;
        [Tooltip("Maximum seconds before a held torch lookup is refreshed.")]
        [SerializeField, Min(0.1f)] private float torchRefreshInterval = 1f;

        private readonly System.Random random = new System.Random();
        private HandheldTorch heldTorch;
        private CaveExit caveExit;
        private float eligibleElapsed;
        private float torchRefreshElapsed = float.PositiveInfinity;

        /// <summary>Whether all conditions required to advance the damage timer currently hold.</summary>
        public bool IsEligible { get; private set; }

        /// <summary>Eligible scaled seconds accumulated toward the next damage roll.</summary>
        public float EligibleElapsed => eligibleElapsed;

        /// <summary>Eligible scaled seconds still required before the next damage roll.</summary>
        public float TimeUntilNextRoll => Mathf.Max(0f, Mathf.Max(0.01f, rollInterval) - eligibleElapsed);

        /// <summary>The burned-out torch currently cached as held by this player, if any.</summary>
        public HandheldTorch HeldBurnedOutTorch => IsHeldBurnedOutTorch(heldTorch) ? heldTorch : null;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ResetTimer();
            torchRefreshElapsed = float.PositiveInfinity;
        }

        private void OnDisable()
        {
            ResetTimer();
        }

        private void Update() => Tick(Time.deltaTime);

        /// <summary>
        /// Advances the scaled darkness-damage timer. At most one chance roll occurs per call,
        /// so long frames cannot catch up with multiple hits.
        /// </summary>
        public void Tick(float dt)
        {
            if (!isActiveAndEnabled)
            {
                ResetTimer();
                return;
            }
            if (dt < 0f) return;

            ResolveReferences();
            RefreshHeldTorch(dt);
            IsEligible = MeetsEligibility();
            if (!IsEligible)
            {
                ResetTimer();
                return;
            }

            if (dt <= 0f) return;
            eligibleElapsed += dt;
            if (eligibleElapsed < Mathf.Max(0.01f, rollInterval)) return;

            eligibleElapsed = 0f;
            if (random.Next(Mathf.Max(1, damageChanceDenominator)) != 0) return;
            if (!health.TryTakeDamage()) return;

            stress?.RegisterHit();
        }

        private void ResolveReferences()
        {
            health ??= GetComponent<CharacterHealth>();
            lightExposure ??= GetComponent<PlayerLightExposure>();
            stress ??= GetComponent<CharacterStress>();
            caveExit ??= GetComponent<CaveExit>();
        }

        private void RefreshHeldTorch(float dt)
        {
            torchRefreshElapsed += Mathf.Max(0f, dt);
            if (torchRefreshElapsed < Mathf.Max(0.1f, torchRefreshInterval)) return;

            torchRefreshElapsed = 0f;
            heldTorch = null;
            foreach (var candidate in FindObjectsByType<HandheldTorch>(FindObjectsSortMode.None))
            {
                if (!IsHeldByThisPlayer(candidate)) continue;
                heldTorch = candidate;
                break;
            }
        }

        private bool MeetsEligibility()
        {
            return isActiveAndEnabled && health != null && health.isActiveAndEnabled && !health.IsDead
                && (caveExit == null || !caveExit.IsCompleted)
                && lightExposure != null && lightExposure.isActiveAndEnabled
                && lightExposure.Exposure <= Mathf.Max(0f, fullDarknessExposure)
                && IsHeldBurnedOutTorch(heldTorch);
        }

        private bool IsHeldBurnedOutTorch(HandheldTorch torch)
        {
            return IsHeldByThisPlayer(torch) && torch.IsBurnedOut;
        }

        private bool IsHeldByThisPlayer(HandheldTorch torch)
        {
            return torch != null && torch.isActiveAndEnabled && torch.IsHeld && torch.Holder == transform;
        }

        private void ResetTimer()
        {
            eligibleElapsed = 0f;
            IsEligible = false;
        }

        private void OnValidate()
        {
            rollInterval = Mathf.Max(0.01f, rollInterval);
            damageChanceDenominator = Mathf.Max(1, damageChanceDenominator);
            fullDarknessExposure = Mathf.Max(0f, fullDarknessExposure);
            torchRefreshInterval = Mathf.Max(0.1f, torchRefreshInterval);
        }
    }
}
