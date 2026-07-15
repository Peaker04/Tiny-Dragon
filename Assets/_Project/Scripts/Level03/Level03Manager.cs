using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TinyDragon.Config;
using TinyDragon.Shared.Animation;
using TinyDragon.Shared.Unity;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public enum Level03State
{
    BossShielded,
    CollectingFragments,
    DragonGemMerging,
    BossVulnerable,
    BossDefeated
}

[DisallowMultipleComponent]
[ExecuteAlways]
public class Level03Manager : MonoBehaviour
{
    [Header("Level 02 Platform Visual")]
    [SerializeField] private Sprite platformBlockSprite;
    [SerializeField] private Color platformColor = Color.white;

    [Header("Config")]
    [SerializeField] private TinyDragonRuntimeConfig runtimeConfig;

    [Header("Dragon Gem Assets")]
    [SerializeField] private Texture2D fragmentTexture;
    [SerializeField] private Texture2D completeGemTexture;
    [SerializeField] private Texture2D mergeEffectTexture;
    [SerializeField] private TextAsset mergeEffectData;

    [Header("Encounter Tuning")]
    [SerializeField] private int requiredFragments = 2;
    [SerializeField] private float platformFadeDelay = 0.15f;
    [SerializeField] private float platformTransitionDuration = 0.25f;
    [SerializeField] private float fragmentPixelsPerUnit = 512f;
    [SerializeField] private Vector3 mergePoint = new Vector3(0f, 15.0f, 0f);

    private readonly List<Level03DragonFragment> fragments = new List<Level03DragonFragment>();
    private Level03PlatformGroupRuntime platformGroupA;
    private Level03PlatformGroupRuntime platformGroupB;
    private PlayerMovement playerMovement;
    private PlayerInputReader playerInput;
    private EnemyHealth bossHealth;
    private EnemyPatrol bossPatrol;
    private Level03BossShield bossShield;
    private DragonGemEffectPlayer mergeEffect;
    private SpriteRenderer completeGemRenderer;
    private Coroutine platformTransition;
    private CameraFollow sceneCamera;
    private bool isGroupAActive = true;
    private int collectedFragments;
    private string announcement;
    private float announcementUntil;
    private GUIStyle counterStyle;
    private GUIStyle announcementStyle;
    private Material spriteUnlitMaterial;
#if UNITY_EDITOR
    private bool editorBuildQueued;
#endif
    private TinyDragonRuntimeConfig Config => TinyDragonRuntimeConfigProvider.Resolve(runtimeConfig);
    private Level03EncounterConfig EncounterConfig => Config.Level03;

    public Level03State CurrentState { get; private set; } = Level03State.BossShielded;
    public bool CanCollectFragments => CurrentState == Level03State.BossShielded
        || CurrentState == Level03State.CollectingFragments;

    private void Awake()
    {
        ApplyConfigDefaults();
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
        QueueEditorBuild();
    }

    private void OnValidate()
    {
        QueueEditorBuild();
    }

    private void QueueEditorBuild()
    {
        if (Application.isPlaying || editorBuildQueued)
        {
            return;
        }

        editorBuildQueued = true;
        EditorApplication.delayCall += BuildEditableHierarchy;
    }

    [ContextMenu("Build Editable Level 03 Objects")]
    private void BuildEditableHierarchy()
    {
        editorBuildQueued = false;
        if (this == null || Application.isPlaying || !gameObject.scene.IsValid())
        {
            return;
        }

        ApplyConfigDefaults();
        BuildEncounterHierarchy(false);
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    private IEnumerator Start()
    {
        if (!Application.isPlaying)
        {
            yield break;
        }

        yield return null;

        if (!ResolveSceneActors())
        {
            enabled = false;
            yield break;
        }

        BuildEncounterHierarchy(true);
        bossShield.ActivateShield(this);
        bossHealth.Died += HandleBossDied;
        playerMovement.JumpPerformed += HandlePlayerJumpPerformed;

        CurrentState = Level03State.BossShielded;
        ShowAnnouncement($"Boss đang được bảo vệ - hãy tìm {requiredFragments} mảnh Ngọc Rồng!", 2f);
        yield return new WaitForSeconds(EncounterConfig.shieldIntroDelay);

        if (CurrentState == Level03State.BossShielded)
        {
            CurrentState = Level03State.CollectingFragments;
        }
    }

    private void OnDestroy()
    {
        if (playerMovement != null)
        {
            playerMovement.JumpPerformed -= HandlePlayerJumpPerformed;
        }

        if (bossHealth != null)
        {
            bossHealth.Died -= HandleBossDied;
        }

        if (spriteUnlitMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(spriteUnlitMaterial);
            }
            else
            {
                DestroyImmediate(spriteUnlitMaterial);
            }
        }
    }

    public void NotifyShieldHit()
    {
        if (CanCollectFragments)
        {
            ShowAnnouncement($"Shielded! Thu thập đủ {requiredFragments} mảnh Ngọc Rồng.", EncounterConfig.shieldHitAnnouncementDuration);
        }
    }

    public void CollectFragment(Level03DragonFragment fragment)
    {
        if (!CanCollectFragments || fragment == null)
        {
            return;
        }

        collectedFragments = Mathf.Min(collectedFragments + 1, requiredFragments);
        ShowAnnouncement($"Dragon Fragment: {collectedFragments}/{requiredFragments}", EncounterConfig.fragmentAnnouncementDuration);

        if (collectedFragments >= requiredFragments)
        {
            StartCoroutine(MergeDragonGem());
        }
    }

    private bool ResolveSceneActors()
    {
        sceneCamera = ObjectLookup.Any<CameraFollow>();
        playerMovement = ObjectLookup.Any<PlayerMovement>();
        if (playerMovement != null)
        {
            playerInput = playerMovement.GetComponent<PlayerInputReader>();
        }

        JsonMultipartAnimationBridge animationBridge = ObjectLookup.Any<JsonMultipartAnimationBridge>();
        if (animationBridge != null)
        {
            bossHealth = animationBridge.GetComponent<EnemyHealth>();
            bossPatrol = animationBridge.GetComponent<EnemyPatrol>();
            bossShield = animationBridge.GetComponent<Level03BossShield>();
            if (bossShield == null)
            {
                bossShield = animationBridge.gameObject.AddComponent<Level03BossShield>();
            }
        }

        if (playerMovement == null || bossHealth == null || bossShield == null)
        {
            Debug.LogError("Level03Manager requires PlayerMovement and a JSON multipart boss in Level_03.", this);
            return false;
        }

        return true;
    }

    private void ApplyConfigDefaults()
    {
        Level03EncounterConfig config = EncounterConfig;
        requiredFragments = Mathf.Max(1, config.requiredFragments);
        platformFadeDelay = config.platformFadeDelay;
        platformTransitionDuration = config.platformTransitionDuration;
        fragmentPixelsPerUnit = config.fragmentPixelsPerUnit;
        mergePoint = config.mergePoint;
    }

    private void BuildEncounterHierarchy(bool runtimeState)
    {
        fragments.Clear();
        Transform flyingPlatforms = GetOrCreateRoot("FlyingPlatforms", transform);
        Transform groupARoot = GetOrCreateRoot("PlatformGroup_A", flyingPlatforms);
        Transform groupBRoot = GetOrCreateRoot("PlatformGroup_B", flyingPlatforms);

        platformGroupA = new Level03PlatformGroupRuntime();
        platformGroupB = new Level03PlatformGroupRuntime();

        Vector3[] platformsA = GetPlatformPositions(groupARoot, EncounterConfig.platformGroupA, "Platform_A");
        Vector3[] platformsB = GetPlatformPositions(groupBRoot, EncounterConfig.platformGroupB, "Platform_B");

        CreatePlatform("Platform_A1", platformsA[0], groupARoot, platformGroupA);
        CreatePlatform("Platform_A2", platformsA[1], groupARoot, platformGroupA);
        CreatePlatform("Platform_A3", platformsA[2], groupARoot, platformGroupA);
        CreatePlatform("Platform_A4", platformsA[3], groupARoot, platformGroupA);
        CreatePlatform("Platform_A5", platformsA[4], groupARoot, platformGroupA);

        CreatePlatform("Platform_B1", platformsB[0], groupBRoot, platformGroupB);
        CreatePlatform("Platform_B2", platformsB[1], groupBRoot, platformGroupB);
        CreatePlatform("Platform_B3", platformsB[2], groupBRoot, platformGroupB);
        CreatePlatform("Platform_B4", platformsB[3], groupBRoot, platformGroupB);
        CreatePlatform("Platform_B5", platformsB[4], groupBRoot, platformGroupB);

        if (runtimeState)
        {
            Level03PlatformGroupRuntime.SetState(platformGroupA, 1f, true);
            Level03PlatformGroupRuntime.SetState(platformGroupB, 0f, false);
        }
        else
        {
            Level03PlatformGroupRuntime.SetState(platformGroupA, 1f, true);
            Level03PlatformGroupRuntime.SetState(platformGroupB, 1f, true);
        }

        Transform fragmentRoot = GetOrCreateRoot("DragonFragments", transform);
        CreateFragments(
            fragmentRoot,
            platformsA,
            platformsB);

        Transform existingCompleteRoot = transform.Find("DragonGemComplete");
        Transform completeRoot = GetOrCreateRoot("DragonGemComplete", transform);
        if (existingCompleteRoot != null)
        {
            mergePoint = completeRoot.position;
        }
        else
        {
            completeRoot.position = mergePoint;
        }
        completeGemRenderer = GetOrCreateComponent<SpriteRenderer>(completeRoot.gameObject);
        completeGemRenderer.sprite = Level03SpriteFactory.CreateFullTextureSprite(completeGemTexture, fragmentPixelsPerUnit);
        completeGemRenderer.sharedMaterial = GetSpriteUnlitMaterial();
        completeGemRenderer.sortingOrder = 25;
        completeGemRenderer.transform.localScale = Vector3.one * 0.65f;
        completeGemRenderer.enabled = !runtimeState;

        Transform effectsRoot = GetOrCreateRoot("Effects", transform);
        Transform mergeEffectRoot = GetOrCreateRoot("MergeEffect", effectsRoot);
        mergeEffectRoot.position = mergePoint;
        mergeEffect = GetOrCreateComponent<DragonGemEffectPlayer>(mergeEffectRoot.gameObject);
        mergeEffect.Configure(mergeEffectTexture, mergeEffectData);
    }

    private void CreateFragments(
        Transform parent,
        Vector3[] platformsA,
        Vector3[] platformsB)
    {
        if (fragmentTexture == null)
        {
            Debug.LogError("Level03Manager is missing dragon_fragments.png.", this);
            return;
        }

        int halfWidth = fragmentTexture.width / 2;
        Color32[] pixels = fragmentTexture.GetPixels32();
        Sprite leftFragment = Level03SpriteFactory.CreateTrimmedSprite(
            fragmentTexture,
            pixels,
            0,
            halfWidth,
            fragmentPixelsPerUnit);
        Sprite rightFragment = Level03SpriteFactory.CreateTrimmedSprite(
            fragmentTexture,
            pixels,
            halfWidth,
            fragmentTexture.width,
            fragmentPixelsPerUnit);

        Vector3 fragmentStandOffset = EncounterConfig.fragmentStandOffset;

        Vector3[] pathOne =
        {
            platformsA[1] + fragmentStandOffset, // A2
            platformsB[2] + fragmentStandOffset, // B3
            platformsA[3] + fragmentStandOffset, // A4
            platformsB[4] + fragmentStandOffset  // B5
        };
        
        Vector3[] pathTwo =
        {
            platformsB[1] + fragmentStandOffset, // B2
            platformsA[2] + fragmentStandOffset, // A3
            platformsB[3] + fragmentStandOffset, // B4
            platformsA[4] + fragmentStandOffset  // A5
        };

        fragments.Add(CreateFragment("Fragment_01", 1, leftFragment, pathOne, parent));
        fragments.Add(CreateFragment("Fragment_02", 2, rightFragment, pathTwo, parent));
    }

    private Level03DragonFragment CreateFragment(
        string objectName,
        int index,
        Sprite sprite,
        Vector3[] path,
        Transform parent)
    {
        Transform fragmentTransform = GetOrCreateRoot(objectName, parent);
        GameObject fragmentObject = fragmentTransform.gameObject;
        fragmentObject.transform.localScale = Vector3.one * 0.8f;
        Level03DragonFragment fragment = GetOrCreateComponent<Level03DragonFragment>(fragmentObject);
        fragment.Initialize(this, index, sprite, path, GetSpriteUnlitMaterial());
        return fragment;
    }

    private void CreatePlatform(
        string objectName,
        Vector3 position,
        Transform parent,
        Level03PlatformGroupRuntime group)
    {
        Transform platformTransform = GetOrCreateRoot(objectName, parent);
        GameObject platform = platformTransform.gameObject;
        platform.layer = LayerMask.NameToLayer("Ground");
        platform.transform.position = position;

        BoxCollider2D collider = GetOrCreateComponent<BoxCollider2D>(platform);
        collider.size = EncounterConfig.platformColliderSize;
        collider.offset = EncounterConfig.platformColliderOffset;
        group.colliders.Add(collider);

        Transform visualTransform = GetOrCreateRoot("BlockVisual", platform.transform);
        GameObject visual = visualTransform.gameObject;

        SpriteRenderer renderer = GetOrCreateComponent<SpriteRenderer>(visual);
        renderer.sprite = platformBlockSprite;
        renderer.sharedMaterial = GetSpriteUnlitMaterial();
        renderer.color = platformColor;
        renderer.sortingOrder = 2;

        Vector3 visualScale = EncounterConfig.platformVisualScale;
        visual.transform.localScale = visualScale;
        if (platformBlockSprite != null)
        {
            Bounds bounds = platformBlockSprite.bounds;
            visual.transform.localPosition = new Vector3(
                -bounds.center.x * visualScale.x,
                -bounds.max.y * visualScale.y,
                0f);
        }

        group.renderers.Add(renderer);
    }

    private static Transform GetOrCreateRoot(string objectName, Transform parent)
    {
        Transform child = parent.Find(objectName);
        if (child != null)
        {
            return child;
        }

        GameObject created = new GameObject(objectName);
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    private static T GetOrCreateComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static Vector3[] GetPlatformPositions(Transform groupRoot, Vector3[] fallbackPositions, string platformPrefix)
    {
        Vector3[] positions = new Vector3[fallbackPositions.Length];
        for (int i = 0; i < fallbackPositions.Length; i++)
        {
            string objectName = $"{platformPrefix}{i + 1}";
            Transform existing = groupRoot.Find(objectName);
            positions[i] = existing != null ? existing.position : fallbackPositions[i];
        }

        return positions;
    }

    private void HandlePlayerJumpPerformed()
    {
        if (!CanCollectFragments)
        {
            return;
        }

        isGroupAActive = !isGroupAActive;
        if (platformTransition != null)
        {
            StopCoroutine(platformTransition);
        }

        platformTransition = StartCoroutine(TransitionPlatforms(
            isGroupAActive ? platformGroupB : platformGroupA,
            isGroupAActive ? platformGroupA : platformGroupB));

        for (int i = 0; i < fragments.Count; i++)
        {
            fragments[i]?.AdvanceToNextPoint();
        }
    }

    private IEnumerator TransitionPlatforms(Level03PlatformGroupRuntime outgoing, Level03PlatformGroupRuntime incoming)
    {
        Level03PlatformGroupRuntime.SetColliderState(incoming, true);
        float elapsed = 0f;
        float duration = Mathf.Max(platformTransitionDuration, 0.01f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Level03PlatformGroupRuntime.SetRendererAlpha(incoming, t);

            float outgoingAlpha = t <= platformFadeDelay / duration
                ? 1f
                : 1f - Mathf.InverseLerp(platformFadeDelay / duration, 1f, t);
            Level03PlatformGroupRuntime.SetRendererAlpha(outgoing, outgoingAlpha);
            yield return null;
        }

        Level03PlatformGroupRuntime.SetRendererAlpha(incoming, 1f);
        Level03PlatformGroupRuntime.SetRendererAlpha(outgoing, 0f);
        Level03PlatformGroupRuntime.SetColliderState(outgoing, false);
        platformTransition = null;
    }

    private IEnumerator MergeDragonGem()
    {
        CurrentState = Level03State.DragonGemMerging;
        ShowAnnouncement("Đang ghép Ngọc Rồng...", EncounterConfig.mergeAnnouncementDuration);
        SetPlayerControls(false);

        if (sceneCamera != null && completeGemRenderer != null)
        {
            sceneCamera.SetTarget(completeGemRenderer.transform);
        }

        if (bossPatrol != null)
        {
            bossPatrol.enabled = false;
        }

        yield return new WaitForSeconds(EncounterConfig.preMergeDelay);

        for (int i = 0; i < fragments.Count; i++)
        {
            fragments[i]?.PrepareForMerge();
        }

        float mergeDuration = EncounterConfig.mergeDuration;
        Vector3[] starts = new Vector3[fragments.Count];
        for (int i = 0; i < fragments.Count; i++)
        {
            starts[i] = fragments[i].transform.position;
        }

        float elapsed = 0f;
        while (elapsed < mergeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / mergeDuration));
            for (int i = 0; i < fragments.Count; i++)
            {
                Level03DragonFragment fragment = fragments[i];
                if (fragment == null)
                {
                    continue;
                }

                float orbit = (i == 0 ? -1f : 1f) * Mathf.Sin(t * Mathf.PI) * 0.65f;
                Vector3 position = Vector3.Lerp(starts[i], mergePoint, t);
                position.x += orbit;
                fragment.transform.position = position;
                fragment.transform.Rotate(0f, 0f, (i == 0 ? 360f : -360f) * Time.deltaTime);
            }

            yield return null;
        }

        for (int i = 0; i < fragments.Count; i++)
        {
            fragments[i]?.Hide();
        }

        if (completeGemRenderer != null)
        {
            completeGemRenderer.enabled = true;
        }

        mergeEffect?.PlayOnce();
        yield return new WaitForSeconds(EncounterConfig.postMergeEffectDelay);

        bossShield.BreakShield();
        if (bossPatrol != null)
        {
            bossPatrol.enabled = true;
        }

        CurrentState = Level03State.BossVulnerable;
        SetPlayerControls(true);
        if (sceneCamera != null)
        {
            sceneCamera.ClearTarget();
        }
        ShowAnnouncement("Boss Shield Broken! Phase 2 bắt đầu!", EncounterConfig.bossVulnerableAnnouncementDuration);

        yield return new WaitForSeconds(EncounterConfig.hideCompleteGemDelay);
        if (completeGemRenderer != null)
        {
            completeGemRenderer.enabled = false;
        }
    }

    private void HandleBossDied(EnemyHealth defeatedBoss)
    {
        CurrentState = Level03State.BossDefeated;
        ShowAnnouncement("Boss Defeated!", 3f);
    }

    private void SetPlayerControls(bool enabledState)
    {
        if (playerInput == null)
        {
            return;
        }

        if (enabledState)
        {
            playerInput.EnableAll();
        }
        else
        {
            playerInput.EnableMovement(false);
            playerInput.EnableJump(false);
            playerInput.EnableAttack(false);
            playerInput.EnablePowerShot(false);
            playerMovement.Stop();
        }
    }

    private void ShowAnnouncement(string message, float duration)
    {
        announcement = message;
        announcementUntil = Time.time + duration;
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        counterStyle ??= Level03GuiStyles.Create(18, new Color(1f, 0.85f, 0.1f), FontStyle.Bold);
        announcementStyle ??= Level03GuiStyles.Create(24, Color.white, FontStyle.Bold);

        GUI.Label(
            new Rect(Screen.width * 0.5f - 170f, 12f, 340f, 32f),
            $"Dragon Fragment: {collectedFragments}/{requiredFragments}",
            counterStyle);

        if (!string.IsNullOrEmpty(announcement) && Time.time <= announcementUntil)
        {
            GUI.Label(
                new Rect(Screen.width * 0.5f - 300f, 50f, 600f, 45f),
                announcement,
                announcementStyle);
        }
    }

    private Material GetSpriteUnlitMaterial()
    {
        if (spriteUnlitMaterial != null)
        {
            return spriteUnlitMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader != null)
        {
            spriteUnlitMaterial = new Material(shader);
        }

        return spriteUnlitMaterial;
    }

}
