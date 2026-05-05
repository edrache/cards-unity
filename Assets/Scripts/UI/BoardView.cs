using System;
using System.Collections.Generic;
using CardsUnity;
using UnityEngine;

namespace CardsUnity.UI
{
    public class BoardView : MonoBehaviour
    {
        [SerializeField] private SlotView[] slotViews;
        [SerializeField] private Transform mainSlotContainer;
        [SerializeField] private Transform passiveSlotContainer;
        [SerializeField] private SlotView slotPrefab;
        [SerializeField] private CardView opponentCardPrefab;

        public Action<CardInstance, int> OnCardPlayed;

        private readonly List<SlotView> _runtimeSlotViews = new();

        private void Awake()
        {
            if (mainSlotContainer == null)
                mainSlotContainer = transform;

            RegisterAllSlotViews();
        }

        public void Refresh(BoardState board)
        {
            if (board == null || board.Slots == null)
                return;

            RegisterAllSlotViews();
            int count = Math.Min(_runtimeSlotViews.Count, board.Slots.Length);
            for (int i = 0; i < count; i++)
            {
                var slotView = _runtimeSlotViews[i];
                var slot = board.Slots[i];
                if (slotView == null || slot == null)
                    continue;

                slotView.gameObject.SetActive(slot.IsActive);
                if (!slot.IsActive)
                {
                    slotView.RefreshClock(null);
                    slotView.ClearPlayerCard();
                    slotView.ClearOpponentCard();
                    continue;
                }

                slotView.ApplyDefinition(slot.Definition);
                slotView.RefreshClock(slot.ClockState);
                slotView.SetHighlight(false);

                if (slot.HasPlayerCard)
                    slotView.ShowPlayerCard(slot.PlayerCard, opponentCardPrefab);
                else
                    slotView.ClearPlayerCard();

                if (slot.HasOpponentCard)
                    slotView.ShowOpponentCard(slot.OpponentCard, opponentCardPrefab);
                else
                    slotView.ClearOpponentCard();
            }

            for (int i = count; i < _runtimeSlotViews.Count; i++)
            {
                if (_runtimeSlotViews[i] == null)
                    continue;

                _runtimeSlotViews[i].gameObject.SetActive(false);
                _runtimeSlotViews[i].SetHighlight(false);
                _runtimeSlotViews[i].RefreshClock(null);
                _runtimeSlotViews[i].ClearPlayerCard();
                _runtimeSlotViews[i].ClearOpponentCard();
            }
        }

        public int CreateRuntimeSlot(SlotDefinition definition)
        {
            if (slotPrefab == null)
            {
                Debug.LogError("BoardView is missing a slotPrefab reference.");
                return -1;
            }

            var parent = ResolveContainer(definition);
            var slotView = Instantiate(slotPrefab, parent);
            slotView.name = definition != null ? $"Slot_Runtime_{definition.name}" : "Slot_Runtime";
            slotView.gameObject.SetActive(true);
            slotView.ApplyDefinition(definition);
            RegisterSlotView(slotView);
            return slotView.SlotIndex;
        }

        public void SetHighlightAll(bool active)
        {
            RegisterAllSlotViews();

            foreach (var slotView in _runtimeSlotViews)
            {
                if (slotView != null)
                    slotView.SetHighlight(active);
            }
        }

        public void ConfigureSlots(SlotDefinition[] definitions)
        {
            RegisterAllSlotViews();

            if (definitions == null)
            {
                for (int i = 0; i < _runtimeSlotViews.Count; i++)
                {
                    if (_runtimeSlotViews[i] != null)
                        _runtimeSlotViews[i].ApplyDefinition(null);
                }

                return;
            }

            int count = Math.Min(_runtimeSlotViews.Count, definitions.Length);
            for (int i = 0; i < count; i++)
            {
                if (_runtimeSlotViews[i] != null)
                    _runtimeSlotViews[i].ApplyDefinition(definitions[i]);
            }

            for (int i = count; i < _runtimeSlotViews.Count; i++)
            {
                if (_runtimeSlotViews[i] != null)
                    _runtimeSlotViews[i].ApplyDefinition(null);
            }
        }

        public SlotDefinition[] GetSceneSlotDefinitions()
        {
            RegisterAllSlotViews();

            var definitions = new SlotDefinition[_runtimeSlotViews.Count];
            for (int i = 0; i < _runtimeSlotViews.Count; i++)
                definitions[i] = _runtimeSlotViews[i] != null ? _runtimeSlotViews[i].Definition : null;

            return definitions;
        }

        public bool HasAnySceneSlotDefinitions()
        {
            RegisterAllSlotViews();

            foreach (var slotView in _runtimeSlotViews)
            {
                if (slotView != null && slotView.Definition != null)
                    return true;
            }

            return false;
        }

        public bool[] GetSceneSlotActiveStates()
        {
            RegisterAllSlotViews();

            var activeStates = new bool[_runtimeSlotViews.Count];
            for (int i = 0; i < _runtimeSlotViews.Count; i++)
                activeStates[i] = _runtimeSlotViews[i] != null && _runtimeSlotViews[i].StartsActiveInBoard;

            return activeStates;
        }

        private Transform ResolveContainer(SlotDefinition definition)
        {
            if (definition != null && definition.spawnInPassiveContainer && passiveSlotContainer != null)
                return passiveSlotContainer;

            return mainSlotContainer != null ? mainSlotContainer : transform;
        }

        private void RegisterAllSlotViews()
        {
            _runtimeSlotViews.Clear();

            if (slotViews != null)
            {
                foreach (var slotView in slotViews)
                {
                    if (slotView != null)
                        _runtimeSlotViews.Add(slotView);
                }
            }

            var discoveredViews = GetComponentsInChildren<SlotView>(true);
            foreach (var slotView in discoveredViews)
            {
                if (slotView == null || _runtimeSlotViews.Contains(slotView))
                    continue;

                _runtimeSlotViews.Add(slotView);
            }

            for (int i = 0; i < _runtimeSlotViews.Count; i++)
                WireSlotView(_runtimeSlotViews[i], i);
        }

        private void RegisterSlotView(SlotView slotView)
        {
            if (slotView == null)
                return;

            RegisterAllSlotViews();
            int index = _runtimeSlotViews.IndexOf(slotView);
            if (index >= 0)
                WireSlotView(slotView, index);
        }

        private void WireSlotView(SlotView slotView, int index)
        {
            if (slotView == null)
                return;

            slotView.SlotIndex = index;
            slotView.OnCardDropped = (cardView, droppedSlotIndex) =>
                OnCardPlayed?.Invoke(cardView.Card, droppedSlotIndex);
        }
    }
}
