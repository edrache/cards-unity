using System.Reflection;
using CardsUnity.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace CardsUnity.Tests
{
    public class SlotViewTests
    {
        [Test]
        public void ApplyDefinition_combines_multiple_passive_effect_descriptions()
        {
            var slotObject = new GameObject("SlotView", typeof(RectTransform));
            var slotView = slotObject.AddComponent<SlotView>();

            var passiveLabel = CreateLabel("PassiveLabel", slotObject.transform);
            SetPrivateField(slotView, "passiveEffectLabel", passiveLabel);
            SetPrivateField(slotView, "noOpponentCardEffectLabel", CreateLabel("NoOpponentLabel", slotObject.transform));
            SetPrivateField(slotView, "noPlayerCardEffectLabel", CreateLabel("NoPlayerLabel", slotObject.transform));

            var firstEffect = ScriptableObject.CreateInstance<SlotEffectDefinition>();
            firstEffect.context = SlotEffectContext.Passive;
            firstEffect.trigger = SlotEffectTrigger.OnOpponentCardPlaced;
            firstEffect.description = "Opponent Force cards are stronger +2";

            var secondEffect = ScriptableObject.CreateInstance<SlotEffectDefinition>();
            secondEffect.context = SlotEffectContext.Passive;
            secondEffect.trigger = SlotEffectTrigger.OnPlayerTurnStart;
            secondEffect.description = "Opponent Force cards are stronger every turn +1";

            var definition = ScriptableObject.CreateInstance<SlotDefinition>();
            definition.effects = new System.Collections.Generic.List<SlotEffectDefinition> { firstEffect, secondEffect };

            slotView.ApplyDefinition(definition);

            Assert.AreEqual(
                "Opponent Force cards are stronger +2\nOpponent Force cards are stronger every turn +1",
                passiveLabel.text);

            Object.DestroyImmediate(slotObject);
            Object.DestroyImmediate(firstEffect);
            Object.DestroyImmediate(secondEffect);
            Object.DestroyImmediate(definition);
        }

        private static TextMeshProUGUI CreateLabel(string name, Transform parent)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);
            return labelObject.GetComponent<TextMeshProUGUI>();
        }

        private static void SetPrivateField(SlotView slotView, string fieldName, Object value)
        {
            var field = typeof(SlotView).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(slotView, value);
        }
    }
}
