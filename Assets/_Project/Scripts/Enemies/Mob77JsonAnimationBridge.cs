using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class Mob77JsonAnimationBridge : MonoBehaviour
{
    private const string PartNamePrefix = "Part_";

    [System.Serializable]
    public class SpritePart
    {
        public int ID;
        public int x0;
        public int y0;
        public int w;
        public int h;
    }

    [System.Serializable]
    public class FramePart
    {
        public int[] dx;
        public int[] dy;
        public int[] idImg;
    }

    [System.Serializable]
    public class ActionData
    {
        public int index;
        public string name;
        public int[] frameIndices;
    }

    [System.Serializable]
    public class MobData
    {
        public int monsterId;
        public int type;
        public int typeData;
        public SpritePart[] imageInfos;
        public FramePart[] frames;
        public ActionData[] actions;
    }

    [System.Serializable]
    private class RawSpritePart
    {
        public int id;
        public int x;
        public int y;
        public int w;
        public int h;
    }

    [System.Serializable]
    private class RawMobData
    {
        public int id;
        public int type;
        public int type_data;
        public RawSpritePart[] sprites;
        public FramePart[] frames;
        public int[] animations;
    }

    [Header("Assets")]
    public Texture2D texture;
    public TextAsset jsonFile;

    [Header("Settings")]
    public float frameRate = 12f;
    public float pixelsPerUnit = 100f;
    public float scale = 3.5f;
    [Min(1f)] public float textureCoordinateScale = 1f;
    public SpriteRenderer parentSR;
    public bool useUnlitMaterial = true;
    public bool suppressParentSpriteRenderer = true;
    public bool ignoreTinyPlaceholderParts = true;

    private Material unlitMaterial;

    private Material GetUnlitMaterial()
    {
        if (unlitMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            if (shader != null)
            {
                unlitMaterial = new Material(shader);
            }
        }
        return unlitMaterial;
    }

    [System.Serializable]
    public struct AnimatorMap
    {
        public string animatorStateName;
        public string jsonActionName;
    }

    [Header("Animation Mapping")]
    public AnimatorMap[] customMappings = new AnimatorMap[]
    {
        new AnimatorMap { animatorStateName = "Idle", jsonActionName = "Stand" },
        new AnimatorMap { animatorStateName = "Stand", jsonActionName = "Stand" },
        new AnimatorMap { animatorStateName = "Boss_idle", jsonActionName = "Stand" },
        new AnimatorMap { animatorStateName = "mob_idle", jsonActionName = "Stand" },

        new AnimatorMap { animatorStateName = "Walk", jsonActionName = "Move" },
        new AnimatorMap { animatorStateName = "Move", jsonActionName = "Move" },
        new AnimatorMap { animatorStateName = "Run", jsonActionName = "Move" },
        new AnimatorMap { animatorStateName = "Boss_move_dash", jsonActionName = "Move" },
        new AnimatorMap { animatorStateName = "MoveDash", jsonActionName = "Move" },

        new AnimatorMap { animatorStateName = "Attack", jsonActionName = "Attack1" },
        new AnimatorMap { animatorStateName = "Attack1", jsonActionName = "Attack1" },
        new AnimatorMap { animatorStateName = "SlashAttack", jsonActionName = "Attack1" },
        new AnimatorMap { animatorStateName = "Boss_slash_attack", jsonActionName = "Attack1" },

        new AnimatorMap { animatorStateName = "rangeAttack", jsonActionName = "Attack2" },
        new AnimatorMap { animatorStateName = "EnergyBlast", jsonActionName = "Attack2" },
        new AnimatorMap { animatorStateName = "Boss_energy_blast", jsonActionName = "Attack2" },

        new AnimatorMap { animatorStateName = "ComboSlashBlast", jsonActionName = "Attack3" },
        new AnimatorMap { animatorStateName = "Boss_combo_slash_blast", jsonActionName = "Attack3" },

        new AnimatorMap { animatorStateName = "Hurt", jsonActionName = "Hurt" },
        new AnimatorMap { animatorStateName = "Hit", jsonActionName = "Hurt" },
        new AnimatorMap { animatorStateName = "Boss_hit_or_special", jsonActionName = "Hurt" },

        new AnimatorMap { animatorStateName = "Die", jsonActionName = "Die" }
    };

    private MobData mobData;
    private Sprite[] sprites;
    private Animator animator;
    private List<SpriteRenderer> partRenderers = new List<SpriteRenderer>();

    private Dictionary<int, string> hashToActionName = new Dictionary<int, string>();
    private ActionData currentAction;
    private int actionFrameIndex;
    private float frameTimer;
    private int lastStateHash = -1;

    private int baseSortingLayerId;
    private int baseSortingOrder;
    private bool partRendererCacheInitialized;

    private void Awake()
    {
        PrepareBridge();

        if (Application.isPlaying)
        {
            InitializeMob();
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying && HasRenderableAssets())
        {
            PrepareBridge();
            InitializeMob();
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying && isActiveAndEnabled && HasRenderableAssets())
        {
            PrepareBridge();
            InitializeMob();
        }
    }

    private bool HasRenderableAssets()
    {
        return texture != null && jsonFile != null;
    }

    private void PrepareBridge()
    {
        animator = GetComponent<Animator>();
        if (parentSR == null)
        {
            parentSR = GetComponent<SpriteRenderer>();
        }

        // Initialize state mapping
        foreach (var map in customMappings)
        {
            int hash = Animator.StringToHash(map.animatorStateName);
            if (!hashToActionName.ContainsKey(hash))
            {
                hashToActionName.Add(hash, map.jsonActionName);
            }
        }

        // Map lowercase "walk" to "Move" to match monster_1 animator states
        int walkHash = Animator.StringToHash("walk");
        if (!hashToActionName.ContainsKey(walkHash))
        {
            hashToActionName.Add(walkHash, "Move");
        }

        // Cache sorting info
        if (parentSR != null)
        {
            baseSortingLayerId = parentSR.sortingLayerID;
            baseSortingOrder = parentSR.sortingOrder;
            ClearParentSprite();
        }
        else
        {
            baseSortingLayerId = 0;
            baseSortingOrder = 0;
        }
    }

    private void Start()
    {
        if (Application.isPlaying && mobData == null)
        {
            InitializeMob();
        }
    }

    private void InitializeMob()
    {
        if (jsonFile == null || texture == null)
        {
            Debug.LogError("Mob77JsonAnimationBridge: Assets are not assigned!", this);
            return;
        }

        EnsureMobDataLoaded();
        if (mobData == null || mobData.imageInfos == null)
        {
            Debug.LogError("Mob77JsonAnimationBridge: Failed to parse JSON!", this);
            return;
        }

        CreateSprites();
        CacheExistingPartRenderers();

        // Default to Stand action
        PlayAction("Stand");
    }

    private bool EnsureMobDataLoaded()
    {
        if (mobData != null && mobData.imageInfos != null)
        {
            return true;
        }

        if (jsonFile == null)
        {
            return false;
        }

        mobData = JsonUtility.FromJson<MobData>(jsonFile.text);
        if (mobData == null || mobData.imageInfos == null)
        {
            mobData = TryParseRawMobData(jsonFile.text);
            if (mobData == null || mobData.imageInfos == null)
            {
                return false;
            }
        }

        return true;
    }

    private void CreateSprites()
    {
        sprites = new Sprite[mobData.imageInfos.Length];

        for (int i = 0; i < mobData.imageInfos.Length; i++)
        {
            var info = mobData.imageInfos[i];
            if (IsIgnoredPlaceholderPart(info))
            {
                sprites[i] = null;
                continue;
            }

            sprites[i] = CreateSprite(info);
        }
    }

    public Sprite CreateLargestAttackEffectSprite()
    {
        if (texture == null || !EnsureMobDataLoaded() || mobData.frames == null || mobData.imageInfos == null)
        {
            return null;
        }

        int bestSpriteIndex = -1;
        int bestArea = 0;
        int startFrame = Mathf.Clamp(6, 0, mobData.frames.Length);
        int endFrame = Mathf.Max(startFrame, mobData.frames.Length - 1);

        for (int frameIndex = startFrame; frameIndex < endFrame; frameIndex++)
        {
            FramePart frame = mobData.frames[frameIndex];
            if (frame == null || frame.idImg == null)
            {
                continue;
            }

            foreach (int spriteIndex in frame.idImg)
            {
                if (spriteIndex < 0 || spriteIndex >= mobData.imageInfos.Length)
                {
                    continue;
                }

                SpritePart info = mobData.imageInfos[spriteIndex];
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

        return bestSpriteIndex >= 0 ? CreateSprite(mobData.imageInfos[bestSpriteIndex]) : null;
    }

    private Sprite CreateSprite(SpritePart info)
    {
        float coordScale = Mathf.Max(1f, textureCoordinateScale);
        float x = info.x0 * coordScale;
        float y = texture.height - (info.y0 + info.h) * coordScale;
        Rect rect = new Rect(x, y, info.w * coordScale, info.h * coordScale);
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        return Sprite.Create(texture, rect, pivot, pixelsPerUnit);
    }

    private void LateUpdate()
    {
        if (mobData == null)
        {
            ClearParentSprite();
            return;
        }

        SyncWithAnimator();
        UpdateAnimation(Time.deltaTime);
        ClearParentSprite();
    }

    private void ClearParentSprite()
    {
        if (suppressParentSpriteRenderer && parentSR != null)
        {
            parentSR.sprite = null;
        }
    }

    private bool IsIgnoredPlaceholderPart(SpritePart info)
    {
        return ignoreTinyPlaceholderParts
            && info != null
            && info.w <= 4
            && info.h <= 4;
    }
    private void SyncWithAnimator()
    {
        if (animator == null) return;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        int stateHash = stateInfo.shortNameHash;

        if (stateHash == lastStateHash) return;
        lastStateHash = stateHash;

        if (hashToActionName.TryGetValue(stateHash, out string actionName))
        {
            PlayAction(actionName);
        }
        else
        {
            // Default to Stand if unknown state
            PlayAction("Stand");
        }
    }

    public void PlayAction(string actionName)
    {
        if (currentAction != null && currentAction.name == actionName) return;
        if (mobData.actions == null || mobData.actions.Length == 0) return;

        ActionData foundAction = null;
        foreach (var act in mobData.actions)
        {
            if (act.name == actionName)
            {
                foundAction = act;
                break;
            }
        }

        if (foundAction == null)
        {
            // Try to find by index or fallback
            if (mobData.actions.Length > 0)
            {
                foundAction = mobData.actions[0];
            }
        }

        if (foundAction != null)
        {
            currentAction = foundAction;
            actionFrameIndex = 0;
            frameTimer = 0f;
            if (currentAction.frameIndices.Length > 0)
            {
                RenderFrame(currentAction.frameIndices[0]);
            }
        }
    }

    private void UpdateAnimation(float deltaTime)
    {
        if (currentAction == null || currentAction.frameIndices.Length == 0) return;

        frameTimer += deltaTime;
        float timePerFrame = 1f / frameRate;
        if (frameTimer >= timePerFrame)
        {
            frameTimer -= timePerFrame;
            actionFrameIndex = (actionFrameIndex + 1) % currentAction.frameIndices.Length;
            RenderFrame(currentAction.frameIndices[actionFrameIndex]);
        }
    }

    private void RenderFrame(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= mobData.frames.Length)
        {
            foreach (var r in partRenderers) r.gameObject.SetActive(false);
            return;
        }

        // Update base sorting layer in case it was changed dynamically
        if (parentSR != null)
        {
            baseSortingLayerId = parentSR.sortingLayerID;
            baseSortingOrder = parentSR.sortingOrder;
        }

        var frame = mobData.frames[frameIndex];
        int partCount = frame.idImg.Length;

        // Ensure we have enough child renderers
        CacheExistingPartRenderers();
        while (partRenderers.Count < partCount)
        {
            partRenderers.Add(null);
        }

        bool isFlipped = (parentSR != null && parentSR.flipX);

        for (int i = 0; i < partRenderers.Count; i++)
        {
            var r = GetOrCreatePartRenderer(i);
            if (i < partCount)
            {
                int imgId = frame.idImg[i];
                if (imgId >= 0 && imgId < sprites.Length && sprites[imgId] != null)
                {
                    r.gameObject.layer = this.gameObject.layer;
                    r.sprite = sprites[imgId];
                    r.sortingLayerID = baseSortingLayerId;
                    r.sortingOrder = baseSortingOrder + i;
                    r.flipX = isFlipped;

                    if (useUnlitMaterial)
                    {
                        r.sharedMaterial = GetUnlitMaterial();
                    }
                    else if (parentSR != null)
                    {
                        r.sharedMaterial = parentSR.sharedMaterial;
                    }

                    if (parentSR != null)
                    {
                        r.color = parentSR.color;
                    }

                    r.transform.localScale = new Vector3(scale, scale, 1f);

                    // Calculate center position of the part in pixels relative to character origin
                    var info = mobData.imageInfos[imgId];
                    float coordScale = Mathf.Max(1f, textureCoordinateScale);
                    float dx = frame.dx[i] * coordScale;
                    float dy = frame.dy[i] * coordScale;

                    float centerX = dx + info.w * coordScale / 2f;
                    float centerY = dy + info.h * coordScale / 2f;

                    if (isFlipped)
                    {
                        r.transform.localPosition = new Vector3(-centerX * scale / pixelsPerUnit, -centerY * scale / pixelsPerUnit, 0f);
                    }
                    else
                    {
                        r.transform.localPosition = new Vector3(centerX * scale / pixelsPerUnit, -centerY * scale / pixelsPerUnit, 0f);
                    }

                    r.gameObject.SetActive(true);
                }
                else
                {
                    r.gameObject.SetActive(false);
                }
            }
            else
            {
                r.gameObject.SetActive(false);
            }
        }
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

        Transform existing = transform.Find(PartNamePrefix + index);
        GameObject child = existing != null ? existing.gameObject : new GameObject(PartNamePrefix + index);
        child.transform.SetParent(transform, false);
        child.layer = gameObject.layer;

        renderer = child.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = child.AddComponent<SpriteRenderer>();
        }

        partRenderers[index] = renderer;
        return renderer;
    }

    private void CacheExistingPartRenderers()
    {
        if (partRendererCacheInitialized)
        {
            return;
        }

        partRendererCacheInitialized = true;
        partRenderers.Clear();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
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
            Destroy(duplicate);
            return;
        }

        DestroyImmediate(duplicate);
    }

    private MobData TryParseRawMobData(string json)
    {
        RawMobData raw = RawMobDataParser.TryParse(json);
        if (raw == null || raw.sprites == null || raw.frames == null)
        {
            return null;
        }

        SpritePart[] imageInfos = new SpritePart[raw.sprites.Length];
        for (int i = 0; i < raw.sprites.Length; i++)
        {
            RawSpritePart sprite = raw.sprites[i];
            imageInfos[i] = new SpritePart
            {
                ID = sprite.id,
                x0 = sprite.x,
                y0 = sprite.y,
                w = sprite.w,
                h = sprite.h
            };
        }

        return new MobData
        {
            monsterId = raw.id,
            type = raw.type,
            typeData = raw.type_data,
            imageInfos = imageInfos,
            frames = raw.frames,
            actions = BuildDefaultActions(raw.frames.Length, raw.animations)
        };
    }

    private static ActionData[] BuildDefaultActions(int frameCount, int[] animationFrames)
    {
        if (frameCount >= 16)
        {
            return new[]
            {
                new ActionData { index = 0, name = "Stand", frameIndices = BuildRange(0, 2) },
                new ActionData { index = 1, name = "Move", frameIndices = BuildRange(2, 6) },
                new ActionData { index = 2, name = "Attack1", frameIndices = BuildRange(6, 10) },
                new ActionData { index = 3, name = "Attack2", frameIndices = BuildRange(frameCount - 3, frameCount - 1) },
                new ActionData { index = 4, name = "Attack3", frameIndices = BuildRange(10, frameCount - 1) },
                new ActionData { index = 5, name = "Hurt", frameIndices = BuildRange(frameCount - 1, frameCount) },
                new ActionData { index = 6, name = "Die", frameIndices = BuildRange(frameCount - 1, frameCount) }
            };
        }

        int[] moveFrames = BuildRange(0, Mathf.Min(frameCount, 6));

        return new[]
        {
            new ActionData { index = 0, name = "Stand", frameIndices = BuildRange(0, Mathf.Min(frameCount, 2)) },
            new ActionData { index = 1, name = "Move", frameIndices = moveFrames },
            new ActionData { index = 2, name = "Attack1", frameIndices = BuildRange(6, Mathf.Min(frameCount, 10)) },
            new ActionData { index = 3, name = "Attack2", frameIndices = BuildRange(10, Mathf.Min(frameCount, 14)) },
            new ActionData { index = 4, name = "Attack3", frameIndices = BuildRange(12, Mathf.Min(frameCount, 16)) },
            new ActionData { index = 5, name = "Hurt", frameIndices = BuildRange(Mathf.Max(0, frameCount - 1), frameCount) },
            new ActionData { index = 6, name = "Die", frameIndices = BuildRange(Mathf.Max(0, frameCount - 1), frameCount) }
        };
    }

    private static int[] BuildRange(int startInclusive, int endExclusive)
    {
        if (endExclusive <= startInclusive)
        {
            return new[] { 0 };
        }

        int[] frames = new int[endExclusive - startInclusive];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = startInclusive + i;
        }

        return frames;
    }

    private static class RawMobDataParser
    {
        public static RawMobData TryParse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || !json.Contains("\"sprites\"") || !json.Contains("\"frames\""))
            {
                return null;
            }

            string spritesSection = ExtractArraySection(json, "\"sprites\"");
            string framesSection = ExtractArraySection(json, "\"frames\"");
            if (spritesSection == null || framesSection == null)
            {
                return null;
            }

            return new RawMobData
            {
                id = ExtractInt(json, "\"id\"", 0),
                type = ExtractInt(json, "\"type\"", 0),
                type_data = ExtractInt(json, "\"type_data\"", 0),
                sprites = ParseSprites(spritesSection),
                frames = ParseFrames(framesSection),
                animations = ParseIntArray(ExtractArraySection(json, "\"animations\""))
            };
        }

        private static RawSpritePart[] ParseSprites(string section)
        {
            List<RawSpritePart> sprites = new List<RawSpritePart>();
            foreach (string objectText in ExtractObjects(section))
            {
                sprites.Add(new RawSpritePart
                {
                    id = ExtractInt(objectText, "\"id\"", 0),
                    x = ExtractInt(objectText, "\"x\"", 0),
                    y = ExtractInt(objectText, "\"y\"", 0),
                    w = ExtractInt(objectText, "\"w\"", 0),
                    h = ExtractInt(objectText, "\"h\"", 0)
                });
            }

            return sprites.ToArray();
        }

        private static FramePart[] ParseFrames(string section)
        {
            List<FramePart> frames = new List<FramePart>();
            foreach (string frameText in ExtractArrayItems(section))
            {
                List<int> dx = new List<int>();
                List<int> dy = new List<int>();
                List<int> idImg = new List<int>();
                foreach (string partText in ExtractObjects(frameText))
                {
                    dx.Add(ExtractInt(partText, "\"dx\"", 0));
                    dy.Add(ExtractInt(partText, "\"dy\"", 0));
                    idImg.Add(ExtractInt(partText, "\"sprite_id\"", 0));
                }

                frames.Add(new FramePart
                {
                    dx = dx.ToArray(),
                    dy = dy.ToArray(),
                    idImg = idImg.ToArray()
                });
            }

            return frames.ToArray();
        }

        private static int[] ParseIntArray(string section)
        {
            if (section == null)
            {
                return new int[0];
            }

            List<int> values = new List<int>();
            string[] pieces = section.Split(',');
            foreach (string piece in pieces)
            {
                if (int.TryParse(piece.Trim(), out int value))
                {
                    values.Add(value);
                }
            }

            return values.ToArray();
        }

        private static IEnumerable<string> ExtractObjects(string section)
        {
            int depth = 0;
            int start = -1;
            for (int i = 0; i < section.Length; i++)
            {
                char c = section[i];
                if (c == '{')
                {
                    if (depth == 0)
                    {
                        start = i;
                    }

                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        yield return section.Substring(start, i - start + 1);
                        start = -1;
                    }
                }
            }
        }

        private static IEnumerable<string> ExtractArrayItems(string section)
        {
            int depth = 0;
            int start = -1;
            for (int i = 0; i < section.Length; i++)
            {
                char c = section[i];
                if (c == '[')
                {
                    if (depth == 0)
                    {
                        start = i + 1;
                    }

                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        yield return section.Substring(start, i - start);
                        start = -1;
                    }
                }
            }
        }

        private static string ExtractArraySection(string json, string key)
        {
            int keyIndex = json.IndexOf(key, System.StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                return null;
            }

            int start = json.IndexOf('[', keyIndex);
            if (start < 0)
            {
                return null;
            }

            int depth = 0;
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '[')
                {
                    depth++;
                }
                else if (json[i] == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return json.Substring(start + 1, i - start - 1);
                    }
                }
            }

            return null;
        }

        private static int ExtractInt(string text, string key, int fallback)
        {
            int keyIndex = text.IndexOf(key, System.StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                return fallback;
            }

            int colon = text.IndexOf(':', keyIndex);
            if (colon < 0)
            {
                return fallback;
            }

            int valueStart = colon + 1;
            while (valueStart < text.Length && char.IsWhiteSpace(text[valueStart]))
            {
                valueStart++;
            }

            int valueEnd = valueStart;
            if (valueEnd < text.Length && text[valueEnd] == '-')
            {
                valueEnd++;
            }

            while (valueEnd < text.Length && char.IsDigit(text[valueEnd]))
            {
                valueEnd++;
            }

            return int.TryParse(text.Substring(valueStart, valueEnd - valueStart), out int value)
                ? value
                : fallback;
        }
    }
}
