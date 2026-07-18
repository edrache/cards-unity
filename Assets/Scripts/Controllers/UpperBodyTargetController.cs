using UnityEngine;

namespace CardsUnity.Controllers
{
    public sealed class UpperBodyTargetController : MonoBehaviour
    {
        [SerializeField] private Transform headTarget;
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform rightHandTarget;
        [SerializeField] private float targetWeight = 1f;

        public Transform HeadTarget => headTarget;
        public Transform LeftHandTarget => leftHandTarget;
        public Transform RightHandTarget => rightHandTarget;
        public float TargetWeight => Mathf.Clamp01(targetWeight);
    }
}
