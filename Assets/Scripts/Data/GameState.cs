using System;
using System.Collections.Generic;
using CardsUnity.Config;

namespace CardsUnity.Data
{
    public sealed class GameState
    {
        public GameState(GameConfig config)
            : this(config != null ? config.SlotCount : throw new ArgumentNullException(nameof(config)))
        {
        }

        public GameState(int slotCount)
        {
            if (slotCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount), slotCount, "Slot count must be at least 1.");
            }

            Phase = GamePhase.Draw;
            Round = 1;
            PlayerDeck = new List<CardInstance>();
            PlayerHand = new List<CardInstance>();
            PlayerCemetery = new List<CardInstance>();
            PlayerBoard = new CardInstance[slotCount];
            EnemyDeck = new List<CardInstance>();
            EnemyCemetery = new List<CardInstance>();
            EnemyBoard = new CardInstance[slotCount];
            PlacementOrder = new List<CardInstance>();
            RewardChoices = new List<CardInstance>();
        }

        public GamePhase Phase { get; set; }
        public int Round { get; set; }
        public List<CardInstance> PlayerDeck { get; }
        public List<CardInstance> PlayerHand { get; }
        public List<CardInstance> PlayerCemetery { get; }
        public CardInstance[] PlayerBoard { get; }
        public List<CardInstance> EnemyDeck { get; }
        public List<CardInstance> EnemyCemetery { get; }
        public CardInstance[] EnemyBoard { get; }
        public List<CardInstance> PlacementOrder { get; }
        public List<CardInstance> RewardChoices { get; }
    }
}
