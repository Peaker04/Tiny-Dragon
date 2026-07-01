using UnityEngine;

internal static class Level03SpriteFactory
{
    public static Sprite CreateFullTextureSprite(Texture2D texture, float pixelsPerUnit)
    {
        return texture == null
            ? null
            : Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit);
    }

    public static Sprite CreateTrimmedSprite(
        Texture2D texture,
        Color32[] pixels,
        int startX,
        int endX,
        float pixelsPerUnit)
    {
        int minX = endX;
        int maxX = startX - 1;
        int minY = texture.height;
        int maxY = -1;

        for (int y = 0; y < texture.height; y++)
        {
            int rowStart = y * texture.width;
            for (int x = startX; x < endX; x++)
            {
                if (pixels[rowStart + x].a <= 8)
                {
                    continue;
                }

                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return Sprite.Create(
                texture,
                new Rect(startX, 0f, endX - startX, texture.height),
                new Vector2(0.5f, 0f),
                pixelsPerUnit);
        }

        const int padding = 2;
        minX = Mathf.Max(startX, minX - padding);
        maxX = Mathf.Min(endX - 1, maxX + padding);
        minY = Mathf.Max(0, minY - padding);
        maxY = Mathf.Min(texture.height - 1, maxY + padding);

        return Sprite.Create(
            texture,
            new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1),
            new Vector2(0.5f, 0f),
            pixelsPerUnit);
    }
}
