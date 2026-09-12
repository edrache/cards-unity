using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Arms the entrance after the player leaves it and completes the run on return.</summary>
    [DisallowMultipleComponent]
    public sealed class CaveExit : MonoBehaviour
    {
        private const float RearmMargin = 0.75f;

        private ProceduralCave cave;
        private CharacterHealth health;
        private bool initialized;
        private bool ownsTimePause;
        private float previousTimeScale = 1f;

        public bool HasLeftEntrance { get; private set; }
        public bool IsCompleted { get; private set; }

        public bool CanExit
        {
            get
            {
                if (health == null) health = GetComponent<CharacterHealth>();
                RefreshState();
                return !IsCompleted && (health == null || !health.IsDead)
                    && HasLeftEntrance && IsInsideExitArea();
            }
        }

        private void Awake() => health = GetComponent<CharacterHealth>();

        private void Update() => RefreshState();

        private void RefreshState()
        {
            if (IsCompleted) return;
            if (cave == null || cave.gameObject.scene != gameObject.scene)
            {
                cave = null;
                foreach (var candidate in FindObjectsByType<ProceduralCave>(FindObjectsSortMode.None))
                    if (candidate.gameObject.scene == gameObject.scene) { cave = candidate; break; }
            }
            if (cave == null || cave.RoomCenters.Count == 0) return;

            float distance = Vector3.Distance(transform.position, cave.ExitPosition);
            if (!initialized)
            {
                initialized = true;
                // The generated cave places the player at the entrance before this first sample.
                HasLeftEntrance = distance > cave.ExitInteractionRadius + RearmMargin;
            }
            else if (distance > cave.ExitInteractionRadius + RearmMargin)
            {
                HasLeftEntrance = true;
            }
        }

        private bool IsInsideExitArea()
        {
            return cave != null
                && Vector3.Distance(transform.position, cave.ExitPosition) <= cave.ExitInteractionRadius;
        }

        public bool TryExit(CharacterInventory inventory)
        {
            if (!CanExit || inventory == null) return false;
            IsCompleted = true;

            GetComponent<TorchAttack>()?.CancelAttack();
            GetComponent<TorchInteraction>()?.CancelInteraction();
            inventory.ShowCaveSummary();

            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            ownsTimePause = true;
            var character = GetComponent<ProceduralCharacter>();
            if (character != null) character.enabled = false;
            var interaction = GetComponent<TorchInteraction>();
            if (interaction != null) interaction.enabled = false;
            var attack = GetComponent<TorchAttack>();
            if (attack != null) attack.enabled = false;
            return true;
        }

        private void RestoreTimeScale()
        {
            if (!ownsTimePause) return;
            Time.timeScale = previousTimeScale;
            ownsTimePause = false;
        }

        private void OnDisable() => RestoreTimeScale();
        private void OnDestroy() => RestoreTimeScale();
    }
}
