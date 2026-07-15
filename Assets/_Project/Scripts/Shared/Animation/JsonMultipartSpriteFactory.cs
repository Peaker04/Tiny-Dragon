using UnityEngine;

namespace TinyDragon.Shared.Animation
{
    public static class JsonMultipartSpriteFactory
    {
        public static Sprite[] CreateSprites(
            Texture2D texture,
            SpritePartInfo[] imageInfos,
            float pixelsPerUnit,
            float textureCoordinateScale,
            bool ignoreTinyPlaceholderParts
        )
        {
            if (texture == null || imageInfos == null)
            {
                return new Sprite[0];
            }

            Sprite[] sprites = new Sprite[imageInfos.Length];
            for (int i = 0; i < imageInfos.Length; i++)
            {
                SpritePartInfo info = imageInfos[i];
                if (IsIgnoredPlaceholderPart(info, ignoreTinyPlaceholderParts))
                {
                    sprites[i] = null;
                    continue;
                }

                sprites[i] = CreateSprite(texture, info, pixelsPerUnit, textureCoordinateScale);
            }

            return sprites;
        }

        public static Sprite CreateSprite(Texture2D texture, SpritePartInfo info, float pixelsPerUnit, float textureCoordinateScale)
        {
            if (texture == null || info == null)
            {
                return null;
            }

            float coordScale = Mathf.Max(1f, textureCoordinateScale);
            float x = info.x0 * coordScale;
            float y = texture.height - (info.y0 + info.h) * coordScale;
            Rect rect = new Rect(x, y, info.w * coordScale, info.h * coordScale);
            return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        private static bool IsIgnoredPlaceholderPart(SpritePartInfo info, bool ignoreTinyPlaceholderParts)
        {
            return ignoreTinyPlaceholderParts
                && info != null
                && info.w <= 4
                && info.h <= 4;
        }
    }
}
