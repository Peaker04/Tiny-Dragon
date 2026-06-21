using System.Collections.Generic;
using UnityEngine;

public class Mob77JsonAnimationBridge : MonoBehaviour
{
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

    [Header("Assets")]
    public Texture2D texture;
    public TextAsset jsonFile;

    [Header("Settings")]
    public float frameRate = 12f;
    public float pixelsPerUnit = 100f;
    public float scale = 3.5f;
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

    private void Awake()
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

        if (Application.isPlaying)
        {
            InitializeMob();
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

        mobData = JsonUtility.FromJson<MobData>(jsonFile.text);
        if (mobData == null || mobData.imageInfos == null)
        {
            Debug.LogError("Mob77JsonAnimationBridge: Failed to parse JSON!", this);
            return;
        }

        CreateSprites();

        // Default to Stand action
        PlayAction("Stand");
    }

    private void CreateSprites()
    {
        sprites = new Sprite[mobData.imageInfos.Length];
        float texWidth = texture.width;
        float texHeight = texture.height;

        for (int i = 0; i < mobData.imageInfos.Length; i++)
        {
            var info = mobData.imageInfos[i];
            // Unity coordinates start from bottom-left, tool starts from top-left
            float x = info.x0;
            float y = texHeight - info.y0 - info.h;

            if (IsIgnoredPlaceholderPart(info))
            {
                sprites[i] = null;
                continue;
            }

            Rect rect = new Rect(x, y, info.w, info.h);
            Vector2 pivot = new Vector2(0.5f, 0.5f);
            sprites[i] = Sprite.Create(texture, rect, pivot, pixelsPerUnit);
        }
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
        while (partRenderers.Count < partCount)
        {
            GameObject child = new GameObject("Part_" + partRenderers.Count);
            child.transform.SetParent(this.transform, false);
            child.layer = this.gameObject.layer;
            var r = child.AddComponent<SpriteRenderer>();
            partRenderers.Add(r);
        }

        bool isFlipped = (parentSR != null && parentSR.flipX);

        for (int i = 0; i < partRenderers.Count; i++)
        {
            var r = partRenderers[i];
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
                    float dx = frame.dx[i];
                    float dy = frame.dy[i];

                    float centerX = dx + info.w / 2f;
                    float centerY = dy + info.h / 2f;

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
}
