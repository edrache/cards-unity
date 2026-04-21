using CardsUnity.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public sealed class CombatResultView : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;

        public void Initialize(Text title, Text body)
        {
            titleText = title;
            bodyText = body;
            Clear();
        }

        public void Show(CombatResult result)
        {
            if (result == null)
            {
                Clear();
                return;
            }

            gameObject.SetActive(true);
            if (titleText != null)
                titleText.text = $"Column {result.SlotIndex + 1}: {result.RpsResult}";

            if (bodyText != null)
            {
                bodyText.text =
                    $"Player rolls {FormatRolls(result.PlayerRoll1, result.PlayerRoll2)} -> {result.ChosenPlayerRoll} / Enemy rolls {FormatRolls(result.EnemyRoll1, result.EnemyRoll2)} -> {result.ChosenEnemyRoll}\n" +
                    $"Ranges P {result.PlayerEffectiveRange} (+{result.PlayerSupportBonus}) / E {result.EnemyEffectiveRange} (+{result.EnemySupportBonus})\n" +
                    $"Damage P->{result.PlayerDamageDealt} E->{result.EnemyDamageDealt} | Values P {result.PlayerValueBefore}->{result.PlayerValueAfter} E {result.EnemyValueBefore}->{result.EnemyValueAfter}\n" +
                    $"{FormatDeath(result)}";
            }
        }

        public void Clear()
        {
            gameObject.SetActive(false);
            if (titleText != null)
                titleText.text = string.Empty;
            if (bodyText != null)
                bodyText.text = string.Empty;
        }

        private static string FormatRolls(int roll1, int roll2)
        {
            return roll2 > 0 ? $"{roll1}, {roll2}" : roll1.ToString();
        }

        private static string FormatDeath(CombatResult result)
        {
            if (result.PlayerDied && result.EnemyDied)
                return "Both cards defeated";
            if (result.PlayerDied)
                return "Player card defeated";
            if (result.EnemyDied)
                return "Enemy card defeated";
            return "Both cards survived";
        }
    }
}
