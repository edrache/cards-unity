using CardsUnity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace CardsUnity.Tests
{
    public class CardViewTests
    {
        [Test]
        public void Drag_toggles_blocksRaycasts_to_allow_slot_drop()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var cardObject = new GameObject("CardView");
            cardObject.transform.SetParent(canvasObject.transform, false);
            var cardView = cardObject.AddComponent<CardView>();

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            var eventData = new PointerEventData(eventSystemObject.GetComponent<EventSystem>());

            cardView.OnBeginDrag(eventData);
            var canvasGroup = cardObject.GetComponent<CanvasGroup>();
            Assert.IsFalse(canvasGroup.blocksRaycasts);

            cardView.OnEndDrag(eventData);
            Assert.IsTrue(canvasGroup.blocksRaycasts);

            Object.DestroyImmediate(eventSystemObject);
            Object.DestroyImmediate(canvasObject);
        }

        [Test]
        public void SetCardInstance_rotates_card_when_instance_is_exhausted()
        {
            var cardObject = new GameObject("CardView", typeof(RectTransform));
            LogAssert.ignoreFailingMessages = true;

            try
            {
                var cardView = cardObject.AddComponent<CardView>();
                SetPrivateField(cardView, "_baseLocalRotation", cardObject.transform.localRotation);

                var definition = ScriptableObject.CreateInstance<CardDefinition>();
                definition.title = "Exhaust Test";
                definition.value = 3;

                var instance = new CardInstance(definition)
                {
                    IsExhausted = true
                };

                cardView.SetCardInstance(instance);

                Assert.AreEqual(-18f, cardObject.transform.localEulerAngles.z > 180f
                    ? cardObject.transform.localEulerAngles.z - 360f
                    : cardObject.transform.localEulerAngles.z, 0.01f);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
                Object.DestroyImmediate(cardObject);
            }
        }

        private static void SetPrivateField(CardView cardView, string fieldName, object value)
        {
            var field = typeof(CardView).GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field.SetValue(cardView, value);
        }
    }
}
