using System;
using System.Collections.Generic;
using FMODUnity;
using Loom.Fmod;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Loom.Demo
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class LoomSynthDemoController : MonoBehaviour
    {
        private static readonly List<string> WaveformChoices =
            new List<string>
            {
                "Sine",
                "Square",
                "Saw Up",
                "Saw Down",
                "Triangle",
                "Noise"
            };

        [SerializeField, Range(1, 127)]
        private int velocity = Unity.ComputerKeyboardNoteInput.DefaultVelocity;

        [SerializeField, Range(0f, 1f)]
        private float gain = 0.08f;

        [SerializeField, Range(-4, 4)]
        private int octave;

        [SerializeField, Range(20f, 22000f)]
        private float cutoffHz = 8000f;

        [SerializeField, Range(0.1f, 10f)]
        private float resonance = 0.707f;

        [SerializeField, Min(0.001f)]
        private float attackSeconds = 0.01f;

        [SerializeField, Min(0f)]
        private float decaySeconds = 0.1f;

        [SerializeField, Range(0f, 1f)]
        private float sustainLevel = 0.7f;

        [SerializeField, Min(0.001f)]
        private float releaseSeconds = 0.2f;

        private FmodOscillatorInstrument instrument;
        private Unity.ComputerKeyboardNoteInput keyboardInput;
        private DropdownField waveformField;
        private Slider gainSlider;
        private SliderInt octaveSlider;
        private Slider cutoffSlider;
        private Slider resonanceSlider;
        private Slider attackSlider;
        private Slider decaySlider;
        private Slider sustainSlider;
        private Slider releaseSlider;
        private Label voiceStatusLabel;
        private Label engineStatusLabel;
        private float nextStatusRefreshTime;
        private int lastDisplayedActiveVoiceCount = -1;
        private bool uiIsBound;
        private bool isShutdown = true;

#if UNITY_EDITOR
        private bool isSubscribedToAssemblyReload;
#endif

        public FmodOscillatorInstrument Instrument => instrument;

        private void OnEnable()
        {
            isShutdown = false;
            SubscribeToAssemblyReload();

            try
            {
                CreateInstrument();
                BindUi();
                SetEngineStatus("FMOD ONLINE · MUS_SYNTH ROUTED");
                RefreshVoiceStatus();
            }
            catch (Exception exception)
            {
                Shutdown();
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private void Update()
        {
            keyboardInput?.Poll();

            if (instrument != null && Time.unscaledTime >= nextStatusRefreshTime)
            {
                nextStatusRefreshTime = Time.unscaledTime + 0.1f;
                RefreshVoiceStatus();
            }
        }

        private void OnDisable()
        {
            Shutdown();
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                Panic();
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                Panic();
            }
        }

        private void OnApplicationQuit()
        {
            Shutdown();
        }

        public void Panic()
        {
            if (keyboardInput == null)
            {
                return;
            }

            keyboardInput.Panic();
            lastDisplayedActiveVoiceCount = -1;
            if (instrument != null && voiceStatusLabel != null)
            {
                RefreshVoiceStatus();
            }
        }

        private void CreateInstrument()
        {
            var envelope = new FmodAdsrEnvelope(
                attackSeconds,
                decaySeconds,
                sustainLevel,
                releaseSeconds);
            var settings = new FmodOscillatorSettings(
                gain: gain,
                octave: octave,
                cutoffHz: cutoffHz,
                resonance: resonance);

            instrument = new FmodOscillatorInstrument(
                RuntimeManager.CoreSystem,
                RuntimeManager.StudioSystem,
                envelope,
                settings);
            keyboardInput = new Unity.ComputerKeyboardNoteInput(
                instrument,
                velocity);
        }

        private void BindUi()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            waveformField = RequireElement<DropdownField>(root, "waveform-field");
            gainSlider = RequireElement<Slider>(root, "gain-slider");
            octaveSlider = RequireElement<SliderInt>(root, "octave-slider");
            cutoffSlider = RequireElement<Slider>(root, "cutoff-slider");
            resonanceSlider = RequireElement<Slider>(root, "resonance-slider");
            attackSlider = RequireElement<Slider>(root, "attack-slider");
            decaySlider = RequireElement<Slider>(root, "decay-slider");
            sustainSlider = RequireElement<Slider>(root, "sustain-slider");
            releaseSlider = RequireElement<Slider>(root, "release-slider");
            voiceStatusLabel = RequireElement<Label>(root, "voice-status");
            engineStatusLabel = RequireElement<Label>(root, "engine-status");

            waveformField.choices = WaveformChoices;
            waveformField.index = (int)instrument.Waveform;
            gainSlider.SetValueWithoutNotify(instrument.Gain);
            octaveSlider.SetValueWithoutNotify(instrument.Octave);
            cutoffSlider.SetValueWithoutNotify(instrument.CutoffHz);
            resonanceSlider.SetValueWithoutNotify(instrument.Resonance);
            attackSlider.SetValueWithoutNotify((float)instrument.Envelope.AttackSeconds);
            decaySlider.SetValueWithoutNotify((float)instrument.Envelope.DecaySeconds);
            sustainSlider.SetValueWithoutNotify(instrument.Envelope.SustainLevel);
            releaseSlider.SetValueWithoutNotify((float)instrument.Envelope.ReleaseSeconds);

            waveformField.RegisterValueChangedCallback(OnWaveformChanged);
            gainSlider.RegisterValueChangedCallback(OnGainChanged);
            octaveSlider.RegisterValueChangedCallback(OnOctaveChanged);
            cutoffSlider.RegisterValueChangedCallback(OnCutoffChanged);
            resonanceSlider.RegisterValueChangedCallback(OnResonanceChanged);
            attackSlider.RegisterValueChangedCallback(OnEnvelopeChanged);
            decaySlider.RegisterValueChangedCallback(OnEnvelopeChanged);
            sustainSlider.RegisterValueChangedCallback(OnEnvelopeChanged);
            releaseSlider.RegisterValueChangedCallback(OnEnvelopeChanged);
            uiIsBound = true;
        }

        private void UnbindUi()
        {
            if (!uiIsBound)
            {
                return;
            }

            waveformField.UnregisterValueChangedCallback(OnWaveformChanged);
            gainSlider.UnregisterValueChangedCallback(OnGainChanged);
            octaveSlider.UnregisterValueChangedCallback(OnOctaveChanged);
            cutoffSlider.UnregisterValueChangedCallback(OnCutoffChanged);
            resonanceSlider.UnregisterValueChangedCallback(OnResonanceChanged);
            attackSlider.UnregisterValueChangedCallback(OnEnvelopeChanged);
            decaySlider.UnregisterValueChangedCallback(OnEnvelopeChanged);
            sustainSlider.UnregisterValueChangedCallback(OnEnvelopeChanged);
            releaseSlider.UnregisterValueChangedCallback(OnEnvelopeChanged);
            uiIsBound = false;
        }

        private void DisposeInstrument()
        {
            keyboardInput = null;
            if (instrument == null)
            {
                return;
            }

            instrument.Dispose();
            instrument = null;
        }

        private void Shutdown()
        {
            if (isShutdown)
            {
                return;
            }

            isShutdown = true;
            UnsubscribeFromAssemblyReload();
            UnbindUi();

            try
            {
                Panic();
            }
            finally
            {
                try
                {
                    DisposeInstrument();
                }
                finally
                {
                    if (instrument != null)
                    {
                        isShutdown = false;
                    }
                }
            }
        }

        private void SubscribeToAssemblyReload()
        {
#if UNITY_EDITOR
            if (isSubscribedToAssemblyReload)
            {
                return;
            }

            AssemblyReloadEvents.beforeAssemblyReload +=
                HandleBeforeAssemblyReload;
            isSubscribedToAssemblyReload = true;
#endif
        }

        private void UnsubscribeFromAssemblyReload()
        {
#if UNITY_EDITOR
            if (!isSubscribedToAssemblyReload)
            {
                return;
            }

            AssemblyReloadEvents.beforeAssemblyReload -=
                HandleBeforeAssemblyReload;
            isSubscribedToAssemblyReload = false;
#endif
        }

#if UNITY_EDITOR
        private void HandleBeforeAssemblyReload()
        {
            Shutdown();
        }
#endif

        private void OnWaveformChanged(ChangeEvent<string> changeEvent)
        {
            instrument.SetWaveform(
                (FmodOscillatorWaveform)waveformField.index);
        }

        private void OnGainChanged(ChangeEvent<float> changeEvent)
        {
            instrument.SetGain(changeEvent.newValue);
        }

        private void OnOctaveChanged(ChangeEvent<int> changeEvent)
        {
            instrument.SetOctave(changeEvent.newValue);
        }

        private void OnCutoffChanged(ChangeEvent<float> changeEvent)
        {
            instrument.SetCutoffHz(changeEvent.newValue);
        }

        private void OnResonanceChanged(ChangeEvent<float> changeEvent)
        {
            instrument.SetResonance(changeEvent.newValue);
        }

        private void OnEnvelopeChanged(ChangeEvent<float> changeEvent)
        {
            instrument.SetEnvelope(
                new FmodAdsrEnvelope(
                    attackSlider.value,
                    decaySlider.value,
                    sustainSlider.value,
                    releaseSlider.value));
        }

        private void RefreshVoiceStatus()
        {
            int activeVoiceCount = instrument.ActiveVoiceCount;
            if (activeVoiceCount == lastDisplayedActiveVoiceCount)
            {
                return;
            }

            lastDisplayedActiveVoiceCount = activeVoiceCount;
            voiceStatusLabel.text =
                $"VOICES  {activeVoiceCount:00} / {instrument.VoiceCapacity:00}";
        }

        private void SetEngineStatus(string message)
        {
            if (engineStatusLabel != null)
            {
                engineStatusLabel.text = message;
            }
        }

        private static T RequireElement<T>(VisualElement root, string name)
            where T : VisualElement
        {
            T element = root.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException(
                    $"The LOOM demo UI is missing required element '{name}'.");
            }

            return element;
        }
    }
}
