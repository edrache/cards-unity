using System;
using CardsUnity.Config;
using CardsUnity.Data;
using CardsUnity.Runtime;
using UnityEngine;

namespace CardsUnity.Controllers
{
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private GameConfig config;

        public GameState State { get; private set; }

        public event Action<GamePhase> OnPhaseChanged;
        public event Action            OnStateChanged;

        private void Awake()
        {
            if (config == null)
                config = Resources.Load<GameConfig>("Config/DefaultGameConfig");

            StartNewRun();
        }

        public void StartNewRun()
        {
            State = new GameState(config);

            CardData[] playerDefs = Resources.LoadAll<CardData>("CardDefinitions/Player");
            CardData[] enemyDefs  = Resources.LoadAll<CardData>("CardDefinitions/Enemy");

            State.PlayerDeck.AddRange(Data.DeckFactory.CreatePlayerDeck(playerDefs));
            State.EnemyDeck.AddRange(Data.DeckFactory.CreateEnemyDeck(enemyDefs));

            RoundFlow.FillEnemyBoard(State);

            NotifyStateChanged();
        }

        public void SetPhase(GamePhase phase)
        {
            if (State.Phase == phase) return;
            State.Phase = phase;
            OnPhaseChanged?.Invoke(phase);
            OnStateChanged?.Invoke();
        }

        public void NotifyStateChanged() => OnStateChanged?.Invoke();
    }
}
