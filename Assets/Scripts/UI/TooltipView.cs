using CardsUnity.Combat;
using CardsUnity.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public sealed class TooltipView : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;

        public void Initialize(Text title, Text body)
        {
            titleText = title;
            bodyText = body;
            Hide();
        }

        public void ShowPreview(int slotIndex, CardInstance playerCard, CardInstance enemyCard, CombatPreview preview)
        {
            if (playerCard == null || enemyCard == null || preview == null)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);
            if (titleText != null)
                titleText.text = $"Column {slotIndex + 1}: {preview.RpsResult}";

            if (bodyText != null)
            {
                bodyText.text =
                    $"{playerCard.CardName} vs {enemyCard.CardName}\n" +
                    $"Player range {preview.PlayerEffectiveRange} (+{preview.PlayerSupportBonus}) / Enemy range {preview.EnemyEffectiveRange} (+{preview.EnemySupportBonus})\n" +
                    $"Player damage {preview.PlayerMinDamageDealt}-{preview.PlayerMaxDamageDealt}; Enemy damage {preview.EnemyMinDamageDealt}-{preview.EnemyMaxDamageDealt}\n" +
                    $"Risk: player {(preview.PlayerCanDie ? "can die" : "survives max hit")}; enemy {(preview.EnemyCanDie ? "can die" : "survives max hit")}";
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
