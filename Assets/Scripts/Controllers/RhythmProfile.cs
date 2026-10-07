using UnityEngine;

namespace CardsUnity.Controllers
{
    [CreateAssetMenu(menuName = "Cards Unity/Gameplay/Rhythm Profile", fileName = "Rhythm Profile")]
    public sealed class RhythmProfile : ScriptableObject
    {
        [SerializeField] private RhythmSettings settings = new RhythmSettings();
        public RhythmSettings Settings => settings;
        private void OnValidate() => settings.Validate();
    }
}
