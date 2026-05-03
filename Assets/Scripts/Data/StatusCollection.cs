using System;
using System.Collections.Generic;

namespace CardsUnity
{
    public class StatusCollection
    {
        private readonly List<StatusState> _statuses = new();

        public IReadOnlyList<StatusState> Statuses => _statuses;

        public void Add(StatusDefinition definition)
        {
            if (definition == null)
                return;

            _statuses.Add(new StatusState(definition));
        }

        public void Remove(StatusState status)
        {
            status?.Remove();
        }

        public void EvaluateAndTick(Action<StatusState> evaluate)
        {
            if (evaluate == null)
                throw new ArgumentNullException(nameof(evaluate));

            var snapshot = new List<StatusState>(_statuses);

            foreach (var status in snapshot)
            {
                if (!status.IsExpired)
                    evaluate(status);
            }

            foreach (var status in snapshot)
            {
                if (!status.IsExpired)
                    status.TickTurn();
            }

            _statuses.RemoveAll(status => status.IsExpired);
        }
    }
}
