using CardsUnity.Combat;
using CardsUnity.Config;
using CardsUnity.Data;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public sealed class TooltipView : MonoBehaviour
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

            PlayShow();
        }

        public void Hide()
        {
            transform.DOKill();
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        private void PlayShow()
        {
            float duration = animConfig != null ? animConfig.ResolvePauseDuration : 0.22f;
            transform.DOKill();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                DOTween.To(() => canvasGroup.alpha, value => canvasGroup.alpha = value, 1f, duration).SetEase(Ease.OutQuad);
            }
        }
    }
}
