using NUnit.Framework;

namespace CardsUnity.Tests
{
    public class SockSleepStateTests
    {
        [Test]
        public void InitialState_IsAwake()
        {
            var state = new SockSleepState(0.05f, 0.5f, 0.4f, 1f);

            Assert.AreEqual(SockSleepState.State.Awake, state.CurrentState);
        }

        [Test]
        public void Tick_WithSpeedAboveThreshold_NeverSleeps()
        {
            var state = new SockSleepState(0.05f, 0.5f, 0.4f, 0f);

            for (int i = 0; i < 100; i++)
                state.Tick(1f, 0.02f);

            Assert.AreEqual(SockSleepState.State.Awake, state.CurrentState);
        }

        [Test]
        public void Tick_WithSpeedBelowThresholdForFullDuration_TransitionsToAsleep()
        {
            var state = new SockSleepState(0.05f, 0.5f, 0.4f, 0f);

            bool sleptOnThisTick = false;
            for (int i = 0; i < 24; i++)
                sleptOnThisTick = state.Tick(0.01f, 0.02f);

            Assert.IsFalse(sleptOnThisTick);
            Assert.AreEqual(SockSleepState.State.Awake, state.CurrentState);

            sleptOnThisTick = state.Tick(0.01f, 0.02f);

            Assert.IsTrue(sleptOnThisTick);
            Assert.AreEqual(SockSleepState.State.Asleep, state.CurrentState);
        }

        [Test]
        public void Tick_SpeedSpikeResetsRestTimer()
        {
            var state = new SockSleepState(0.05f, 0.5f, 0.4f, 0f);

            for (int i = 0; i < 24; i++)
                state.Tick(0.01f, 0.02f);

            state.Tick(1f, 0.02f);

            for (int i = 0; i < 24; i++)
                state.Tick(0.01f, 0.02f);

            Assert.AreEqual(SockSleepState.State.Awake, state.CurrentState);
        }

        [Test]
        public void Tick_WhileAsleep_ReturnsFalseAndStaysAsleep()
        {
            var state = new SockSleepState(0.05f, 0.5f, 0.4f, 0f);

            for (int i = 0; i < 25; i++)
                state.Tick(0.01f, 0.02f);

            Assert.AreEqual(SockSleepState.State.Asleep, state.CurrentState);

            bool result = state.Tick(0.01f, 0.02f);

            Assert.IsFalse(result);
            Assert.AreEqual(SockSleepState.State.Asleep, state.CurrentState);
        }

        [Test]
        public void WakeUp_TransitionsToAwakeAndResetsRestTimer()
        {
            var state = new SockSleepState(0.05f, 0.5f, 0.4f, 0f);

            for (int i = 0; i < 25; i++)
                state.Tick(0.01f, 0.02f);

            Assert.AreEqual(SockSleepState.State.Asleep, state.CurrentState);

            state.WakeUp();

            Assert.AreEqual(SockSleepState.State.Awake, state.CurrentState);
        }

        [Test]
        public void WakeUp_WithGracePeriod_SuppressesSleepUntilElapsed()
        {
            var state = new SockSleepState(0.05f, 0.5f, 0.4f, 0.3f);

            state.WakeUp();

            for (int i = 0; i < 20; i++)
                state.Tick(0.01f, 0.02f);

            Assert.AreEqual(SockSleepState.State.Awake, state.CurrentState);

            for (int i = 0; i < 20; i++)
                state.Tick(0.01f, 0.02f);

            Assert.AreEqual(SockSleepState.State.Asleep, state.CurrentState);
        }

        [Test]
        public void WakeUp_WithZeroGracePeriod_AllowsSleepCheckImmediately()
        {
            var state = new SockSleepState(0.05f, 0.5f, 0.4f, 0f);

            state.WakeUp();

            for (int i = 0; i < 25; i++)
                state.Tick(0.01f, 0.02f);

            Assert.AreEqual(SockSleepState.State.Asleep, state.CurrentState);
        }
    }
}
