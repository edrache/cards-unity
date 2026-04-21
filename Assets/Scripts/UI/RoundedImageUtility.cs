using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace CardsUnity.UI
{
    public static class RoundedImageUtility
    {
        private const int SpriteSize = 64;
        private static readonly Dictionary<int, Sprite> SpritesByRadius = new Dictionary<int, Sprite>();

        public static void ApplyRoundedSprite(Image image, float cornerRadius)
        {
            if (image == null) return;

            int radius = Mathf.Clamp(Mathf.RoundToInt(cornerRadius), 0, SpriteSize / 2);
            image.sprite = GetRoundedSprite(radius);
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
        }

        public static void AddOptionalTrueShadow(GameObject target, float size, float offsetDistance, Color color)
        {
            if (target == null) return;

            Type shadowType = Type.GetType("LeTai.TrueShadow.TrueShadow, LeTai.TrueShadow");
            if (shadowType == null || target.GetComponent(shadowType) != null)
                return;

            Component shadow = target.AddComponent(shadowType);
            SetProperty(shadow, shadowType, "Size", size);
            SetProperty(shadow, shadowType, "OffsetDistance", offsetDistance);
            SetProperty(shadow, shadowType, "OffsetAngle", 270f);
            SetProperty(shadow, shadowType, "Color", color);
            SetProperty(shadow, shadowType, "UseCasterAlpha", true);
        }

        private static Sprite GetRoundedSprite(int radius)
        {
            if (SpritesByRadius.TryGetValue(radius, out Sprite sprite))
                return sprite;

            Texture2D texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false)
            {
                name = $"RoundedRect_{radius}",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32 opaque = new Color32(255, 255, 255, 255);
            Color32 transparent = new Color32(255, 255, 255, 0);
            Color32[] pixels = new Color32[SpriteSize * SpriteSize];

            for (int y = 0; y < SpriteSize; y++)
            {
                for (int x = 0; x < SpriteSize; x++)
                {
                    pixels[y * SpriteSize + x] = IsInsideRoundedRect(x, y, radius) ? opaque : transparent;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Vector4 border = new Vector4(radius, radius, radius, radius);
            sprite = Sprite.Create(texture, new Rect(0f, 0f, SpriteSize, SpriteSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            SpritesByRadius[radius] = sprite;
            return sprite;
        }

        private static bool IsInsideRoundedRect(int x, int y, int radius)
        {
            if (radius <= 0)
                return true;

            int left = radius;
            int right = SpriteSize - radius - 1;
            int bottom = radius;
            int top = SpriteSize - radius - 1;

            int nearestX = Mathf.Clamp(x, left, right);
            int nearestY = Mathf.Clamp(y, bottom, top);
            int dx = x - nearestX;
            int dy = y - nearestY;
            return dx * dx + dy * dy <= radius * radius;
        }

        private static void SetProperty(Component component, Type type, string propertyName, object value)
        {
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.CanWrite)
                property.SetValue(component, value);
        }
    }
}
