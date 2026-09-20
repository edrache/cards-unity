using System;
using TMPro;
using UnityEngine;

namespace CardsUnity.Controllers
{
    public sealed class StorySequencePlayer : MonoBehaviour
    {
        private enum Phase { Idle, Revealing, Holding, Fading }

        [SerializeField] private TMP_Text textLabel;
        [SerializeField] private StorySequence sequence;

        public bool IsPlaying => phase != Phase.Idle;
        public int CurrentLineIndex => IsPlaying ? lineIndex : -1;
        public event Action Completed;

        private Phase phase;
        private string[] lines;
        private int lineIndex;
        private float revealPosition;
        private float revealElapsed;
        private float phaseElapsed;
        private bool revealAccelerated;
        private bool fadeAccelerated;
        private float[] characterRevealTimes;
        private byte[][] baseVertexAlphas;

        private void Awake()
        {
            ClearLabel();
        }

        private void OnEnable()
        {
            if (textLabel != null) textLabel.OnPreRenderText += OnPreRenderText;
        }

        private void OnDisable()
        {
            if (textLabel != null) textLabel.OnPreRenderText -= OnPreRenderText;
            ClearLabel();
            ResetState();
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        public void Begin()
        {
            Stop();

            if (textLabel == null || sequence == null)
            {
                Completed?.Invoke();
                return;
            }

            if (string.IsNullOrEmpty(sequence.Text))
            {
                Completed?.Invoke();
                return;
            }

            lines = sequence.GetLines();

            lineIndex = 0;
            BeginLine();
        }

        public void Stop()
        {
            ClearLabel();
            ResetState();
        }

        public void Advance()
        {
            if (!IsPlaying) return;

            switch (phase)
            {
                case Phase.Revealing:
                    revealAccelerated = true;
                    break;
                case Phase.Holding:
                    BeginFade();
                    break;
                case Phase.Fading:
                    fadeAccelerated = true;
                    break;
            }
        }

        public void Tick(float deltaTime)
        {
            if (!IsPlaying || textLabel == null) return;
            deltaTime = Mathf.Max(0f, deltaTime);

            switch (phase)
            {
                case Phase.Revealing:
                    TickReveal(deltaTime);
                    break;
                case Phase.Holding:
                    revealElapsed += deltaTime;
                    phaseElapsed += deltaTime;
                    ApplyVertexAlphas(1f);
                    if (phaseElapsed >= sequence.LineHoldDuration) BeginFade();
                    break;
                case Phase.Fading:
                    phaseElapsed += deltaTime * FadeAccelerationMultiplier();
                    ApplyVertexAlphas(1f - Normalized(phaseElapsed, sequence.LineFadeDuration));
                    if (phaseElapsed >= sequence.LineFadeDuration) NextLine();
                    break;
            }
        }

        private void TickReveal(float deltaTime)
        {
            float rate = revealAccelerated
                ? Mathf.Max(sequence.CharactersPerSecond, sequence.AcceleratedCharactersPerSecond)
                : sequence.CharactersPerSecond;
            int count = textLabel.textInfo.characterCount;

            if (rate <= 0f)
            {
                revealPosition = count;
                for (int i = 0; i < characterRevealTimes.Length; i++) characterRevealTimes[i] = 0f;
                revealElapsed += deltaTime;
            }
            else
            {
                float previousPosition = revealPosition;
                revealPosition = Mathf.Min(count, revealPosition + rate * deltaTime);
                RecordRevealedCharacters(previousPosition, rate, deltaTime);
            }

            ApplyVertexAlphas(1f);
            if (IsRevealComplete())
            {
                phase = Phase.Holding;
                phaseElapsed = 0f;
            }
        }

        private void BeginLine()
        {
            // Regenerate untouched glyph colours before applying this line's reveal.
            // The preceding line may have finished at zero opacity.
            phase = Phase.Idle;
            textLabel.text = lines[lineIndex];
            textLabel.ForceMeshUpdate();
            CacheBaseVertexAlphas();
            characterRevealTimes = new float[textLabel.textInfo.characterCount];
            for (int i = 0; i < characterRevealTimes.Length; i++) characterRevealTimes[i] = -1f;

            revealPosition = 0f;
            revealElapsed = 0f;
            phaseElapsed = 0f;
            revealAccelerated = false;
            fadeAccelerated = false;
            phase = Phase.Revealing;
            ApplyVertexAlphas(1f);
        }

        private void BeginFade()
        {
            phase = Phase.Fading;
            phaseElapsed = 0f;
            fadeAccelerated = false;
            if (sequence.LineFadeDuration <= 0f) NextLine();
        }

        private void NextLine()
        {
            ++lineIndex;
            if (lineIndex < lines.Length)
            {
                BeginLine();
                return;
            }

            ClearLabel();
            ResetState();
            Completed?.Invoke();
        }

        private void RecordRevealedCharacters(float previousPosition, float rate, float deltaTime)
        {
            int first = Mathf.Max(0, Mathf.FloorToInt(previousPosition));
            int last = Mathf.Min(characterRevealTimes.Length - 1, Mathf.CeilToInt(revealPosition) - 1);
            for (int i = first; i <= last; i++)
            {
                if (characterRevealTimes[i] >= 0f) continue;
                characterRevealTimes[i] = revealElapsed + (i + 1f - previousPosition) / rate;
            }
            revealElapsed += deltaTime;
        }

        private void ApplyVertexAlphas(float lineOpacity, bool uploadToMesh = true)
        {
            TMP_TextInfo textInfo = textLabel.textInfo;
            if (!HasCachedBaseAlphas(textInfo)) return;
            if (characterRevealTimes == null || characterRevealTimes.Length != textInfo.characterCount)
            {
                characterRevealTimes = new float[textInfo.characterCount];
                for (int i = 0; i < characterRevealTimes.Length; i++) characterRevealTimes[i] = -1f;
            }

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo character = textInfo.characterInfo[i];
                if (!character.isVisible) continue;

                float revealOpacity = characterRevealTimes[i] < 0f
                    ? 0f
                    : Normalized(revealElapsed - characterRevealTimes[i], sequence.CharacterFadeDuration);
                Color32[] colors = textInfo.meshInfo[character.materialReferenceIndex].colors32;
                byte[] baseAlphas = baseVertexAlphas[character.materialReferenceIndex];
                int vertex = character.vertexIndex;
                for (int j = 0; j < 4; j++)
                {
                    Color32 color = colors[vertex + j];
                    color.a = (byte)Mathf.RoundToInt(baseAlphas[vertex + j] * revealOpacity * lineOpacity);
                    colors[vertex + j] = color;
                }
            }

            if (uploadToMesh) textLabel.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        private float FadeAccelerationMultiplier()
        {
            if (!fadeAccelerated) return 1f;
            return sequence.CharactersPerSecond <= 0f
                ? 4f
                : Mathf.Max(1f, sequence.AcceleratedCharactersPerSecond / sequence.CharactersPerSecond);
        }

        private static float Normalized(float value, float duration)
        {
            return duration <= 0f ? 1f : Mathf.Clamp01(value / duration);
        }

        private void OnPreRenderText(TMP_TextInfo textInfo)
        {
            if (!IsPlaying || textInfo != textLabel.textInfo) return;

            // TMP has just regenerated unmodified colours. Its TEXT_CHANGED event
            // fires after this callback, so it must not gate subsequent animation ticks.
            CacheBaseVertexAlphas();

            ApplyVertexAlphas(phase == Phase.Fading
                ? 1f - Normalized(phaseElapsed, sequence.LineFadeDuration)
                : 1f, false);
        }

        private bool IsRevealComplete()
        {
            if (revealPosition < characterRevealTimes.Length) return false;
            if (characterRevealTimes.Length == 0 || sequence.CharacterFadeDuration <= 0f) return true;

            return revealElapsed >= characterRevealTimes[characterRevealTimes.Length - 1]
                + sequence.CharacterFadeDuration;
        }

        private bool HasCachedBaseAlphas(TMP_TextInfo textInfo)
        {
            if (baseVertexAlphas == null || baseVertexAlphas.Length != textInfo.meshInfo.Length) return false;
            for (int i = 0; i < baseVertexAlphas.Length; i++)
            {
                if (baseVertexAlphas[i] == null || baseVertexAlphas[i].Length != textInfo.meshInfo[i].colors32.Length)
                    return false;
            }
            return true;
        }

        private void CacheBaseVertexAlphas()
        {
            TMP_MeshInfo[] meshes = textLabel.textInfo.meshInfo;
            baseVertexAlphas = new byte[meshes.Length][];
            for (int i = 0; i < meshes.Length; i++)
            {
                Color32[] colors = meshes[i].colors32;
                baseVertexAlphas[i] = new byte[colors.Length];
                for (int j = 0; j < colors.Length; j++) baseVertexAlphas[i][j] = colors[j].a;
            }
        }

        private void ClearLabel()
        {
            if (textLabel != null) textLabel.text = string.Empty;
        }

        private void ResetState()
        {
            phase = Phase.Idle;
            lines = null;
            characterRevealTimes = null;
            baseVertexAlphas = null;
            revealPosition = 0f;
            revealElapsed = 0f;
            phaseElapsed = 0f;
        }
    }
}
