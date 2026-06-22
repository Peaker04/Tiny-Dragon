using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DragonGemEffectPlayer : MonoBehaviour
{
    private sealed class ImagePart
    {
        public int id;
        public int x;
        public int y;
        public int width;
        public int height;
    }

    private sealed class FramePart
    {
        public short[] dx;
        public short[] dy;
        public byte[] imageIds;
    }

    [SerializeField] private Texture2D effectTexture;
    [SerializeField] private TextAsset effectData;
    [SerializeField] private float pixelsPerUnit = 100f;
    [SerializeField] private float effectScale = 0.35f;
    [SerializeField] private float frameRate = 12f;
    [SerializeField] private int sortingOrder = 30;

    private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    private ImagePart[] imageParts;
    private FramePart[] frames;
    private short[] animationFrames;
    private Sprite[] sprites;
    private Material unlitMaterial;
    private Action completion;
    private float timer;
    private int animationIndex;
    private bool isPlaying;

    private void Awake()
    {
        ParseAssets();
        HideRenderers();
    }

    private void Update()
    {
        if (!isPlaying || animationFrames == null || animationFrames.Length == 0)
        {
            return;
        }

        timer += Time.deltaTime;
        float secondsPerFrame = 1f / Mathf.Max(frameRate, 1f);
        while (timer >= secondsPerFrame)
        {
            timer -= secondsPerFrame;
            animationIndex++;
            if (animationIndex >= animationFrames.Length)
            {
                isPlaying = false;
                HideRenderers();
                Action callback = completion;
                completion = null;
                callback?.Invoke();
                return;
            }

            RenderFrame(animationFrames[animationIndex]);
        }
    }

    public void Configure(Texture2D texture, TextAsset data)
    {
        effectTexture = texture;
        effectData = data;
        ParseAssets();
        HideRenderers();
    }

    public void PlayOnce(Action onComplete = null)
    {
        if (frames == null || animationFrames == null || animationFrames.Length == 0)
        {
            onComplete?.Invoke();
            return;
        }

        completion = onComplete;
        animationIndex = 0;
        timer = 0f;
        isPlaying = true;
        RenderFrame(animationFrames[0]);
    }

    private void ParseAssets()
    {
        if (effectTexture == null || effectData == null || effectData.bytes == null)
        {
            return;
        }

        byte[] data = effectData.bytes;
        int offset = 0;

        try
        {
            int imageCount = ReadByte(data, ref offset);
            imageParts = new ImagePart[imageCount];
            sprites = new Sprite[imageCount];
            for (int i = 0; i < imageCount; i++)
            {
                ImagePart part = new ImagePart
                {
                    id = ReadByte(data, ref offset),
                    x = ReadByte(data, ref offset),
                    y = ReadByte(data, ref offset),
                    width = ReadByte(data, ref offset),
                    height = ReadByte(data, ref offset)
                };
                imageParts[i] = part;

                Rect rect = new Rect(
                    part.x,
                    effectTexture.height - part.y - part.height,
                    part.width,
                    part.height);
                sprites[part.id] = Sprite.Create(effectTexture, rect, new Vector2(0f, 1f), pixelsPerUnit);
            }

            int frameCount = ReadInt16(data, ref offset);
            frames = new FramePart[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                int partCount = ReadByte(data, ref offset);
                FramePart frame = new FramePart
                {
                    dx = new short[partCount],
                    dy = new short[partCount],
                    imageIds = new byte[partCount]
                };

                for (int j = 0; j < partCount; j++)
                {
                    frame.dx[j] = ReadInt16(data, ref offset);
                    frame.dy[j] = ReadInt16(data, ref offset);
                    frame.imageIds[j] = (byte)ReadByte(data, ref offset);
                }

                frames[i] = frame;
            }

            int animationCount = ReadInt16(data, ref offset);
            animationFrames = new short[animationCount];
            for (int i = 0; i < animationCount; i++)
            {
                animationFrames[i] = ReadInt16(data, ref offset);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"Cannot parse Dragon Ball effect data: {exception.Message}", this);
            imageParts = null;
            frames = null;
            animationFrames = null;
            sprites = null;
        }
    }

    private void RenderFrame(int frameIndex)
    {
        if (frames == null || frameIndex < 0 || frameIndex >= frames.Length)
        {
            HideRenderers();
            return;
        }

        FramePart frame = frames[frameIndex];
        EnsureRendererCount(frame.imageIds.Length);

        for (int i = 0; i < renderers.Count; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (i >= frame.imageIds.Length)
            {
                renderer.gameObject.SetActive(false);
                continue;
            }

            int imageId = frame.imageIds[i];
            if (sprites == null || imageId >= sprites.Length || sprites[imageId] == null)
            {
                renderer.gameObject.SetActive(false);
                continue;
            }

            renderer.sprite = sprites[imageId];
            renderer.sortingOrder = sortingOrder + i;
            renderer.sharedMaterial = GetUnlitMaterial();
            renderer.transform.localPosition = new Vector3(
                frame.dx[i] * effectScale / pixelsPerUnit,
                -frame.dy[i] * effectScale / pixelsPerUnit,
                0f);
            renderer.transform.localScale = new Vector3(effectScale, effectScale, 1f);
            renderer.gameObject.SetActive(true);
        }
    }

    private void EnsureRendererCount(int count)
    {
        while (renderers.Count < count)
        {
            GameObject part = new GameObject($"EffectPart_{renderers.Count}");
            part.transform.SetParent(transform, false);
            renderers.Add(part.AddComponent<SpriteRenderer>());
        }
    }

    private void HideRenderers()
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            renderers[i].gameObject.SetActive(false);
        }
    }

    private Material GetUnlitMaterial()
    {
        if (unlitMaterial != null)
        {
            return unlitMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit") ?? Shader.Find("Sprites/Default");
        if (shader != null)
        {
            unlitMaterial = new Material(shader);
        }

        return unlitMaterial;
    }

    private static int ReadByte(byte[] data, ref int offset)
    {
        if (offset >= data.Length)
        {
            throw new IndexOutOfRangeException();
        }

        return data[offset++];
    }

    private static short ReadInt16(byte[] data, ref int offset)
    {
        int high = ReadByte(data, ref offset);
        int low = ReadByte(data, ref offset);
        return unchecked((short)((high << 8) | low));
    }
}
