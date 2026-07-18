using CardsUnity.Config;
using CardsUnity.Data;
using UnityEngine;

namespace CardsUnity.Controllers
{
    public sealed class StepPlanner : MonoBehaviour
    {
        [SerializeField] private StepTuning tuning;

        public bool TryCreateStepRequest(
            Vector3 centerOfMass,
            Vector3 supportCenter,
            Vector3 leftFoot,
            Vector3 rightFoot,
            Vector3 velocity,
            out StepRequest request)
        {
            request = PlanStep(centerOfMass, supportCenter, leftFoot, rightFoot, velocity, tuning);
            return request.IsValid;
        }

        public static StepRequest PlanStep(
            Vector3 centerOfMass,
            Vector3 supportCenter,
            Vector3 leftFoot,
            Vector3 rightFoot,
            Vector3 velocity,
            StepTuning tuning)
        {
            if (tuning == null)
            {
                return StepRequest.Invalid;
            }

            var horizontalOffset = new Vector3(
                centerOfMass.x - supportCenter.x,
                0f,
                centerOfMass.z - supportCenter.z);

            var offsetMagnitude = horizontalOffset.magnitude;
            if (offsetMagnitude < tuning.triggerDistance)
            {
                return StepRequest.Invalid;
            }

            var stepSide = SelectStepSide(centerOfMass.x, leftFoot.x, rightFoot.x);
            var origin = stepSide == StepSide.Left ? leftFoot : rightFoot;
            var direction = (horizontalOffset + velocity * 0.2f);
            if (direction.sqrMagnitude < Mathf.Epsilon)
            {
                direction = Vector3.forward;
            }

            direction.y = 0f;
            direction.Normalize();

            var travelDistance = Mathf.Min(offsetMagnitude, tuning.maxStepDistance);
            var targetPoint = origin + direction * travelDistance;
            targetPoint.y = supportCenter.y;

            return new StepRequest(stepSide, targetPoint, tuning.stepDuration, tuning.stepHeight);
        }

        public static StepSide SelectStepSide(float centerOfMassX, float leftFootX, float rightFootX)
        {
            var midPoint = (leftFootX + rightFootX) * 0.5f;
            return centerOfMassX >= midPoint ? StepSide.Right : StepSide.Left;
        }
    }
}
