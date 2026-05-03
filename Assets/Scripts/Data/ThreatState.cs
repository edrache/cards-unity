using System;
using System.Collections.Generic;

namespace CardsUnity
{
    public class ThreatState
    {
        private readonly ThreatBarState _force;
        private readonly ThreatBarState _presence;
        private readonly ThreatBarState _wit;

        public event Action<CardType, int> OnTierDepleted;
        public event Action<CardType> OnBarAtZero;

        public bool AnyAtZero => _force.IsAtZero || _presence.IsAtZero || _wit.IsAtZero;

        public ThreatState(
            IReadOnlyList<ThreatTierDefinition> forceTiers,
            IReadOnlyList<ThreatTierDefinition> witTiers,
            IReadOnlyList<ThreatTierDefinition> presenceTiers)
        {
            _force = new ThreatBarState(forceTiers);
            _wit = new ThreatBarState(witTiers);
            _presence = new ThreatBarState(presenceTiers);

            Wire(_force, CardType.Force);
            Wire(_presence, CardType.Presence);
            Wire(_wit, CardType.Wit);
        }

        public ThreatBarState GetBar(CardType type)
        {
            return type switch
            {
                CardType.Force => _force,
                CardType.Presence => _presence,
                CardType.Wit => _wit,
                _ => null
            };
        }

        private void Wire(ThreatBarState bar, CardType type)
        {
            bar.OnTierDepleted += tierIndex => OnTierDepleted?.Invoke(type, tierIndex);
            bar.OnReachedZero += () => OnBarAtZero?.Invoke(type);
        }
    }
}
