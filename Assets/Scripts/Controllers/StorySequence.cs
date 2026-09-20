using System;
using UnityEngine;

namespace CardsUnity.Controllers
{
    [CreateAssetMenu(fileName = "StorySequence", menuName = "Cards Unity/Story Sequence")]
    public sealed class StorySequence : ScriptableObject
    {
        [TextArea(8, 24)]
        [SerializeField] private string text = "This is not a story about heroes.\nThere is no evil here to defeat.\nNo kingdom to save.\nNo prophecy waiting to be fulfilled.\nThe people who descend into the earth are not adventurers.\nThey are debtors.\nWidows.\nCriminals.\nThe sick.\nThe abandoned.\nPeople who have already lost more than they can afford to lose.\nSomewhere below, there are things worth more than a lifetime of honest work.\nMost who go looking for them never return.\nSome people consider that an acceptable risk.\n<b>These are their stories.</b>";

        [Min(0f)] [SerializeField] private float charactersPerSecond = 30f;
        [Min(0f)] [SerializeField] private float characterFadeDuration = 0.08f;
        [Min(0f)] [SerializeField] private float lineHoldDuration = 1.5f;
        [Min(0f)] [SerializeField] private float lineFadeDuration = 0.5f;
        [Min(0f)] [SerializeField] private float acceleratedCharactersPerSecond = 120f;

        public string Text => text ?? string.Empty;
        public float CharactersPerSecond => charactersPerSecond;
        public float CharacterFadeDuration => characterFadeDuration;
        public float LineHoldDuration => lineHoldDuration;
        public float LineFadeDuration => lineFadeDuration;
        public float AcceleratedCharactersPerSecond => acceleratedCharactersPerSecond;

        public string[] GetLines()
        {
            return Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }
    }
}
