using System;
using UnityEngine;

namespace CardsUnity.Controllers
{
    // Tonal footstep clips: the movement direction picks the note (C minor), the foot picks the
    // weight. Each slot holds interchangeable variants that are never played twice in a row.
    [CreateAssetMenu(menuName = "Cards Unity/Audio/Step Sound Set", fileName = "Step Sound Set")]
    public sealed class StepSoundSet : ScriptableObject
    {
        [Serializable]
        public sealed class FootPair
        {
            public AudioClip[] leftFoot = Array.Empty<AudioClip>();
            public AudioClip[] rightFoot = Array.Empty<AudioClip>();
        }

        [Tooltip("Moving away from the camera. Synth set: C2.")]
        [SerializeField] private FootPair forward = new FootPair();
        [Tooltip("Moving to the camera's right. Synth set: G2.")]
        [SerializeField] private FootPair right = new FootPair();
        [Tooltip("Moving toward the camera. Synth set: A-flat 1.")]
        [SerializeField] private FootPair back = new FootPair();
        [Tooltip("Moving to the camera's left. Synth set: E-flat 2.")]
        [SerializeField] private FootPair left = new FootPair();
        [Tooltip("Toneless shuffle when turning left in place.")]
        [SerializeField] private AudioClip[] turnLeft = Array.Empty<AudioClip>();
        [Tooltip("Toneless shuffle when turning right in place.")]
        [SerializeField] private AudioClip[] turnRight = Array.Empty<AudioClip>();

        public const int SlotCount = 10;

        public AudioClip[] GetSteps(StepDirection direction, bool leftFoot)
        {
            FootPair pair = direction switch
            {
                StepDirection.Right => right,
                StepDirection.Back => back,
                StepDirection.Left => left,
                _ => forward
            };
            return (leftFoot ? pair?.leftFoot : pair?.rightFoot) ?? Array.Empty<AudioClip>();
        }

        public AudioClip[] GetTurn(bool turnRight) => (turnRight ? this.turnRight : turnLeft) ?? Array.Empty<AudioClip>();

        public static int SlotIndex(StepDirection direction, bool leftFoot) => (int)direction * 2 + (leftFoot ? 0 : 1);
        public static int TurnSlotIndex(bool turnRight) => turnRight ? 9 : 8;
    }
}
