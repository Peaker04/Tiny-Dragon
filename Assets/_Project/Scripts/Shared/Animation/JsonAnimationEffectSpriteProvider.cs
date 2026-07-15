using System.Collections.Generic;
using UnityEngine;

namespace TinyDragon.Shared.Animation
{
    public static class JsonAnimationEffectSpriteProvider
    {
        public static Sprite CreateLargestAttackEffectSprite(
            JsonMultipartAnimationData data,
            Texture2D texture,
            float pixelsPerUnit,
            float textureCoordinateScale
        )
        {
            if (data == null || data.frames == null || data.imageInfos == null)
            {
                return null;
            }

            int bestSpriteIndex = -1;
            int bestArea = 0;
            int startFrame = Mathf.Clamp(6, 0, data.frames.Length);
            int endFrame = Mathf.Max(startFrame, data.frames.Length - 1);

            for (int frameIndex = startFrame; frameIndex < endFrame; frameIndex++)
            {
                FramePartData frame = data.frames[frameIndex];
                if (frame == null || frame.idImg == null)
                {
                    continue;
                }

                foreach (int spriteIndex in frame.idImg)
                {
                    if (spriteIndex < 0 || spriteIndex >= data.imageInfos.Length)
                    {
                        continue;
                    }

                    SpritePartInfo info = data.imageInfos[spriteIndex];
                    if (info.w < 20 || info.h < 20)
                    {
                        continue;
                    }

                    int area = info.w * info.h;
                    if (area <= bestArea)
                    {
                        continue;
                    }

                    bestArea = area;
                    bestSpriteIndex = spriteIndex;
                }
            }

            return bestSpriteIndex >= 0
                ? JsonMultipartSpriteFactory.CreateSprite(texture, data.imageInfos[bestSpriteIndex], pixelsPerUnit, textureCoordinateScale)
                : null;
        }

        public static Sprite[] CreateActionEffectSprites(
            JsonMultipartAnimationData data,
            Texture2D texture,
            Sprite[] sprites,
            string actionName,
            int minimumSize,
            float minimumForwardOffset,
            float pixelsPerUnit,
            float textureCoordinateScale
        )
        {
            if (data == null || data.frames == null || data.imageInfos == null)
            {
                return new Sprite[0];
            }

            JsonAnimationActionData action = FindAction(data, actionName) ?? FindAction(data, "Attack2") ?? FindAction(data, "Attack1");
            if (action == null || action.frameIndices == null)
            {
                return new Sprite[0];
            }

            List<Sprite> effectSprites = new List<Sprite>();
            int clampedMinimumSize = Mathf.Max(1, minimumSize);

            foreach (int frameIndex in action.frameIndices)
            {
                if (frameIndex < 0 || frameIndex >= data.frames.Length)
                {
                    continue;
                }

                FramePartData frame = data.frames[frameIndex];
                if (frame == null || frame.idImg == null || frame.idImg.Length <= 1)
                {
                    continue;
                }

                for (int i = frame.idImg.Length - 1; i >= 0; i--)
                {
                    if (frame.dx != null && i < frame.dx.Length && frame.dx[i] < minimumForwardOffset)
                    {
                        continue;
                    }

                    int spriteIndex = frame.idImg[i];
                    if (spriteIndex < 0 || spriteIndex >= data.imageInfos.Length)
                    {
                        continue;
                    }

                    SpritePartInfo info = data.imageInfos[spriteIndex];
                    if (info == null || Mathf.Max(info.w, info.h) < clampedMinimumSize)
                    {
                        continue;
                    }

                    Sprite sprite = sprites != null && spriteIndex < sprites.Length && sprites[spriteIndex] != null
                        ? sprites[spriteIndex]
                        : JsonMultipartSpriteFactory.CreateSprite(texture, info, pixelsPerUnit, textureCoordinateScale);
                    if (sprite != null)
                    {
                        effectSprites.Add(sprite);
                        break;
                    }
                }
            }

            if (effectSprites.Count > 0)
            {
                return effectSprites.ToArray();
            }

            Sprite fallback = CreateLargestAttackEffectSprite(data, texture, pixelsPerUnit, textureCoordinateScale);
            return fallback != null ? new[] { fallback } : new Sprite[0];
        }

        public static JsonAnimationActionData FindAction(JsonMultipartAnimationData data, string actionName)
        {
            if (data == null || data.actions == null || string.IsNullOrWhiteSpace(actionName))
            {
                return null;
            }

            foreach (JsonAnimationActionData action in data.actions)
            {
                if (action != null && action.name == actionName)
                {
                    return action;
                }
            }

            return null;
        }
    }
}
