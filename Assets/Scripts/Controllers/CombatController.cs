using System;
using System.Collections.Generic;
using CardsUnity.Combat;
using Random = System.Random;
using CardsUnity.Data;
using CardsUnity.Runtime;
using UnityEngine;

namespace CardsUnity.Controllers
{
    public sealed class CombatController : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        public event Action<CombatResult> OnCombatResult;

        public bool HasPendingSteps => _queue != null && _queue.Count > 0;

        private Queue<int> _queue;
        private Random     _rng;

        private void Awake()
        {
            _rng = new Random();
        }

        // Transitions to Combat phase and builds the ordered resolution queue.
        public void PrepareResolveRound()
        {
            _queue = CombatFlow.BuildQueue(gameManager.State);
            gameManager.SetPhase(GamePhase.Combat);
        }

        // Resolves the next column. Returns the result (or null if queue exhausted).
        // Fires OnCombatResult and notifies listeners via GameManager.
        public CombatResult ResolveCombatStep()
        {
            CombatResult result = CombatFlow.ResolveStep(gameManager.State, _queue, _rng);
            if (result != null)
            {
                OnCombatResult?.Invoke(result);
                gameManager.NotifyStateChanged();
            }
            return result;
        }

        // Cleans up after all steps, refills the enemy board, and advances the phase.
        public void FinalizeResolveRound()
        {
            CombatFlow.FinalizeRound(gameManager.State);
            GamePhase next = CombatFlow.DetermineNextPhase(gameManager.State);
            gameManager.SetPhase(next);
        }

        // Allows tests or deterministic replays to inject a seeded RNG.
        internal void SetRng(Random rng) => _rng = rng ?? throw new ArgumentNullException(nameof(rng));
    }
}
