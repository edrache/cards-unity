using UnityEngine;

namespace CardsUnity.Controllers
{
    // Reference-inspired procedural cycles. Flight is visual, not a gameplay jump.
    public struct GaitStylePose
    {
        public float StrideScale, BounceScale, SwayScale, FootLiftScale, ArmSwingScale;
        public float RunAmount, Lean, BodyY, Flight, ArmPitch, Elbow;
        public Vector3 SpinePitch, SpineRoll;
        public float SpineTwist;

        public static GaitStylePose Evaluate(int index, float cycle)
        {
            float phase = cycle * Mathf.PI * 2f;
            float step = phase * 2f;
            var p = new GaitStylePose
            {
                StrideScale = 1f, BounceScale = 1f, SwayScale = 1f,
                FootLiftScale = 1f, ArmSwingScale = 1f
            };
            switch (index)
            {
                case 1: // Two down/up accents per step.
                    p.BodyY = -0.055f * (1f + Mathf.Sin(step * 2f));
                    p.BounceScale = 0.6f; p.Elbow = 20f;
                    break;
                case 2: // Proud posture, high recovery and broad opposing arms.
                    p.Lean = -15f; p.FootLiftScale = 1.7f; p.ArmSwingScale = 1.45f;
                    p.SwayScale = 1.3f; p.StrideScale = 1.15f;
                    break;
                case 3: // Short, low scraping steps and slumped shoulders.
                    p.StrideScale = 0.48f; p.FootLiftScale = 0.12f; p.BounceScale = 0.15f;
                    p.ArmSwingScale = 0.15f; p.Lean = 24f; p.BodyY = -0.07f; p.Elbow = -35f;
                    break;
                case 4: // Deep compression alternating with a cautious reach.
                    p.StrideScale = 0.8f; p.Lean = 22f + 10f * Mathf.Sin(step);
                    p.BodyY = -0.15f - 0.09f * Mathf.Sin(step); p.BounceScale = 0.3f;
                    p.FootLiftScale = 1.4f; p.ArmSwingScale = 0.3f; p.ArmPitch = -28f; p.Elbow = 25f;
                    break;
                case 5:
                    p.RunAmount = 1f; p.FootLiftScale = 1.2f; p.Elbow = 15f;
                    break;
                case 6: // Both legs launch and tuck together once per cycle.
                    p.StrideScale = 1.35f; p.BounceScale = 0f; p.SwayScale = 0f;
                    p.BodyY = -0.17f * (1f - JumpArc(cycle)); p.Flight = JumpArc(cycle) * 0.38f;
                    p.ArmSwingScale = 0f; p.ArmPitch = -75f * JumpArc(cycle);
                    p.Elbow = -20f; p.Lean = 12f * Mathf.Sin(phase);
                    break;
                case 7: // Low, stretched sprint with arms streaming behind.
                    p.RunAmount = 1f; p.StrideScale = 1.2f; p.Lean = 38f;
                    p.BodyY = -0.10f; p.FootLiftScale = 1.6f; p.ArmSwingScale = 0.25f;
                    p.ArmPitch = 48f; p.Elbow = -55f; p.BounceScale = 0.65f;
                    break;
                case 8: // Raised heels and compact, quiet steps.
                    p.StrideScale = 0.55f; p.BounceScale = 0.18f; p.SwayScale = 0.45f;
                    p.BodyY = 0.035f; p.FootLiftScale = 0.45f; p.ArmSwingScale = 0.15f;
                    p.ArmPitch = -22f; p.Elbow = 35f;
                    break;
                case 9: // Unequal step-hop accents and a lifted leading knee.
                    p.StrideScale = 0.85f; p.BounceScale = 0.4f; p.FootLiftScale = 1.4f;
                    p.Flight = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(step)), 2f)
                        * (Mathf.Sin(phase) > 0f ? 0.24f : 0.12f);
                    p.ArmSwingScale = 1.25f; p.Elbow = 15f;
                    break;
            }
            float wave = Mathf.Sin(phase), pulse = Mathf.Sin(step);
            p.SpinePitch = new Vector3(1f, 2f + pulse, -1f);
            p.SpineTwist = wave * 7f;
            p.SpineRoll = new Vector3(-wave, wave * 2f, wave);
            switch (index)
            {
                case 1: p.SpinePitch = new Vector3(2f, 4f, 2f) * (1f + 0.6f * Mathf.Sin(step * 2f)); break;
                case 2: p.SpinePitch = new Vector3(2f, -7f, -5f + pulse * 2f); p.SpineTwist = wave * 14f; break;
                case 3: p.SpinePitch = new Vector3(5f, 14f + pulse, 12f); p.SpineTwist = wave * 2f; break;
                case 4: p.SpinePitch = new Vector3(8f + pulse * 5f, 12f + pulse * 7f, -5f - pulse * 3f); p.SpineTwist = wave * 9f; break;
                case 5: p.SpinePitch = new Vector3(3f + pulse * 2f, 5f - pulse * 3f, -2f); p.SpineTwist = wave * 13f; break;
                case 6:
                    float arc = JumpArc(cycle);
                    p.SpinePitch = Vector3.Lerp(new Vector3(13f, 17f, 9f), new Vector3(-4f, -7f, -3f), arc);
                    p.SpineTwist = 0f; p.SpineRoll = Vector3.zero; break;
                case 7: p.SpinePitch = new Vector3(8f, 7f + pulse * 2f, 3f - pulse); p.SpineTwist = wave * 10f; break;
                case 8: p.SpinePitch = new Vector3(-5f, 14f, 9f + pulse); p.SpineTwist = wave * 3f; p.SpineRoll *= 0.4f; break;
                case 9: p.SpinePitch = new Vector3(3f * pulse, -4f + pulse * 5f, -2f - pulse * 3f); p.SpineTwist = wave * 11f; p.SpineRoll *= 1.5f; break;
            }
            return p;
        }

        public static float JumpArc(float cycle)
        {
            float t = Mathf.Clamp01((cycle - 0.18f) / 0.7f);
            float sine = Mathf.Sin(t * Mathf.PI);
            return sine * sine;
        }

        public static GaitStylePose Blend(float[] weights, float cycle)
        {
            var result = new GaitStylePose();
            for (int i = 0; i < weights.Length; i++)
            {
                float w = weights[i];
                var p = Evaluate(i, cycle);
                result.StrideScale += p.StrideScale * w;
                result.BounceScale += p.BounceScale * w;
                result.SwayScale += p.SwayScale * w;
                result.FootLiftScale += p.FootLiftScale * w;
                result.ArmSwingScale += p.ArmSwingScale * w;
                result.RunAmount += p.RunAmount * w;
                result.Lean += p.Lean * w;
                result.BodyY += p.BodyY * w;
                result.Flight += p.Flight * w;
                result.ArmPitch += p.ArmPitch * w;
                result.Elbow += p.Elbow * w;
                result.SpinePitch += p.SpinePitch * w;
                result.SpineRoll += p.SpineRoll * w;
                result.SpineTwist += p.SpineTwist * w;
            }
            return result;
        }

        public static void AdjustFoot(float[] weights, float cycle, float side, float travel,
            float activity, ref float z, ref float lift, ref float pitch)
        {
            float arc = JumpArc(cycle);
            float jumpZ = -Mathf.Sin(cycle * Mathf.PI * 2f) * travel * 0.6f;
            z = Mathf.Lerp(z, jumpZ, weights[6]);
            lift = Mathf.Lerp(lift, arc * 0.19f * activity, weights[6]);
            float skipWave = Mathf.Max(0f, Mathf.Sin(cycle * Mathf.PI * 2f + (side < 0f ? 0f : Mathf.PI)));
            lift += weights[9] * skipWave * skipWave * 0.10f * activity;
            // Raise the ankle as the shoe pivots onto its toe.
            lift += weights[8] * 0.08f * activity;
            pitch = weights[8] * 28f * activity;
        }
    }
}
