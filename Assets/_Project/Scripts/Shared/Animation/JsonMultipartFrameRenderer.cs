using System.Collections.Generic;
using UnityEngine;

namespace TinyDragon.Shared.Animation
{
    public class JsonMultipartFrameRenderer
    {
        private const string PartNamePrefix = "Part_";

        private readonly MonoBehaviour owner;
        private readonly List<SpriteRenderer> partRenderers = new List<SpriteRenderer>();
        private bool partRendererCacheInitialized;
        private float runtimeVisualOffsetY;

        public JsonMultipartFrameRenderer(MonoBehaviour owner)
        {
            this.owner = owner;
        }

        public void CacheExistingPartRenderers()
        {
            if (partRendererCacheInitialized || owner == null)
            {
                return;
            }

            partRendererCacheInitialized = true;
            partRenderers.Clear();

            Transform root = owner.transform;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (!TryGetPartIndex(child.name, out int partIndex))
                {
                    continue;
                }

                while (partRenderers.Count <= partIndex)
                {
                    partRenderers.Add(null);
                }

                SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
                if (partRenderers[partIndex] == null)
                {
                    if (renderer == null)
                    {
                        renderer = child.gameObject.AddComponent<SpriteRenderer>();
                    }

                    child.name = PartNamePrefix + partIndex;
                    partRenderers[partIndex] = renderer;
                    continue;
                }

                DestroyDuplicatePart(child.gameObject);
            }
        }

        public void RenderFrame(
            int frameIndex,
            JsonMultipartAnimationData data,
            Sprite[] sprites,
            SpriteRenderer parentRenderer,
            Material material,
            bool useUnlitMaterial,
            float scale,
            float pixelsPerUnit,
            float textureCoordinateScale
        )
        {
            if (data == null || data.frames == null || frameIndex < 0 || frameIndex >= data.frames.Length)
            {
                HideAllParts();
                return;
            }

            FramePartData frame = data.frames[frameIndex];
            if (frame == null || frame.idImg == null)
            {
                HideAllParts();
                return;
            }

            CacheExistingPartRenderers();
            int partCount = frame.idImg.Length;
            while (partRenderers.Count < partCount)
            {
                partRenderers.Add(null);
            }

            int baseSortingLayerId = parentRenderer != null ? parentRenderer.sortingLayerID : 0;
            int baseSortingOrder = parentRenderer != null ? parentRenderer.sortingOrder : 0;
            bool isFlipped = parentRenderer != null && parentRenderer.flipX;

            for (int i = 0; i < partRenderers.Count; i++)
            {
                SpriteRenderer renderer = GetOrCreatePartRenderer(i);
                if (i >= partCount)
                {
                    renderer.gameObject.SetActive(false);
                    continue;
                }

                int imageId = frame.idImg[i];
                if (imageId < 0 || sprites == null || imageId >= sprites.Length || sprites[imageId] == null)
                {
                    renderer.gameObject.SetActive(false);
                    continue;
                }

                renderer.gameObject.layer = owner.gameObject.layer;
                renderer.sprite = sprites[imageId];
                renderer.sortingLayerID = baseSortingLayerId;
                renderer.sortingOrder = baseSortingOrder + i;
                renderer.flipX = isFlipped;
                renderer.sharedMaterial = ResolveMaterial(parentRenderer, material, useUnlitMaterial);
                if (parentRenderer != null)
                {
                    renderer.color = parentRenderer.color;
                }

                renderer.transform.localScale = new Vector3(scale, scale, 1f);
                renderer.transform.localPosition = CalculateLocalPosition(data, frame, imageId, i, isFlipped, scale, pixelsPerUnit, textureCoordinateScale);
                renderer.gameObject.SetActive(true);
            }
        }

        public void SetRuntimeVisualOffsetY(float offsetY)
        {
            if (Mathf.Approximately(runtimeVisualOffsetY, offsetY))
            {
                return;
            }

            float deltaY = offsetY - runtimeVisualOffsetY;
            runtimeVisualOffsetY = offsetY;

            foreach (SpriteRenderer renderer in partRenderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                Vector3 localPosition = renderer.transform.localPosition;
                localPosition.y += deltaY;
                renderer.transform.localPosition = localPosition;
            }
        }

        public void ClearParentSprite(SpriteRenderer parentRenderer, bool suppressParentSpriteRenderer)
        {
            if (suppressParentSpriteRenderer && parentRenderer != null)
            {
                parentRenderer.sprite = null;
            }
        }

        private Vector3 CalculateLocalPosition(
            JsonMultipartAnimationData data,
            FramePartData frame,
            int imageId,
            int partIndex,
            bool isFlipped,
            float scale,
            float pixelsPerUnit,
            float textureCoordinateScale
        )
        {
            SpritePartInfo info = data.imageInfos[imageId];
            float coordScale = Mathf.Max(1f, textureCoordinateScale);
            float dx = frame.dx != null && partIndex < frame.dx.Length ? frame.dx[partIndex] * coordScale : 0f;
            float dy = frame.dy != null && partIndex < frame.dy.Length ? frame.dy[partIndex] * coordScale : 0f;
            float centerX = dx + info.w * coordScale / 2f;
            float centerY = dy + info.h * coordScale / 2f;

            return isFlipped
                ? new Vector3(-centerX * scale / pixelsPerUnit, -centerY * scale / pixelsPerUnit + runtimeVisualOffsetY, 0f)
                : new Vector3(centerX * scale / pixelsPerUnit, -centerY * scale / pixelsPerUnit + runtimeVisualOffsetY, 0f);
        }

        private SpriteRenderer GetOrCreatePartRenderer(int index)
        {
            while (partRenderers.Count <= index)
            {
                partRenderers.Add(null);
            }

            SpriteRenderer renderer = partRenderers[index];
            if (renderer != null)
            {
                return renderer;
            }

            Transform existing = owner.transform.Find(PartNamePrefix + index);
            GameObject child = existing != null ? existing.gameObject : new GameObject(PartNamePrefix + index);
            child.transform.SetParent(owner.transform, false);
            child.layer = owner.gameObject.layer;

            renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = child.AddComponent<SpriteRenderer>();
            }

            partRenderers[index] = renderer;
            return renderer;
        }

        private void HideAllParts()
        {
            foreach (SpriteRenderer renderer in partRenderers)
            {
                if (renderer != null)
                {
                    renderer.gameObject.SetActive(false);
                }
            }
        }

        private static Material ResolveMaterial(SpriteRenderer parentRenderer, Material material, bool useUnlitMaterial)
        {
            if (useUnlitMaterial && material != null)
            {
                return material;
            }

            return parentRenderer != null ? parentRenderer.sharedMaterial : material;
        }

        private static bool TryGetPartIndex(string childName, out int partIndex)
        {
            partIndex = -1;
            if (string.IsNullOrWhiteSpace(childName) || !childName.StartsWith(PartNamePrefix))
            {
                return false;
            }

            int start = PartNamePrefix.Length;
            int length = 0;
            while (start + length < childName.Length && char.IsDigit(childName[start + length]))
            {
                length++;
            }

            return length > 0 && int.TryParse(childName.Substring(start, length), out partIndex);
        }

        private static void DestroyDuplicatePart(GameObject duplicate)
        {
            duplicate.SetActive(false);
            if (Application.isPlaying)
            {
                Object.Destroy(duplicate);
                return;
            }

            Object.DestroyImmediate(duplicate);
        }
    }
}
