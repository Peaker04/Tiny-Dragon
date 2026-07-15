using System;
using System.Collections.Generic;
using UnityEngine;

#pragma warning disable CS0649
public sealed class FideFrameAnimationBridge : MonoBehaviour
{
    [Serializable]
    private sealed class CanvasInfo
    {
        public int width;
        public int height;
    }

    [Serializable]
    private sealed class PivotInfo
    {
        public int x;
        public int y;
    }

    [Serializable]
    private sealed class FrameMetadata
    {
        public CanvasInfo canvas;
        public PivotInfo pivot;
        public float scale = 4f;
    }

    [Serializable]
    public struct AnimatorMap
    {
        public string animatorStateName;
        public string actionName;
    }

    [Serializable]
    public struct ActionFrameRange
    {
        public string actionName;
        public int startFrame;
        public int endFrameExclusive;
        public bool loop;
    }

    [Header("Resources")]
    [SerializeField] private string resourceFolder = "Enemies/Fide/FideDaiCa3";
    [SerializeField] private int frameCount = 33;
    [SerializeField] private bool useLeftFrames;

    [Header("Rendering")]
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private float frameRate = 12f;
    [SerializeField] private float pixelsPerUnit = 100f;
    [SerializeField] private float renderScale = 0.7f;
    [SerializeField] private bool useUnlitMaterial = true;

    [Header("Playback")]
    [SerializeField] private string defaultActionName = "Stand";
    [SerializeField] private ActionFrameRange[] actionRanges =
    {
        new ActionFrameRange { actionName = "Stand", startFrame = 0, endFrameExclusive = 2, loop = true },
        new ActionFrameRange { actionName = "Move", startFrame = 2, endFrameExclusive = 6, loop = true },
        new ActionFrameRange { actionName = "Attack1", startFrame = 6, endFrameExclusive = 11, loop = false },
        new ActionFrameRange { actionName = "Attack2", startFrame = 11, endFrameExclusive = 17, loop = false },
        new ActionFrameRange { actionName = "Attack3", startFrame = 17, endFrameExclusive = 25, loop = false },
        new ActionFrameRange { actionName = "Hurt", startFrame = 25, endFrameExclusive = 28, loop = false },
        new ActionFrameRange { actionName = "Die", startFrame = 28, endFrameExclusive = 33, loop = false }
    };

    [Header("Animation Mapping")]
    [SerializeField] private AnimatorMap[] customMappings =
    {
        new AnimatorMap { animatorStateName = "Idle", actionName = "Stand" },
        new AnimatorMap { animatorStateName = "Stand", actionName = "Stand" },
        new AnimatorMap { animatorStateName = "Boss_idle", actionName = "Stand" },
        new AnimatorMap { animatorStateName = "mob_idle", actionName = "Stand" },
        new AnimatorMap { animatorStateName = "Walk", actionName = "Move" },
        new AnimatorMap { animatorStateName = "Move", actionName = "Move" },
        new AnimatorMap { animatorStateName = "Run", actionName = "Move" },
        new AnimatorMap { animatorStateName = "Boss_move_dash", actionName = "Move" },
        new AnimatorMap { animatorStateName = "MoveDash", actionName = "Move" },
        new AnimatorMap { animatorStateName = "Attack", actionName = "Attack1" },
        new AnimatorMap { animatorStateName = "Attack1", actionName = "Attack1" },
        new AnimatorMap { animatorStateName = "SlashAttack", actionName = "Attack1" },
        new AnimatorMap { animatorStateName = "Boss_slash_attack", actionName = "Attack1" },
        new AnimatorMap { animatorStateName = "rangeAttack", actionName = "Attack2" },
        new AnimatorMap { animatorStateName = "EnergyBlast", actionName = "Attack2" },
        new AnimatorMap { animatorStateName = "Boss_energy_blast", actionName = "Attack2" },
        new AnimatorMap { animatorStateName = "ComboSlashBlast", actionName = "Attack3" },
        new AnimatorMap { animatorStateName = "Boss_combo_slash_blast", actionName = "Attack3" },
        new AnimatorMap { animatorStateName = "Hurt", actionName = "Hurt" },
        new AnimatorMap { animatorStateName = "Hit", actionName = "Hurt" },
        new AnimatorMap { animatorStateName = "Boss_hit_or_special", actionName = "Hurt" },
        new AnimatorMap { animatorStateName = "Die", actionName = "Die" }
    };

    private readonly Dictionary<int, string> hashToActionName = new Dictionary<int, string>();
    private readonly Dictionary<string, int[]> actions = new Dictionary<string, int[]>();
    private readonly Dictionary<string, bool> actionLoops = new Dictionary<string, bool>();
    private Sprite[] frames;
    private Animator animator;
    private Material unlitMaterial;
    private string currentActionName;
    private int currentActionFrameIndex;
    private float frameTimer;
    private int lastStateHash = -1;

    private void Awake()
    {
        Prepare();
        LoadFrames();
    }

    private void OnEnable()
    {
        Prepare();
        LoadFrames();
        PlayAction(defaultActionName);
    }

    private void OnValidate()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        Prepare();
        LoadFrames();
        PlayAction(defaultActionName);
        RenderEditorPreviewFrame();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            RenderEditorPreviewFrame();
            return;
        }

        if (frames == null || frames.Length == 0)
        {
            LoadFrames();
        }

        SyncWithAnimator();
        UpdateAnimation(Time.deltaTime);
    }

    private void Prepare()
    {
        animator = GetComponent<Animator>();
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        if (targetRenderer != null)
        {
            targetRenderer.transform.localScale = new Vector3(renderScale, renderScale, 1f);
            if (useUnlitMaterial)
            {
                targetRenderer.sharedMaterial = GetUnlitMaterial();
            }
        }

        hashToActionName.Clear();
        foreach (AnimatorMap map in customMappings)
        {
            if (!string.IsNullOrWhiteSpace(map.animatorStateName) && !string.IsNullOrWhiteSpace(map.actionName))
            {
                hashToActionName[Animator.StringToHash(map.animatorStateName)] = map.actionName;
            }
        }

        BuildActions();
    }

    private void LoadFrames()
    {
        if (targetRenderer == null || string.IsNullOrWhiteSpace(resourceFolder))
        {
            return;
        }

        string folder = useLeftFrames ? $"{resourceFolder}/left" : resourceFolder;
        Sprite[] spriteAssets = Resources.LoadAll<Sprite>(folder);
        if (spriteAssets != null && spriteAssets.Length > 0)
        {
            Array.Sort(spriteAssets, (a, b) => string.CompareOrdinal(a.name, b.name));
            frames = spriteAssets;
            frameCount = frames.Length;
            return;
        }

        FrameMetadata metadata = LoadMetadata(folder);
        int count = Mathf.Max(1, frameCount);
        frames = new Sprite[count];

        for (int i = 0; i < count; i++)
        {
            string framePath = $"{folder}/pose_{i:00}";
            Texture2D texture = Resources.Load<Texture2D>(framePath);
            if (texture == null)
            {
                continue;
            }

            frames[i] = CreateFrameSprite(texture, metadata);
        }

        if (Array.Exists(frames, frame => frame != null))
        {
            return;
        }

        Debug.LogWarning($"FideFrameAnimationBridge could not load any frames from Resources/{folder}.", this);
    }

    private FrameMetadata LoadMetadata(string folder)
    {
        TextAsset metadataAsset = Resources.Load<TextAsset>($"{folder}/frames");
        if (metadataAsset == null)
        {
            return null;
        }

        return JsonUtility.FromJson<FrameMetadata>(metadataAsset.text);
    }

    private Sprite CreateFrameSprite(Texture2D texture, FrameMetadata metadata)
    {
        Vector2 pivot = new Vector2(0.5f, 0f);
        if (metadata != null && metadata.pivot != null)
        {
            pivot = new Vector2(
                Mathf.Clamp01(metadata.pivot.x / (float)texture.width),
                Mathf.Clamp01((texture.height - metadata.pivot.y) / (float)texture.height)
            );
        }

        Rect rect = new Rect(0f, 0f, texture.width, texture.height);
        return Sprite.Create(texture, rect, pivot, pixelsPerUnit);
    }

    private void BuildActions()
    {
        actions.Clear();
        actionLoops.Clear();

        if (actionRanges == null || actionRanges.Length == 0)
        {
            actions["Stand"] = Range(0, Mathf.Max(frameCount, 1));
            actionLoops["Stand"] = true;
            return;
        }

        foreach (ActionFrameRange actionRange in actionRanges)
        {
            if (string.IsNullOrWhiteSpace(actionRange.actionName))
            {
                continue;
            }

            int startFrame = Mathf.Clamp(actionRange.startFrame, 0, Mathf.Max(frameCount - 1, 0));
            int endFrame = Mathf.Clamp(actionRange.endFrameExclusive, startFrame + 1, Mathf.Max(frameCount, 1));
            actions[actionRange.actionName] = Range(startFrame, endFrame);
            actionLoops[actionRange.actionName] = actionRange.loop;
        }

        if (!actions.ContainsKey(defaultActionName))
        {
            defaultActionName = actions.ContainsKey("Stand") ? "Stand" : FirstActionName();
        }
    }

    private void SyncWithAnimator()
    {
        if (animator == null)
        {
            return;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        int stateHash = stateInfo.shortNameHash;
        if (stateHash == lastStateHash)
        {
            return;
        }

        lastStateHash = stateHash;
        PlayAction(hashToActionName.TryGetValue(stateHash, out string actionName) ? actionName : defaultActionName);
    }

    public void PlayAction(string actionName)
    {
        if (currentActionName == actionName || !actions.ContainsKey(actionName))
        {
            return;
        }

        currentActionName = actionName;
        currentActionFrameIndex = 0;
        frameTimer = 0f;
        RenderCurrentFrame();
    }

    private void UpdateAnimation(float deltaTime)
    {
        if (string.IsNullOrWhiteSpace(currentActionName) || !actions.TryGetValue(currentActionName, out int[] actionFrames))
        {
            PlayAction(defaultActionName);
            return;
        }

        if (actionFrames.Length == 0)
        {
            return;
        }

        frameTimer += deltaTime;
        float timePerFrame = 1f / Mathf.Max(frameRate, 1f);
        if (frameTimer < timePerFrame)
        {
            return;
        }

        frameTimer -= timePerFrame;
        if (currentActionFrameIndex >= actionFrames.Length - 1 && !IsCurrentActionLooping())
        {
            PlayAction(defaultActionName);
            return;
        }

        currentActionFrameIndex = (currentActionFrameIndex + 1) % actionFrames.Length;
        RenderCurrentFrame();
    }

    private void RenderCurrentFrame()
    {
        if (targetRenderer == null || frames == null || string.IsNullOrWhiteSpace(currentActionName))
        {
            return;
        }

        if (!actions.TryGetValue(currentActionName, out int[] actionFrames) || actionFrames.Length == 0)
        {
            return;
        }

        int frameIndex = actionFrames[Mathf.Clamp(currentActionFrameIndex, 0, actionFrames.Length - 1)];
        if (frameIndex < 0 || frameIndex >= frames.Length || frames[frameIndex] == null)
        {
            return;
        }

        targetRenderer.sprite = frames[frameIndex];
    }

    private void RenderEditorPreviewFrame()
    {
        if (Application.isPlaying)
        {
            return;
        }

        if (frames == null || frames.Length == 0)
        {
            LoadFrames();
        }

        if (targetRenderer != null && frames != null && frames.Length > 0 && frames[0] != null)
        {
            targetRenderer.sprite = frames[0];
        }
    }

    private Material GetUnlitMaterial()
    {
        if (unlitMaterial != null)
        {
            return unlitMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader != null)
        {
            unlitMaterial = new Material(shader);
        }

        return unlitMaterial;
    }

    private static int[] Range(int startInclusive, int endExclusive)
    {
        endExclusive = Mathf.Max(startInclusive + 1, endExclusive);
        int[] values = new int[endExclusive - startInclusive];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = startInclusive + i;
        }

        return values;
    }

    private bool IsCurrentActionLooping()
    {
        return string.IsNullOrWhiteSpace(currentActionName)
            || !actionLoops.TryGetValue(currentActionName, out bool loop)
            || loop;
    }

    private string FirstActionName()
    {
        foreach (string actionName in actions.Keys)
        {
            return actionName;
        }

        return "Stand";
    }
}
#pragma warning restore CS0649
