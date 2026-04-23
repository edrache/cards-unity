using CardsUnity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

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
    }
}
