using System;
using UnityEngine;

namespace CardsUnity.Controllers
{
    public enum StepDirection { Forward, Right, Back, Left }

    // Picks the movement sector relative to a view direction, holding the current sector until the
    // angle passes its edge by the hysteresis margin so diagonal movement does not flicker.
    public sealed class StepDirectionSelector
    {
        public StepDirection Current { get; private set; } = StepDirection.Forward;

        public StepDirection Select(Vector2 move, Vector2 viewForward, float hysteresisDegrees)
        {
            if (move.sqrMagnitude < 1e-6f || viewForward.sqrMagnitude < 1e-6f) return Current;
            // Unity's SignedAngle is counter-clockwise positive; right of forward should be positive.
            float angle = -Vector2.SignedAngle(viewForward, move);
            if (Mathf.Abs(Mathf.DeltaAngle(angle, Centre(Current))) <= 45f + Mathf.Max(0f, hysteresisDegrees))
                return Current;
            StepDirection best = StepDirection.Forward;
            float bestDelta = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                float delta = Mathf.Abs(Mathf.DeltaAngle(angle, Centre((StepDirection)i)));
                if (delta < bestDelta) { bestDelta = delta; best = (StepDirection)i; }
            }
            Current = best;
            return Current;
        }

        public void Reset(StepDirection direction = StepDirection.Forward) => Current = direction;

        private static float Centre(StepDirection direction) => (int)direction * 90f;
    }

    public static class VariantPicker
    {
        /// <summary>Random index below <paramref name="count"/> that differs from <paramref name="previous"/> when possible.</summary>
        public static int Pick(int count, int previous, Func<int, int> randomBelow)
        {
            if (count <= 0) return -1;
            if (count == 1) return 0;
            if (previous < 0 || previous >= count) return randomBelow(count);
            int roll = randomBelow(count - 1);
            return roll >= previous ? roll + 1 : roll;
        }
    }
}
