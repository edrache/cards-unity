namespace CardsUnity
{
    public class SockSleepState
    {
        public enum State
        {
            Awake,
            Asleep
        }

        public State CurrentState { get; private set; } = State.Awake;

        public float WakeUpRadius { get; }

        private readonly float sleepVelocityThreshold;
        private readonly float minRestTimeBeforeSleep;
        private readonly float minActiveTimeAfterWake;

        private float restTime;
        private float graceTimeRemaining;

        public SockSleepState(
            float sleepVelocityThreshold,
            float minRestTimeBeforeSleep,
            float wakeUpRadius,
            float minActiveTimeAfterWake)
        {
            this.sleepVelocityThreshold = sleepVelocityThreshold;
            this.minRestTimeBeforeSleep = minRestTimeBeforeSleep;
            WakeUpRadius = wakeUpRadius;
            this.minActiveTimeAfterWake = minActiveTimeAfterWake;
        }

        public bool Tick(float maxParticleSpeed, float deltaTime)
        {
            if (CurrentState == State.Asleep)
                return false;

            if (graceTimeRemaining > 0f)
            {
                graceTimeRemaining = UnityEngine.Mathf.Max(0f, graceTimeRemaining - deltaTime);
                restTime = 0f;
                return false;
            }

            if (maxParticleSpeed < sleepVelocityThreshold)
                restTime += deltaTime;
            else
                restTime = 0f;

            if (restTime < minRestTimeBeforeSleep)
                return false;

            CurrentState = State.Asleep;
            return true;
        }

        public void WakeUp()
        {
            CurrentState = State.Awake;
            restTime = 0f;
            graceTimeRemaining = minActiveTimeAfterWake;
        }
    }
}
