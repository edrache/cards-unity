using System;

namespace Loom.Core
{
    /// <summary>
    /// Resolves pitched track roles against one immutable scale and cyclic harmony plan.
    /// </summary>
    public sealed class HarmonyResolver
    {
        private const int ChordToneCount = 3;

        private readonly Scale scale;
        private readonly Note tonic;
        private readonly HarmonyPlan harmonyPlan;

        public HarmonyResolver(Scale scale, Note tonic, HarmonyPlan harmonyPlan)
        {
            this.scale = scale ?? throw new ArgumentNullException(nameof(scale));
            this.tonic = tonic;
            this.harmonyPlan = harmonyPlan
                ?? throw new ArgumentNullException(nameof(harmonyPlan));
        }

        /// <summary>
        /// Resolves lead degrees freely, bass notes to the active root, and pad notes to root-third-fifth
        /// diatonic chord tones. Requested octave offsets are preserved for every role.
        /// </summary>
        public Note Resolve(ScaleDegreePitch requestedPitch, long tick, TrackRole role)
        {
            HarmonyStep harmonyStep = harmonyPlan.GetStepAtTick(tick);

            switch (role)
            {
                case TrackRole.Bass:
                    return scale.Resolve(
                        tonic,
                        new ScaleDegreePitch(
                            harmonyStep.RootScaleDegree,
                            requestedPitch.OctaveOffset));
                case TrackRole.Lead:
                    return scale.Resolve(tonic, requestedPitch);
                case TrackRole.Pad:
                    return ResolvePad(requestedPitch, harmonyStep.RootScaleDegree);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(role),
                        role,
                        "Track role must identify a pitched harmony policy.");
            }
        }

        private Note ResolvePad(ScaleDegreePitch requestedPitch, int rootScaleDegree)
        {
            long degreeCycle = requestedPitch.Degree / ChordToneCount;
            int chordToneIndex = requestedPitch.Degree % ChordToneCount;
            if (chordToneIndex < 0)
            {
                chordToneIndex += ChordToneCount;
                degreeCycle--;
            }

            int chordToneOffset = chordToneIndex * 2;
            long resolvedDegree = (long)rootScaleDegree + chordToneOffset;
            long resolvedOctaveOffset = (long)requestedPitch.OctaveOffset + degreeCycle;
            if (resolvedDegree < int.MinValue
                || resolvedDegree > int.MaxValue
                || resolvedOctaveOffset < int.MinValue
                || resolvedOctaveOffset > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requestedPitch),
                    requestedPitch,
                    "Resolved harmony degree exceeds the supported integer range.");
            }

            return scale.Resolve(
                tonic,
                new ScaleDegreePitch(
                    (int)resolvedDegree,
                    (int)resolvedOctaveOffset));
        }
    }
}
