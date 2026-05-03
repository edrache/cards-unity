using System;

namespace CardsUnity
{
    public class StatusState
    {
        public StatusDefinition Definition { get; }
        public int RemainingTurns { get; private set; }
        public bool IsExpired { get; private set; }

        public StatusState(StatusDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            RemainingTurns = definition.durationTurns;
        }

        public void TickTurn()
        {
            if (IsExpired || Definition.durationType == StatusDurationType.Permanent)
                return;

            RemainingTurns--;
            if (RemainingTurns <= 0)
                IsExpired = true;
        }

        public void Remove()
        {
            IsExpired = true;
        }
    }
}
