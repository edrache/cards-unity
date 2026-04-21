using System.Collections.Generic;
using CardsUnity.Data;
using CardsUnity.Runtime;
using UnityEngine;

namespace CardsUnity.Controllers
{
    public sealed class RoundController : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        private GameState State => gameManager.State;

        public void Initialize(GameManager manager)
        {
            gameManager = manager;
        }

        public void DrawCards()
        {
            RoundFlow.DrawCards(State);
            gameManager.NotifyStateChanged();
        }

        public bool PlaceCard(CardInstance card, int slotIndex)
        {
            bool placed = RoundFlow.PlaceCard(State, card, slotIndex);
            if (placed) gameManager.NotifyStateChanged();
            return placed;
        }

        public bool UnplaceCard(int slotIndex)
        {
            bool unplaced = RoundFlow.UnplaceCard(State, slotIndex);
            if (unplaced) gameManager.NotifyStateChanged();
            return unplaced;
        }

        public bool CanResolve() => RoundFlow.CanResolve(State);

        public List<int> GetResolutionOrder() => RoundFlow.GetResolutionOrder(State);
    }
}
