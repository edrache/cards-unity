using CardsUnity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.Tests
{
    public class RectOutlineGraphicTests
    {
        [Test]
        public void PopulateMesh_creates_single_full_rect_quad()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            var outlineObject = new GameObject("Outline", typeof(RectTransform));
            outlineObject.transform.SetParent(canvasObject.transform, false);

            var rectTransform = outlineObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(200f, 100f);

            var outline = outlineObject.AddComponent<TestRectOutlineGraphic>();
            outline.LineThickness = 8f;

            var vertexHelper = new VertexHelper();
            outline.Populate(vertexHelper);

            Assert.AreEqual(4, vertexHelper.currentVertCount);
            Assert.AreEqual(6, vertexHelper.currentIndexCount);

            vertexHelper.Dispose();
            Object.DestroyImmediate(canvasObject);
        }

        [Test]
        public void PopulateMesh_still_creates_quad_for_short_rect()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            var outlineObject = new GameObject("Outline", typeof(RectTransform));
            outlineObject.transform.SetParent(canvasObject.transform, false);

            var rectTransform = outlineObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(120f, 12f);

            var outline = outlineObject.AddComponent<TestRectOutlineGraphic>();
            outline.LineThickness = 6f;

            var vertexHelper = new VertexHelper();
            outline.Populate(vertexHelper);

            Assert.AreEqual(4, vertexHelper.currentVertCount);
            Assert.AreEqual(6, vertexHelper.currentIndexCount);

            vertexHelper.Dispose();
            Object.DestroyImmediate(canvasObject);
        }

        private class TestRectOutlineGraphic : RectOutlineGraphic
        {
            public void Populate(VertexHelper vertexHelper)
            {
                OnPopulateMesh(vertexHelper);
            }
        }
    }
}
