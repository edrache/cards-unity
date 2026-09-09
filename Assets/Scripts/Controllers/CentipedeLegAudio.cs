using UnityEngine;
using UnityEngine.Audio;

namespace CardsUnity.Controllers
{
    // Chitinous clicks for every individual leg contact. Playback is gated on the distance to the
    // player, so a cave full of creatures neither floods the mix nor spends voices out of earshot.
    [RequireComponent(typeof(ProceduralCentipede))]
    public sealed class CentipedeLegAudio : MonoBehaviour
    {
        [Header("Clip")]
        [SerializeField] private AudioClip legStep;
        [Tooltip("Optional mixer group for every leg voice.")]
        [SerializeField] private AudioMixerGroup output;

        [Header("Hearing range")]
        [Tooltip("Distance to the player within which leg clicks play at their full volume.")]
        [SerializeField, Min(0f)] private float fullVolumeDistance = 4f;
        [Tooltip("Distance to the player beyond which the creature stays silent and starts no voices.")]
        [SerializeField, Min(0.5f)] private float hearingDistance = 16f;

        [Header("Volume and pitch")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.25f;
        [Tooltip("Centre of the randomized pitch. Each leg contact picks its own value.")]
        [SerializeField, Range(0.2f, 3f)] private float pitchCentre = 1f;
        [SerializeField, Range(0f, 1f)] private float pitchJitter = 0.25f;
        [Tooltip("How much a single click may drop below the current volume. Keeps the rattle uneven.")]
        [SerializeField, Range(0f, 1f)] private float volumeJitter = 0.3f;

        [Header("Voices")]
        [Tooltip("Overlapping voices. Each one carries its own pitch, and a voice is recycled after "
            + "every full round, so more voices keep fast cadences from cutting each other short.")]
        [SerializeField, Range(1, 32)] private int voiceCount = 16;
        [Tooltip("Maximum clicks started in one frame. A long frame drops the surplus instead of stacking it.")]
        [SerializeField, Range(1, 64)] private int maximumStepsPerFrame = 12;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.7f;

        private ProceduralCentipede centipede;
        private AudioSource[] voices;
        private int nextVoice, stepsThisFrame, countedFrame = -1;
        private float frameFalloff;

        private void Awake()
        {
            centipede = GetComponent<ProceduralCentipede>();
            voices = new AudioSource[Mathf.Max(1, voiceCount)];
            for (int i = 0; i < voices.Length; i++) voices[i] = CreateVoice(i);
        }

        private void OnEnable()
        {
            if (centipede != null) centipede.LegStep += OnLegStep;
        }

        private void OnDisable()
        {
            if (centipede != null) centipede.LegStep -= OnLegStep;
        }

        private AudioSource CreateVoice(int index)
        {
            var holder = new GameObject("Leg Click " + index.ToString());
            holder.transform.SetParent(transform, false);
            var source = holder.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = spatialBlend;
            source.dopplerLevel = 0f;
            // The hearing range below does the audible falloff; the 3D curve only keeps the panning,
            // because the listener rides an overhead camera rather than the player itself.
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = hearingDistance;
            source.maxDistance = hearingDistance * 4f;
            if (output != null) source.outputAudioMixerGroup = output;
            return source;
        }

        private void OnLegStep(CentipedeLegStep step)
        {
            if (legStep == null || voices == null) return;
            if (countedFrame != Time.frameCount)
            {
                countedFrame = Time.frameCount;
                stepsThisFrame = 0;
                frameFalloff = Falloff();
            }
            if (frameFalloff <= 0f || stepsThisFrame >= maximumStepsPerFrame) return;
            stepsThisFrame++;
            AudioSource source = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            source.transform.position = step.Position;
            // The sample carries over a second of trailing silence, so release the voice this one-shot
            // still holds instead of letting idle voices pile up past the real voice limit.
            source.Stop();
            source.pitch = Mathf.Max(0.05f, pitchCentre + Random.Range(-pitchJitter, pitchJitter));
            float level = volume * frameFalloff * (1f - Random.Range(0f, volumeJitter));
            source.PlayOneShot(legStep, Mathf.Clamp01(level));
        }

        private float Falloff()
        {
            Transform player = centipede != null ? centipede.Target : null;
            if (player == null) return 0f;
            float distance = Vector3.Distance(transform.position, player.position);
            if (distance >= hearingDistance) return 0f;
            if (distance <= fullVolumeDistance) return 1f;
            float remaining = 1f - (distance - fullVolumeDistance)
                / Mathf.Max(0.01f, hearingDistance - fullVolumeDistance);
            return remaining * remaining;
        }

        private void OnValidate()
        {
            hearingDistance = Mathf.Max(hearingDistance, fullVolumeDistance + 0.5f);
        }
    }
}
