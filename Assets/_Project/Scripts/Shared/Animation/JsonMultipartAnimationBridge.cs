using UnityEngine;

namespace TinyDragon.Shared.Animation
{
    [ExecuteAlways]
    public class JsonMultipartAnimationBridge : MonoBehaviour, IProjectileEffectSource
    {
        [Header("Assets")]
        public Texture2D texture;
        public TextAsset jsonFile;

        [Header("Settings")]
        public float frameRate = 12f;
        public float pixelsPerUnit = 100f;
        public float scale = 3.5f;
        [Min(1f)] public float textureCoordinateScale = 1f;

        [Header("Combat Actions")]
        public string meleeAttackActionName = "Attack1";
        public string rangedAttackActionName = "Attack2";
        public string comboAttackActionName = "Attack3";
        [Min(1)] public int effectSpriteMinimumSize = 30;
        [Min(0f)] public float effectPartMinimumForwardOffset = 35f;

        public SpriteRenderer parentSR;
        public bool useUnlitMaterial = true;
        public bool suppressParentSpriteRenderer = true;
        public bool ignoreTinyPlaceholderParts = true;

        [Header("Animation Mapping")]
        public JsonAnimatorActionMap[] customMappings = new JsonAnimatorActionMap[]
        {
            new JsonAnimatorActionMap { animatorStateName = "Idle", jsonActionName = "Stand" },
            new JsonAnimatorActionMap { animatorStateName = "Stand", jsonActionName = "Stand" },
            new JsonAnimatorActionMap { animatorStateName = "Boss_idle", jsonActionName = "Stand" },
            new JsonAnimatorActionMap { animatorStateName = "mob_idle", jsonActionName = "Stand" },

            new JsonAnimatorActionMap { animatorStateName = "Walk", jsonActionName = "Move" },
            new JsonAnimatorActionMap { animatorStateName = "Move", jsonActionName = "Move" },
            new JsonAnimatorActionMap { animatorStateName = "Run", jsonActionName = "Move" },
            new JsonAnimatorActionMap { animatorStateName = "Boss_move_dash", jsonActionName = "Move" },
            new JsonAnimatorActionMap { animatorStateName = "MoveDash", jsonActionName = "Move" },

            new JsonAnimatorActionMap { animatorStateName = "Attack", jsonActionName = "Attack1" },
            new JsonAnimatorActionMap { animatorStateName = "Attack1", jsonActionName = "Attack1" },
            new JsonAnimatorActionMap { animatorStateName = "SlashAttack", jsonActionName = "Attack1" },
            new JsonAnimatorActionMap { animatorStateName = "Boss_slash_attack", jsonActionName = "Attack1" },

            new JsonAnimatorActionMap { animatorStateName = "rangeAttack", jsonActionName = "Attack2" },
            new JsonAnimatorActionMap { animatorStateName = "EnergyBlast", jsonActionName = "Attack2" },
            new JsonAnimatorActionMap { animatorStateName = "Boss_energy_blast", jsonActionName = "Attack2" },

            new JsonAnimatorActionMap { animatorStateName = "ComboSlashBlast", jsonActionName = "Attack3" },
            new JsonAnimatorActionMap { animatorStateName = "Boss_combo_slash_blast", jsonActionName = "Attack3" },

            new JsonAnimatorActionMap { animatorStateName = "Hurt", jsonActionName = "Hurt" },
            new JsonAnimatorActionMap { animatorStateName = "Hit", jsonActionName = "Hurt" },
            new JsonAnimatorActionMap { animatorStateName = "Boss_hit_or_special", jsonActionName = "Hurt" },

            new JsonAnimatorActionMap { animatorStateName = "Die", jsonActionName = "Die" }
        };

        private JsonMultipartAnimationData animationData;
        private Sprite[] sprites;
        private Animator animator;
        private JsonAnimationActionMapper actionMapper;
        private JsonMultipartFrameRenderer frameRenderer;
        private JsonAnimationActionData currentAction;
        private int actionFrameIndex;
        private float frameTimer;
        private int lastStateHash = -1;
        private Material unlitMaterial;

        private void Awake()
        {
            PrepareBridge();

            if (Application.isPlaying)
            {
                InitializeAnimation();
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying && HasRenderableAssets())
            {
                PrepareBridge();
                InitializeAnimation();
            }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying && isActiveAndEnabled && HasRenderableAssets())
            {
                PrepareBridge();
                InitializeAnimation();
            }
        }

        private void Start()
        {
            if (Application.isPlaying && animationData == null)
            {
                InitializeAnimation();
            }
        }

        public Sprite CreateLargestAttackEffectSprite()
        {
            if (!EnsureAnimationDataLoaded())
            {
                return null;
            }

            return JsonAnimationEffectSpriteProvider.CreateLargestAttackEffectSprite(animationData, texture, pixelsPerUnit, textureCoordinateScale);
        }

        public Sprite[] CreateProjectileEffectSprites()
        {
            return CreateActionEffectSprites(rangedAttackActionName);
        }

        public Sprite[] CreateRangedAttackEffectSprites()
        {
            return CreateProjectileEffectSprites();
        }

        public Sprite[] CreateActionEffectSprites(string actionName)
        {
            if (!EnsureAnimationDataLoaded())
            {
                return new Sprite[0];
            }

            EnsureSpritesCreated();
            return JsonAnimationEffectSpriteProvider.CreateActionEffectSprites(
                animationData,
                texture,
                sprites,
                actionName,
                effectSpriteMinimumSize,
                effectPartMinimumForwardOffset,
                pixelsPerUnit,
                textureCoordinateScale
            );
        }

        public bool TryGetProjectileEffect(out Sprite[] effectSprites, out float effectFrameRate)
        {
            effectSprites = CreateProjectileEffectSprites();
            effectFrameRate = Mathf.Max(1f, frameRate);
            return HasSprites(effectSprites);
        }

        public void PlayAction(string actionName)
        {
            if (currentAction != null && currentAction.name == actionName)
            {
                return;
            }

            if (animationData == null || animationData.actions == null || animationData.actions.Length == 0)
            {
                return;
            }

            JsonAnimationActionData foundAction = FindAction(actionName) ?? FindAttackFallback(actionName) ?? animationData.actions[0];
            if (foundAction == null)
            {
                return;
            }

            currentAction = foundAction;
            actionFrameIndex = 0;
            frameTimer = 0f;
            if (currentAction.frameIndices.Length > 0)
            {
                RenderFrame(currentAction.frameIndices[0]);
            }
        }

        public void SetRuntimeVisualOffsetY(float offsetY)
        {
            EnsureFrameRenderer();
            frameRenderer.SetRuntimeVisualOffsetY(offsetY);
        }

        private void LateUpdate()
        {
            if (animationData == null)
            {
                ClearParentSprite();
                return;
            }

            SyncWithAnimator();
            UpdateAnimation(Time.deltaTime);
            ClearParentSprite();
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

            if (actionMapper == null)
            {
                actionMapper = new JsonAnimationActionMapper();
            }

            actionMapper.Configure(customMappings, meleeAttackActionName, rangedAttackActionName, comboAttackActionName);
            EnsureFrameRenderer();
            ClearParentSprite();
        }

        private void InitializeAnimation()
        {
            if (jsonFile == null || texture == null)
            {
                Debug.LogError("JsonMultipartAnimationBridge: Assets are not assigned.", this);
                return;
            }

            EnsureAnimationDataLoaded();
            if (animationData == null || animationData.imageInfos == null)
            {
                Debug.LogError("JsonMultipartAnimationBridge: Failed to parse JSON animation data.", this);
                return;
            }

            EnsureSpritesCreated();
            EnsureFrameRenderer();
            frameRenderer.CacheExistingPartRenderers();
            PlayAction("Stand");
        }

        private bool EnsureAnimationDataLoaded()
        {
            if (animationData != null && animationData.imageInfos != null)
            {
                return true;
            }

            if (jsonFile == null)
            {
                return false;
            }

            animationData = JsonMultipartAnimationParser.Parse(jsonFile.text);
            return animationData != null && animationData.imageInfos != null;
        }

        private void EnsureSpritesCreated()
        {
            if (animationData == null || animationData.imageInfos == null)
            {
                return;
            }

            if (sprites != null && sprites.Length == animationData.imageInfos.Length)
            {
                return;
            }

            sprites = JsonMultipartSpriteFactory.CreateSprites(
                texture,
                animationData.imageInfos,
                pixelsPerUnit,
                textureCoordinateScale,
                ignoreTinyPlaceholderParts
            );
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
            PlayAction(actionMapper.TryGetActionName(stateHash, out string actionName) ? actionName : "Stand");
        }

        private void UpdateAnimation(float deltaTime)
        {
            if (currentAction == null || currentAction.frameIndices.Length == 0)
            {
                return;
            }

            frameTimer += deltaTime;
            float timePerFrame = 1f / Mathf.Max(1f, frameRate);
            if (frameTimer < timePerFrame)
            {
                return;
            }

            frameTimer -= timePerFrame;
            actionFrameIndex = (actionFrameIndex + 1) % currentAction.frameIndices.Length;
            RenderFrame(currentAction.frameIndices[actionFrameIndex]);
        }

        private void RenderFrame(int frameIndex)
        {
            EnsureSpritesCreated();
            EnsureFrameRenderer();
            frameRenderer.RenderFrame(
                frameIndex,
                animationData,
                sprites,
                parentSR,
                GetUnlitMaterial(),
                useUnlitMaterial,
                scale,
                pixelsPerUnit,
                textureCoordinateScale
            );
        }

        private void ClearParentSprite()
        {
            EnsureFrameRenderer();
            frameRenderer.ClearParentSprite(parentSR, suppressParentSpriteRenderer);
        }

        private JsonAnimationActionData FindAction(string actionName)
        {
            return JsonAnimationEffectSpriteProvider.FindAction(animationData, actionName);
        }

        private JsonAnimationActionData FindAttackFallback(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName) || !actionName.StartsWith("Attack"))
            {
                return null;
            }

            return FindAction("Attack1") ?? FindAction("Attack2") ?? FindAction("Attack3");
        }

        private void EnsureFrameRenderer()
        {
            if (frameRenderer == null)
            {
                frameRenderer = new JsonMultipartFrameRenderer(this);
            }
        }

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

        private static bool HasSprites(Sprite[] source)
        {
            if (source == null || source.Length == 0)
            {
                return false;
            }

            foreach (Sprite sprite in source)
            {
                if (sprite != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
