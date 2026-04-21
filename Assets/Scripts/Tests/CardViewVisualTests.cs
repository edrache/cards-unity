using CardsUnity.Config;
using CardsUnity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.Tests
{
    public sealed class CardViewVisualTests
    {
        [Test]
        public void ApplyVisualConfig_UsesConfiguredCardDimensions()
        {
            GameObject cardObject = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(CardView));
            CardVisualConfig config = ScriptableObject.CreateInstance<CardVisualConfig>();

            try
            {
                CardView view = cardObject.GetComponent<CardView>();
                view.Initialize(cardObject.GetComponent<Image>(), null, null, null, null, null, null, null, null, null, config);

                LayoutElement layout = cardObject.GetComponent<LayoutElement>();

                Assert.AreEqual(config.Width, layout.preferredWidth);
                Assert.AreEqual(config.Height, layout.preferredHeight);
                Assert.AreEqual(config.Width, layout.minWidth);
                Assert.AreEqual(config.Height, layout.minHeight);
            }
            finally
            {
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(cardObject);
            }
        }

        [Test]
        public void ApplyVisualConfig_AppliesRoundedSlicedSprite()
        {
            GameObject cardObject = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(CardView));
            CardVisualConfig config = ScriptableObject.CreateInstance<CardVisualConfig>();

            try
            {
                Image image = cardObject.GetComponent<Image>();
                CardView view = cardObject.GetComponent<CardView>();

                view.Initialize(image, null, null, null, null, null, null, null, null, null, config);

                Assert.IsNotNull(image.sprite);
                Assert.AreEqual(Image.Type.Sliced, image.type);
                Assert.AreEqual(config.CornerRadius, image.sprite.border.x);
            }
            finally
            {
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(cardObject);
            }
        }
    }
}
