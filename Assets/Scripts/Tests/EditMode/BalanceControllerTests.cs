using CardsUnity.Config;
using CardsUnity.Controllers;
using CardsUnity.Data;
using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests.EditMode
{
    public sealed class BalanceControllerTests
    {
        [Test]
        public void EvaluateState_ReturnsNeedsStep_WhenDistanceExceedsStepThreshold()
        {
            var tuning = ScriptableObject.CreateInstance<BalanceTuning>();
            tuning.recoverDistance = 0.1f;
            tuning.stepDistance = 0.3f;
            tuning.fallingDistance = 0.6f;
            tuning.stableVelocityThreshold = 0.1f;

            var state = BalanceController.EvaluateState(0.4f, 0f, tuning);

            Assert.That(state, Is.EqualTo(BalanceState.NeedsStep));
            Object.DestroyImmediate(tuning);
        }

        [Test]
        public void EvaluateState_ReturnsRecovering_WhenVelocityExceedsStableThreshold()
        {
            var tuning = ScriptableObject.CreateInstance<BalanceTuning>();
            tuning.recoverDistance = 0.1f;
            tuning.stepDistance = 0.3f;
            tuning.fallingDistance = 0.6f;
            tuning.stableVelocityThreshold = 0.1f;

            var state = BalanceController.EvaluateState(0.05f, 0.2f, tuning);

            Assert.That(state, Is.EqualTo(BalanceState.Recovering));
            Object.DestroyImmediate(tuning);
        }
    }
}
