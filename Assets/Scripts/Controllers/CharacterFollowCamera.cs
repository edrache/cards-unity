using UnityEngine;

namespace CardsUnity.Controllers
{
    public sealed class CharacterFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 7f, -8f);
        [SerializeField] private float smoothing = 8f;

        private Vector3 appliedShake;
        private float shakeRemaining;
        private float shakeDuration;
        private float shakeAmplitude;
        private float shakeFrequency;

        /// <summary>Refreshes a bounded impulse without stacking amplitudes from simultaneous rocks.</summary>
        public void Shake(float amplitude, float duration, float frequency)
        {
            if (amplitude <= 0f || duration <= 0f || !isActiveAndEnabled) return;
            float current = shakeDuration > 0f ? shakeAmplitude * shakeRemaining / shakeDuration : 0f;
            shakeAmplitude = Mathf.Max(current, amplitude);
            shakeDuration = shakeRemaining = Mathf.Max(shakeRemaining, duration);
            shakeFrequency = Mathf.Max(0.1f, frequency);
        }

        private void OnDisable()
        {
            transform.position -= appliedShake;
            appliedShake = Vector3.zero;
            shakeRemaining = 0f;
        }

        private void LateUpdate()
        {
            // Remove the previous visual offset so it never feeds back into follow smoothing.
            transform.position -= appliedShake;
            appliedShake = Vector3.zero;
            if (target == null) return;
            transform.position = Vector3.Lerp(transform.position, target.position + offset,
                1f - Mathf.Exp(-smoothing * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation(-offset + Vector3.up);
            if (shakeRemaining <= 0f) return;
            shakeRemaining = Mathf.Max(0f, shakeRemaining - Time.deltaTime);
            float envelope = shakeRemaining / shakeDuration;
            float phase = Time.time * shakeFrequency;
            Vector3 noise = new Vector3(Mathf.PerlinNoise(phase, 0.37f) * 2f - 1f,
                Mathf.PerlinNoise(0.73f, phase) * 2f - 1f, 0f);
            appliedShake = transform.rotation * noise * (shakeAmplitude * envelope * envelope);
            transform.position += appliedShake;
        }
    }
}
