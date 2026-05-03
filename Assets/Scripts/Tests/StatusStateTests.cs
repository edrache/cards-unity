using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class StatusStateTests
    {
        private static StatusDefinition MakeDefinition(StatusDurationType durationType, int turns = 3)
        {
            var definition = ScriptableObject.CreateInstance<StatusDefinition>();
            definition.durationType = durationType;
            definition.durationTurns = turns;
            return definition;
        }

        [Test]
        public void New_turn_based_status_has_correct_remaining_turns()
        {
            var state = new StatusState(MakeDefinition(StatusDurationType.Turns, 3));

            Assert.AreEqual(3, state.RemainingTurns);
        }

        [Test]
        public void New_status_is_not_expired()
        {
            var state = new StatusState(MakeDefinition(StatusDurationType.Turns, 2));

            Assert.IsFalse(state.IsExpired);
        }

        [Test]
        public void TickTurn_decrements_remaining_turns_by_one()
        {
            var state = new StatusState(MakeDefinition(StatusDurationType.Turns, 3));

            state.TickTurn();

            Assert.AreEqual(2, state.RemainingTurns);
        }

        [Test]
        public void TickTurn_sets_is_expired_when_remaining_turns_reach_zero()
        {
            var state = new StatusState(MakeDefinition(StatusDurationType.Turns, 1));

            state.TickTurn();

            Assert.IsTrue(state.IsExpired);
        }

        [Test]
        public void TickTurn_on_permanent_status_does_not_expire_it()
        {
            var state = new StatusState(MakeDefinition(StatusDurationType.Permanent));

            state.TickTurn();

            Assert.IsFalse(state.IsExpired);
        }

        [Test]
        public void Remove_sets_is_expired_immediately()
        {
            var state = new StatusState(MakeDefinition(StatusDurationType.Turns, 10));

            state.Remove();

            Assert.IsTrue(state.IsExpired);
        }

        [Test]
        public void Definition_is_accessible_on_state()
        {
            var definition = MakeDefinition(StatusDurationType.Permanent);
            var state = new StatusState(definition);

            Assert.AreSame(definition, state.Definition);
        }
    }
}
