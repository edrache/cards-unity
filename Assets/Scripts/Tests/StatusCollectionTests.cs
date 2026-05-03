using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class StatusCollectionTests
    {
        private static StatusDefinition MakeDefinition(
            StatusDurationType durationType,
            int turns = 3,
            SlotEffectAction action = SlotEffectAction.AddToClock,
            int value = 1)
        {
            var definition = ScriptableObject.CreateInstance<StatusDefinition>();
            definition.durationType = durationType;
            definition.durationTurns = turns;
            definition.action = action;
            definition.actionValue = value;
            return definition;
        }

        [Test]
        public void Add_increases_statuses_count()
        {
            var collection = new StatusCollection();

            collection.Add(MakeDefinition(StatusDurationType.Turns));

            Assert.AreEqual(1, collection.Statuses.Count);
        }

        [Test]
        public void EvaluateAndTick_calls_callback_once_per_active_status()
        {
            var collection = new StatusCollection();
            collection.Add(MakeDefinition(StatusDurationType.Turns, 2));
            collection.Add(MakeDefinition(StatusDurationType.Turns, 2));
            int callCount = 0;

            collection.EvaluateAndTick(_ => callCount++);

            Assert.AreEqual(2, callCount);
        }

        [Test]
        public void EvaluateAndTick_does_not_call_callback_for_already_expired_status()
        {
            var collection = new StatusCollection();
            collection.Add(MakeDefinition(StatusDurationType.Turns, 1));
            collection.Statuses[0].Remove();
            int callCount = 0;

            collection.EvaluateAndTick(_ => callCount++);

            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void EvaluateAndTick_removes_status_with_one_turn_remaining_after_tick()
        {
            var collection = new StatusCollection();
            collection.Add(MakeDefinition(StatusDurationType.Turns, 1));

            collection.EvaluateAndTick(_ => { });

            Assert.AreEqual(0, collection.Statuses.Count);
        }

        [Test]
        public void EvaluateAndTick_keeps_status_with_multiple_turns_remaining()
        {
            var collection = new StatusCollection();
            collection.Add(MakeDefinition(StatusDurationType.Turns, 3));

            collection.EvaluateAndTick(_ => { });

            Assert.AreEqual(1, collection.Statuses.Count);
        }

        [Test]
        public void EvaluateAndTick_does_not_remove_permanent_status()
        {
            var collection = new StatusCollection();
            collection.Add(MakeDefinition(StatusDurationType.Permanent));

            collection.EvaluateAndTick(_ => { });
            collection.EvaluateAndTick(_ => { });

            Assert.AreEqual(1, collection.Statuses.Count);
        }

        [Test]
        public void Remove_immediately_excludes_status_from_subsequent_evaluation()
        {
            var collection = new StatusCollection();
            collection.Add(MakeDefinition(StatusDurationType.Turns, 5));
            var status = collection.Statuses[0];

            collection.Remove(status);

            int callCount = 0;
            collection.EvaluateAndTick(_ => callCount++);

            Assert.AreEqual(0, callCount);
        }
    }
}
