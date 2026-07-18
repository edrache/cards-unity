using CardsUnity.Config;
using CardsUnity.Controllers;
using CardsUnity.Data;
using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests.EditMode
{
    public sealed class StepPlannerTests
    {
        [Test]
        public void PlanStep_ReturnsInvalidRequest_BelowTriggerDistance()
        {
            var tuning = ScriptableObject.CreateInstance<StepTuning>();
            tuning.triggerDistance = 0.5f;
            tuning.maxStepDistance = 1f;
            tuning.stepDuration = 0.25f;
            tuning.stepHeight = 0.2f;

            var request = StepPlanner.PlanStep(
                new Vector3(0.1f, 0f, 0f),
                Vector3.zero,
                new Vector3(-0.2f, 0f, 0f),
                new Vector3(0.2f, 0f, 0f),
                Vector3.zero,
                tuning);

            Assert.That(request.IsValid, Is.False);
            Object.DestroyImmediate(tuning);
        }

        [Test]
        public void PlanStep_SelectsRightFoot_WhenCenterOfMassLeansRight()
        {
            var tuning = ScriptableObject.CreateInstance<StepTuning>();
            tuning.triggerDistance = 0.2f;
            tuning.maxStepDistance = 1f;
            tuning.stepDuration = 0.25f;
            tuning.stepHeight = 0.2f;

            var request = StepPlanner.PlanStep(
                new Vector3(0.6f, 0f, 0.4f),
                Vector3.zero,
                new Vector3(-0.2f, 0f, 0f),
                new Vector3(0.2f, 0f, 0f),
                Vector3.zero,
                tuning);

            Assert.That(request.IsValid, Is.True);
            Assert.That(request.Side, Is.EqualTo(StepSide.Right));
            Assert.That(request.TargetPoint.z, Is.GreaterThan(0f));
            Object.DestroyImmediate(tuning);
        }
    }
}
