using UnityEngine;

namespace CardsUnity.Controllers
{
    public sealed class CharacterFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 7f, -8f);
        [SerializeField] private float smoothing = 8f;

        private void LateUpdate()
        {
            if (target == null) return;
            transform.position = Vector3.Lerp(transform.position, target.position + offset,
                1f - Mathf.Exp(-smoothing * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation(-offset + Vector3.up);
        }
    }
}
