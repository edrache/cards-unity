using TMPro;
using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Prefab-owned end screen presentation; gameplay only supplies its text.</summary>
    [DisallowMultipleComponent]
    public sealed class CaveSummaryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text result;

        public void Show(string titleText, string resultText)
        {
            if (title != null) title.text = titleText;
            if (result != null) result.text = resultText;
            gameObject.SetActive(true);
        }
    }
}
