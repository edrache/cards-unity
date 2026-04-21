using CardsUnity.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public sealed class DeckPanelView : MonoBehaviour
    {
        [SerializeField] private Text playerDeckText;
        [SerializeField] private Text playerCemeteryText;
        [SerializeField] private Text enemyDeckText;
        [SerializeField] private Text enemyCemeteryText;

        public void Initialize(Text playerDeck, Text playerCemetery, Text enemyDeck, Text enemyCemetery)
        {
            playerDeckText = playerDeck;
            playerCemeteryText = playerCemetery;
            enemyDeckText = enemyDeck;
            enemyCemeteryText = enemyCemetery;
        }

        public void Bind(GameState state)
        {
            if (state == null) return;

            SetText(playerDeckText, $"Player deck: {state.PlayerDeck.Count}");
            SetText(playerCemeteryText, $"Player cemetery: {state.PlayerCemetery.Count}");
            SetText(enemyDeckText, $"Enemy deck: {state.EnemyDeck.Count}");
            SetText(enemyCemeteryText, $"Enemy cemetery: {state.EnemyCemetery.Count}");
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
                target.text = value;
        }
    }
}
