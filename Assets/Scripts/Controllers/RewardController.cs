using System;
using CardsUnity.Data;
using CardsUnity.Runtime;
using UnityEngine;
using Random = System.Random;

namespace CardsUnity.Controllers
{
    public sealed class RewardController : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        public event Action OnRewardReady;

        private Random _rng;

        private void Awake()
        {
            _rng = new Random();
        }

        public void Initialize(GameManager manager)
        {
            gameManager = manager;
        }

        // Generates 3 reward choices and transitions to Reward phase.
        public void GenerateRewardChoices()
        {
            RewardFlow.GenerateRewardChoices(gameManager.State, _rng);
            gameManager.SetPhase(GamePhase.Reward);
            OnRewardReady?.Invoke();
        }

        // Adds the chosen card to the player deck and starts a new encounter.
        public void SelectRewardAndStartEncounter(CardInstance chosen)
        {
            RewardFlow.SelectReward(gameManager.State, chosen);

            CardData[] enemyDefs = Resources.LoadAll<CardData>("CardDefinitions/Enemy");
            RewardFlow.SetupNewEncounter(gameManager.State, enemyDefs, _rng);

            gameManager.SetPhase(GamePhase.Draw);
        }

        // Allows tests or deterministic replays to inject a seeded RNG.
        internal void SetRng(Random rng) => _rng = rng ?? throw new ArgumentNullException(nameof(rng));
    }
}
