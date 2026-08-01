using System;

namespace Loom.Fmod
{
    public sealed class FmodAdsrEnvelope
    {
        public static FmodAdsrEnvelope Default { get; } =
            new FmodAdsrEnvelope(0.01, 0.1, 0.7f, 0.2);

        public FmodAdsrEnvelope(
            double attackSeconds,
            double decaySeconds,
            float sustainLevel,
            double releaseSeconds)
        {
            ValidatePositiveDuration(attackSeconds, nameof(attackSeconds));
            ValidateNonNegativeDuration(decaySeconds, nameof(decaySeconds));
            ValidateNormalizedLevel(sustainLevel, nameof(sustainLevel));
            ValidatePositiveDuration(releaseSeconds, nameof(releaseSeconds));

            AttackSeconds = attackSeconds;
            DecaySeconds = decaySeconds;
            SustainLevel = sustainLevel;
            ReleaseSeconds = releaseSeconds;
        }

        public double AttackSeconds { get; }

        public double DecaySeconds { get; }

        public float SustainLevel { get; }

        public double ReleaseSeconds { get; }

        public FmodAdsrEnvelopeSamples ResolveSampleFrames(int sampleRate)
        {
            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleRate),
                    sampleRate,
                    "Sample rate must be positive.");
            }

            return new FmodAdsrEnvelopeSamples(
                SecondsToSampleFrames(AttackSeconds, sampleRate, nameof(AttackSeconds)),
                SecondsToSampleFrames(DecaySeconds, sampleRate, nameof(DecaySeconds)),
                SustainLevel,
                SecondsToSampleFrames(ReleaseSeconds, sampleRate, nameof(ReleaseSeconds)));
        }

        private static ulong SecondsToSampleFrames(
            double seconds,
            int sampleRate,
            string parameterName)
        {
            double exactFrames = seconds * sampleRate;
            if (exactFrames > ulong.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    seconds,
                    "Envelope duration is too large to represent in DSP sample frames.");
            }

            ulong frames = (ulong)Math.Round(exactFrames, MidpointRounding.AwayFromZero);
            return seconds > 0d && frames == 0UL ? 1UL : frames;
        }

        private static void ValidatePositiveDuration(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Envelope duration must be finite and greater than zero.");
            }
        }

        private static void ValidateNonNegativeDuration(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Envelope duration must be finite and non-negative.");
            }
        }

        private static void ValidateNormalizedLevel(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Envelope level must be finite and between 0 and 1.");
            }
        }
    }

    public readonly struct FmodAdsrEnvelopeSamples
    {
        internal FmodAdsrEnvelopeSamples(
            ulong attackFrames,
            ulong decayFrames,
            float sustainLevel,
            ulong releaseFrames)
        {
            AttackFrames = attackFrames;
            DecayFrames = decayFrames;
            SustainLevel = sustainLevel;
            ReleaseFrames = releaseFrames;
        }

        public ulong AttackFrames { get; }

        public ulong DecayFrames { get; }

        public float SustainLevel { get; }

        public ulong ReleaseFrames { get; }

        public float GetAttackDecaySustainLevel(ulong elapsedFrames)
        {
            if (elapsedFrames < AttackFrames)
            {
                float attackTarget = DecayFrames == 0UL ? SustainLevel : 1f;
                return attackTarget * (float)((double)elapsedFrames / AttackFrames);
            }

            if (DecayFrames == 0UL)
            {
                return SustainLevel;
            }

            ulong decayElapsedFrames = elapsedFrames - AttackFrames;
            if (decayElapsedFrames < DecayFrames)
            {
                float decayProgress = (float)((double)decayElapsedFrames / DecayFrames);
                return 1f + ((SustainLevel - 1f) * decayProgress);
            }

            return SustainLevel;
        }
    }
}
