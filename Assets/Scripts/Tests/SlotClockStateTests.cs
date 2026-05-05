using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class SlotClockStateTests
    {
        [Test]
        public void Increment_increases_current_value()
        {
            var clock = new SlotClockState(maxValue: 5);

            clock.Increment(2);

            Assert.AreEqual(2, clock.CurrentValue);
        }

        [Test]
        public void IsFull_true_when_current_equals_max()
        {
            var clock = new SlotClockState(maxValue: 3);

            clock.Increment(3);

            Assert.IsTrue(clock.IsFull);
        }

        [Test]
        public void IsFull_true_when_current_exceeds_max()
        {
            var clock = new SlotClockState(maxValue: 3);

            clock.Increment(5);

            Assert.IsTrue(clock.IsFull);
        }

        [Test]
        public void IsFull_false_when_below_max()
        {
            var clock = new SlotClockState(maxValue: 3);

            clock.Increment(2);

            Assert.IsFalse(clock.IsFull);
        }

        [Test]
        public void Progress_returns_correct_ratio()
        {
            var clock = new SlotClockState(maxValue: 4);

            clock.Increment(2);

            Assert.AreEqual(0.5f, clock.Progress, 0.001f);
        }

        [Test]
        public void Progress_is_zero_on_fresh_clock()
        {
            var clock = new SlotClockState(maxValue: 5);

            Assert.AreEqual(0f, clock.Progress, 0.001f);
        }

        [Test]
        public void SlotState_builds_clock_state_from_definition_clock()
        {
            var definition = MakeSlotDefinition(maxValue: 4);

            var slot = new SlotState(definition);

            Assert.IsNotNull(slot.ClockState);
            Assert.AreEqual(4, slot.ClockState.MaxValue);
            Assert.AreEqual(0, slot.ClockState.CurrentValue);
        }

        [Test]
        public void SlotState_omits_clock_state_when_definition_has_no_clock()
        {
            var definition = ScriptableObject.CreateInstance<SlotDefinition>();

            var slot = new SlotState(definition);

            Assert.IsNull(slot.ClockState);
        }

        [Test]
        public void SetDefinition_rebuilds_clock_state_from_new_definition()
        {
            var slot = new SlotState(MakeSlotDefinition(maxValue: 2));
            slot.ClockState.Increment(2);

            slot.SetDefinition(MakeSlotDefinition(maxValue: 5));

            Assert.IsNotNull(slot.ClockState);
            Assert.AreEqual(5, slot.ClockState.MaxValue);
            Assert.AreEqual(0, slot.ClockState.CurrentValue);
        }

        [Test]
        public void SetDefinition_clears_clock_state_when_new_definition_disables_clock()
        {
            var slot = new SlotState(MakeSlotDefinition(maxValue: 2));

            slot.SetDefinition(MakeSlotDefinition(maxValue: 0));

            Assert.IsNull(slot.ClockState);
        }

        private static SlotDefinition MakeSlotDefinition(int maxValue)
        {
            var definition = ScriptableObject.CreateInstance<SlotDefinition>();
            definition.clock = new SlotClockConfig
            {
                maxValue = maxValue,
                completionEffect = SlotClockCompletionEffect.RemoveSlot
            };
            return definition;
        }
    }
}
