using CardsUnity.Data;
using UnityEngine;

namespace CardsUnity.Controllers
{
    public sealed class StepExecutor : MonoBehaviour
    {
        [SerializeField] private Transform leftFootTarget;
        [SerializeField] private Transform rightFootTarget;

        private StepRequest activeRequest;
        private float elapsed;

        public bool IsStepping => activeRequest.IsValid;

        public void BeginStep(StepRequest request)
        {
            activeRequest = request;
            elapsed = 0f;
        }

        private void Update()
        {
            if (!activeRequest.IsValid)
            {
                return;
            }

            var footTarget = activeRequest.Side == StepSide.Left ? leftFootTarget : rightFootTarget;
            if (footTarget == null)
            {
                activeRequest = StepRequest.Invalid;
                return;
            }

            elapsed += Time.deltaTime;
            var normalizedTime = Mathf.Clamp01(elapsed / activeRequest.Duration);
            var currentPosition = footTarget.position;
            var nextPosition = Vector3.Lerp(currentPosition, activeRequest.TargetPoint, normalizedTime);
            nextPosition.y += Mathf.Sin(normalizedTime * Mathf.PI) * activeRequest.StepHeight;
            footTarget.position = nextPosition;

            if (normalizedTime >= 1f)
            {
                footTarget.position = activeRequest.TargetPoint;
                activeRequest = StepRequest.Invalid;
            }
        }
    }
}
