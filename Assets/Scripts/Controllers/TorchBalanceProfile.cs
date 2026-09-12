using System;
using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Shared torch gameplay tuning. Fuel age always remains instance runtime state.</summary>
    [CreateAssetMenu(fileName = "Torch Balance", menuName = "Cards Unity/Balance/Torch")]
    public sealed class TorchBalanceProfile : ScriptableObject
    {
        [SerializeField] private TorchIlluminationBalance illumination = new();
        [SerializeField] private TorchFuelBalance fuel = new();

        public TorchIlluminationBalance Illumination => illumination;
        public TorchFuelBalance Fuel => fuel;

        private void OnValidate()
        {
            illumination ??= new TorchIlluminationBalance();
            fuel ??= new TorchFuelBalance();
            illumination.Validate();
            fuel.Validate();
        }

#if UNITY_EDITOR
        /// <summary>Copies the selected torch's local fallback values into this asset.</summary>
        public bool CaptureFrom(HandheldTorch source, out string warning)
        {
            if (source == null)
            {
                warning = "Select a HandheldTorch to capture.";
                return false;
            }
            UnityEditor.Undo.RecordObject(this, "Capture Torch Balance");
            source.CopyLegacyBalanceTo(illumination, fuel);
            OnValidate();
            UnityEditor.EditorUtility.SetDirty(this);
            warning = string.Empty;
            return true;
        }

        /// <summary>Assigns this asset while preserving all local rig and presentation settings.</summary>
        public bool ApplyTo(HandheldTorch target, out string warning)
        {
            if (target == null)
            {
                warning = "Select a HandheldTorch to assign.";
                return false;
            }
            warning = target.BalanceProfile != null && target.BalanceProfile != this
                ? "Replaced a different torch balance profile." : string.Empty;
            UnityEditor.Undo.RecordObject(target, "Assign Torch Balance");
            target.AssignBalanceProfile(this);
            return true;
        }
#endif
    }

    [Serializable]
    public sealed class TorchIlluminationBalance
    {
        [Tooltip("Brightness used by both the Light and gameplay exposure sampling.")]
        [SerializeField, Range(0f, 15f)] private float brightness = 5f;
        [SerializeField, Range(1f, 20f)] private float lightRange = 8f;
        public float Brightness => brightness;
        public float LightRange => lightRange;
        internal void Capture(float value, float range) { brightness = value; lightRange = range; }
        internal void Validate()
        {
            brightness = Mathf.Clamp(brightness, 0f, 15f);
            lightRange = Mathf.Clamp(lightRange, 1f, 20f);
        }
    }

    [Serializable]
    public sealed class TorchFuelBalance
    {
        [SerializeField, Min(0.1f)] private float lifetime = 180f;
        [Tooltip("Fraction of the lifetime over which illumination and emissions fade out.")]
        [SerializeField, Range(0.05f, 1f)] private float burnoutFraction = 0.4f;
        [SerializeField, Range(0f, 60f)] private float strikeLifetimeCost = 5f;
        [SerializeField, Min(0f)] private float dropLifetimeCost = 10f;
        public float Lifetime => lifetime;
        public float BurnoutFraction => burnoutFraction;
        public float StrikeLifetimeCost => strikeLifetimeCost;
        public float DropLifetimeCost => dropLifetimeCost;
        internal void Capture(float duration, float burnout, float strikeCost, float dropCost)
        {
            lifetime = duration;
            burnoutFraction = burnout;
            strikeLifetimeCost = strikeCost;
            dropLifetimeCost = dropCost;
        }
        internal void Validate()
        {
            lifetime = Mathf.Max(0.1f, lifetime);
            burnoutFraction = Mathf.Clamp(burnoutFraction, 0.05f, 1f);
            strikeLifetimeCost = Mathf.Clamp(strikeLifetimeCost, 0f, 60f);
            dropLifetimeCost = Mathf.Max(0f, dropLifetimeCost);
        }
    }
}
