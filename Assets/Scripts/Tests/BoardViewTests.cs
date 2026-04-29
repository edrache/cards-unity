using System.Reflection;
using CardsUnity.UI;
using NUnit.Framework;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class BoardViewTests
    {
        [Test]
        public void HasAnySceneSlotDefinitions_returns_true_when_any_slot_has_definition()
        {
            var boardObject = new GameObject("BoardView");
            var boardView = boardObject.AddComponent<BoardView>();
            var slots = CreateSlotViews(3);

            var definition = ScriptableObject.CreateInstance<SlotDefinition>();
            slots[1].ApplyDefinition(definition);
            SetSlotViews(boardView, slots);

            Assert.IsTrue(boardView.HasAnySceneSlotDefinitions());

            Object.DestroyImmediate(boardObject);
            DestroySlots(slots);
        }

        [Test]
        public void GetSceneSlotDefinitions_returns_definitions_in_slot_order()
        {
            var boardObject = new GameObject("BoardView");
            var boardView = boardObject.AddComponent<BoardView>();
            var slots = CreateSlotViews(3);

            var first = ScriptableObject.CreateInstance<SlotDefinition>();
            var third = ScriptableObject.CreateInstance<SlotDefinition>();
            slots[0].ApplyDefinition(first);
            slots[2].ApplyDefinition(third);
            SetSlotViews(boardView, slots);

            var definitions = boardView.GetSceneSlotDefinitions();

            Assert.AreEqual(3, definitions.Length);
            Assert.AreSame(first, definitions[0]);
            Assert.IsNull(definitions[1]);
            Assert.AreSame(third, definitions[2]);

            Object.DestroyImmediate(boardObject);
            DestroySlots(slots);
        }

        private static SlotView[] CreateSlotViews(int count)
        {
            var slotViews = new SlotView[count];
            for (int i = 0; i < count; i++)
            {
                var slotObject = new GameObject($"Slot_{i}");
                slotViews[i] = slotObject.AddComponent<SlotView>();
            }

            return slotViews;
        }

        private static void DestroySlots(SlotView[] slotViews)
        {
            foreach (var slotView in slotViews)
            {
                if (slotView != null)
                    Object.DestroyImmediate(slotView.gameObject);
            }
        }

        private static void SetSlotViews(BoardView boardView, SlotView[] slotViews)
        {
            var field = typeof(BoardView).GetField("slotViews", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(boardView, slotViews);
        }
    }
}
