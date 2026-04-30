using System.Collections.Generic;

namespace CardsUnity
{
    public class StoryDeckState
    {
        private readonly List<StoryCardDefinition> _sequence;
        private int _currentIndex;

        public StoryDeckDefinition Definition { get; }
        public StoryCardDefinition ActiveCard { get; private set; }
        public ClockState ActiveCardClock { get; private set; }

        public StoryDeckState(StoryDeckDefinition definition, System.Random rng = null)
        {
            Definition = definition;
            _sequence = new List<StoryCardDefinition>(definition.cards);

            if (definition.drawMode == StoryDrawMode.Random)
            {
                var r = rng ?? new System.Random();
                for (int i = _sequence.Count - 1; i > 0; i--)
                {
                    int j = r.Next(i + 1);
                    (_sequence[i], _sequence[j]) = (_sequence[j], _sequence[i]);
                }
            }

            _currentIndex = 0;
            SetActiveCard(_currentIndex < _sequence.Count ? _sequence[_currentIndex] : null);
        }

        public void AdvanceCard()
        {
            _currentIndex++;
            SetActiveCard(_currentIndex < _sequence.Count ? _sequence[_currentIndex] : null);
        }

        private void SetActiveCard(StoryCardDefinition card)
        {
            ActiveCard = card;
            ActiveCardClock = card != null ? new ClockState(card.clockMaxValue) : null;
        }
    }
}
