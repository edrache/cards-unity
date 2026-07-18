using CardsUnity.Config;
using CardsUnity.Data;
using CardsUnity.Runtime;
using UnityEngine;

namespace CardsUnity.Controllers
{
    public sealed class BalanceController : MonoBehaviour
    {
        [SerializeField] private BalanceTuning tuning;
        [SerializeField] private CenterOfMassTracker centerOfMassTracker;

        public BalanceState CurrentState { get; private set; } = BalanceState.Stable;

        public void EvaluateBalance(float supportDistance, float horizontalVelocity)
        {
            CurrentState = EvaluateState(supportDistance, horizontalVelocity, tuning);
        }

        public void EvaluateBalance(Vector3 supportCenter)
        {
            if (centerOfMassTracker == null)
            {
                CurrentState = BalanceState.Stable;
                return;
            }

            var delta = centerOfMassTracker.WorldCenterOfMass - supportCenter;
            EvaluateBalance(new Vector2(delta.x, delta.z).magnitude, centerOfMassTracker.HorizontalSpeed);
        }

        public static BalanceState EvaluateState(float supportDistance, float horizontalVelocity, BalanceTuning tuning)
        {
            if (tuning == null)
            {
                return BalanceState.Stable;
            }

            if (supportDistance >= tuning.fallingDistance)
            {
                return BalanceState.Falling;
            }

            if (supportDistance >= tuning.stepDistance)
            {
                return BalanceState.NeedsStep;
            }

            if (supportDistance >= tuning.recoverDistance || Mathf.Abs(horizontalVelocity) > tuning.stableVelocityThreshold)
            {
                return BalanceState.Recovering;
            }

            return BalanceState.Stable;
        }
    }
}
