using CardsUnity.Combat;
using CardsUnity.Config;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public sealed class CombatResultView : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private AnimConfig animConfig;

        public void Initialize(Text title, Text body, AnimConfig animationConfig = null)
        {
            titleText = title;
            bodyText = body;
            animConfig = animationConfig;
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
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

            PlayReveal();
        }

        public void Clear()
        {
            gameObject.SetActive(false);
            transform.DOKill();
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
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

        private void PlayReveal()
        {
            float duration = animConfig != null ? animConfig.ResolveFlashDuration : 0.45f;
            transform.DOKill();
            transform.localScale = Vector3.one * 0.98f;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                DOTween.To(() => canvasGroup.alpha, value => canvasGroup.alpha = value, 1f, duration * 0.45f).SetEase(Ease.OutQuad);
            }
            transform.DOScale(Vector3.one, duration).SetEase(Ease.OutBack);
        }
    }
}
