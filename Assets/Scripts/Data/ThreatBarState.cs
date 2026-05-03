using System;
using System.Collections.Generic;

namespace CardsUnity
{
    public class ThreatTierState
    {
        public int MaxValue { get; }
        public int CurrentValue { get; private set; }
        public float Progress => MaxValue > 0 ? (float)CurrentValue / MaxValue : 0f;
        public bool IsDepleted => CurrentValue <= 0;

        public ThreatTierState(int maxValue)
        {
            if (maxValue < 1)
                throw new ArgumentOutOfRangeException(nameof(maxValue), "Threat tier max value must be at least 1.");

            MaxValue = maxValue;
            CurrentValue = maxValue;
        }

        public int Drain(int amount)
        {
            if (amount <= 0)
                return 0;

            int absorbed = Math.Min(amount, CurrentValue);
            CurrentValue -= absorbed;
            return amount - absorbed;
        }
    }

    public class ThreatBarState
    {
        private readonly List<ThreatTierState> _segments;
        private bool _hasReachedZero;

        public event Action<int> OnTierDepleted;
        public event Action OnReachedZero;

        public IReadOnlyList<ThreatTierState> Segments => _segments;
        public bool IsAtZero => _segments.Count == 0 || _segments.TrueForAll(segment => segment.IsDepleted);

        public ThreatBarState(IReadOnlyList<ThreatTierDefinition> tierDefinitions)
        {
            if (tierDefinitions == null)
                throw new ArgumentNullException(nameof(tierDefinitions));

            _segments = new List<ThreatTierState>(tierDefinitions.Count);

            for (int i = 0; i < tierDefinitions.Count; i++)
            {
                ThreatTierDefinition definition = tierDefinitions[i];
                if (definition == null)
                    throw new ArgumentException("Threat tier definitions cannot contain null entries.", nameof(tierDefinitions));

                _segments.Add(new ThreatTierState(definition.maxValue));
            }

            _hasReachedZero = IsAtZero;
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0 || _segments.Count == 0 || _hasReachedZero)
                return;

            int remainingDamage = amount;
            for (int i = 0; i < _segments.Count && remainingDamage > 0; i++)
            {
                ThreatTierState segment = _segments[i];
                if (segment.IsDepleted)
                    continue;

                remainingDamage = segment.Drain(remainingDamage);

                if (segment.IsDepleted)
                    OnTierDepleted?.Invoke(i);
            }

            if (!_hasReachedZero && IsAtZero)
            {
                _hasReachedZero = true;
                OnReachedZero?.Invoke();
            }
        }
    }
}
