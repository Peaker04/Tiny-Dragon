using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TinyDragon.Camera;
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
    private const int FragmentGridColumns = 3;
    private const float BossDetectionBoundsPadding = 2f;
    private const float BossDetectionFallbackRange = 100f;

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
    [SerializeField] private float fragmentMoveDuration = 0.32f;
    [SerializeField] private float fragmentPixelsPerUnit = 512f;
    [SerializeField] private Vector3 mergePoint = new Vector3(0f, 15.0f, 0f);

    private readonly List<Level03DragonFragment> fragments = new List<Level03DragonFragment>();
    private readonly List<int> fragmentTargetIndices = new List<int>();
    private readonly List<Collider2D> playerSolidColliders = new List<Collider2D>();
    private readonly List<Collider2D> bossSolidColliders = new List<Collider2D>();
    private readonly List<Collider2D> deferredPlatformColliderDisables = new List<Collider2D>();
    private Level03PlatformGroupRuntime platformGroupA;
    private Level03PlatformGroupRuntime platformGroupB;
    private Vector3[] fragmentTargetsA;
    private Vector3[] fragmentTargetsB;
    private PlayerMovement playerMovement;
    private PlayerInputReader playerInput;
    private EnemyHealth bossHealth;
    private EnemyPatrol bossPatrol;
    private BossAI bossAI;
    private Level03BossShield bossShield;
    private DragonGemEffectPlayer mergeEffect;
    private SpriteRenderer completeGemRenderer;
    private Coroutine platformTransition;
    private Coroutine deferredPlatformColliderDisableRoutine;
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
        EnforceLevel03SolidCollisions();
        bossShield.ActivateShield(this);
        SetBossCombatController(true);
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
            EnsurePlayerDeathSceneHandler();
        }

        JsonMultipartAnimationBridge animationBridge = ObjectLookup.Any<JsonMultipartAnimationBridge>();
        if (animationBridge != null)
        {
            bossHealth = animationBridge.GetComponent<EnemyHealth>();
            bossPatrol = animationBridge.GetComponent<EnemyPatrol>();
            bossAI = animationBridge.GetComponent<BossAI>();
            if (bossAI == null)
            {
                bossAI = animationBridge.gameObject.AddComponent<BossAI>();
            }
            ConfigureLevel03BossAnimation();
            ConfigureLevel03BossDetection();

            bossShield = animationBridge.GetComponent<Level03BossShield>();
            if (bossShield == null)
            {
                bossShield = animationBridge.gameObject.AddComponent<Level03BossShield>();
            }

            SetBossCombatController(false);
        }

        if (playerMovement == null || bossHealth == null || bossShield == null)
        {
            Debug.LogError("Level03Manager requires PlayerMovement and a JSON multipart boss in Level_03.", this);
            return false;
        }

        return true;
    }

    private void EnsurePlayerDeathSceneHandler()
    {
        if (playerMovement == null)
        {
            return;
        }

        PlayerHealth playerHealth = playerMovement.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            return;
        }

        if (playerMovement.GetComponent<PlayerDeathSceneHandler>() == null)
        {
            playerMovement.gameObject.AddComponent<PlayerDeathSceneHandler>();
        }
    }

    private void ConfigureLevel03BossAnimation()
    {
        if (bossAI == null)
        {
            return;
        }

        bossAI.ConfigureAnimationProfile(
            string.Empty,
            "walk",
            "walk",
            "attack",
            "Attack",
            "rangeAttack",
            "rangeAttack",
            "attack",
            "Attack",
            "Hit");
    }

    private void ConfigureLevel03BossDetection()
    {
        if (bossAI == null)
        {
            return;
        }

        MapBounds2D mapBounds = ObjectLookup.Any<MapBounds2D>();
        if (mapBounds != null)
        {
            Bounds bounds = mapBounds.GetBounds();
            bossAI.SetDetectionAwareness(
                bounds.size.x + BossDetectionBoundsPadding * 2f,
                bounds.size.y + BossDetectionBoundsPadding * 2f);
            return;
        }

        bossAI.SetDetectionAwareness(BossDetectionFallbackRange, BossDetectionFallbackRange);
    }

    private void SetBossCombatController(bool enabledState)
    {
        if (bossPatrol != null)
        {
            bossPatrol.enabled = false;
        }
        if (bossAI != null)
        {
            bossAI.enabled = false;
        }

        if (bossAI != null)
        {
            bossAI.enabled = enabledState;
        }

        EnforceLevel03SolidCollisions();
    }

    private void EnforceLevel03SolidCollisions()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        RefreshActorSolidColliders();
        SetIgnoredCollisionPairs(playerSolidColliders, bossSolidColliders, true);
        EnforcePlatformCollision(platformGroupA);
        EnforcePlatformCollision(platformGroupB);
    }

    private void RefreshActorSolidColliders()
    {
        playerSolidColliders.Clear();
        bossSolidColliders.Clear();

        if (playerMovement != null)
        {
            AddSolidColliders(playerMovement.gameObject, playerSolidColliders);
        }

        if (bossHealth != null)
        {
            AddSolidColliders(bossHealth.gameObject, bossSolidColliders);
        }
    }

    private void EnforcePlatformCollision(Level03PlatformGroupRuntime group)
    {
        if (group == null)
        {
            return;
        }

        SetIgnoredCollisionPairs(playerSolidColliders, group.colliders, false);
        SetIgnoredCollisionPairs(bossSolidColliders, group.colliders, true);
    }

    private void DisablePlatformCollidersWhenActorsClear(Level03PlatformGroupRuntime group)
    {
        if (group == null)
        {
            return;
        }

        RefreshActorSolidColliders();
        for (int i = 0; i < group.colliders.Count; i++)
        {
            Collider2D collider = group.colliders[i];
            if (collider == null)
            {
                continue;
            }

            if (IsTouchingAnyPlayer(collider))
            {
                collider.enabled = true;
                if (!deferredPlatformColliderDisables.Contains(collider))
                {
                    deferredPlatformColliderDisables.Add(collider);
                }
                continue;
            }

            collider.enabled = false;
        }

        if (deferredPlatformColliderDisables.Count > 0 && deferredPlatformColliderDisableRoutine == null)
        {
            deferredPlatformColliderDisableRoutine = StartCoroutine(DisableDeferredPlatformColliders());
        }
    }

    private IEnumerator DisableDeferredPlatformColliders()
    {
        WaitForFixedUpdate wait = new WaitForFixedUpdate();
        while (deferredPlatformColliderDisables.Count > 0)
        {
            RefreshActorSolidColliders();
            for (int i = deferredPlatformColliderDisables.Count - 1; i >= 0; i--)
            {
                Collider2D collider = deferredPlatformColliderDisables[i];
                if (collider == null)
                {
                    deferredPlatformColliderDisables.RemoveAt(i);
                    continue;
                }

                if (IsColliderInActivePlatformGroup(collider))
                {
                    deferredPlatformColliderDisables.RemoveAt(i);
                    continue;
                }

                if (IsTouchingAnyPlayer(collider))
                {
                    continue;
                }

                collider.enabled = false;
                deferredPlatformColliderDisables.RemoveAt(i);
            }

            yield return wait;
        }

        deferredPlatformColliderDisableRoutine = null;
    }

    private bool IsColliderInActivePlatformGroup(Collider2D collider)
    {
        Level03PlatformGroupRuntime activeGroup = isGroupAActive ? platformGroupA : platformGroupB;
        return activeGroup != null && activeGroup.colliders.Contains(collider);
    }

    private bool IsTouchingAnyPlayer(Collider2D platformCollider)
    {
        return IsTouchingAny(platformCollider, playerSolidColliders);
    }

    private static bool IsTouchingAny(Collider2D collider, List<Collider2D> otherColliders)
    {
        if (collider == null || otherColliders == null)
        {
            return false;
        }

        for (int i = 0; i < otherColliders.Count; i++)
        {
            Collider2D other = otherColliders[i];
            if (other != null && collider.IsTouching(other))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddSolidColliders(GameObject root, List<Collider2D> colliders)
    {
        if (root == null)
        {
            return;
        }

        Collider2D[] rootColliders = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < rootColliders.Length; i++)
        {
            Collider2D collider = rootColliders[i];
            if (collider == null || collider.isTrigger)
            {
                continue;
            }

            colliders.Add(collider);
        }
    }

    private static void SetIgnoredCollisionPairs(List<Collider2D> firstGroup, List<Collider2D> secondGroup, bool ignore)
    {
        if (firstGroup == null || secondGroup == null)
        {
            return;
        }

        for (int i = 0; i < firstGroup.Count; i++)
        {
            Collider2D first = firstGroup[i];
            if (first == null)
            {
                continue;
            }

            for (int j = 0; j < secondGroup.Count; j++)
            {
                Collider2D second = secondGroup[j];
                if (second == null || first == second || first.attachedRigidbody == second.attachedRigidbody)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(first, second, ignore);
            }
        }
    }

    private void ApplyConfigDefaults()
    {
        Level03EncounterConfig config = EncounterConfig;
        requiredFragments = Mathf.Max(1, config.requiredFragments);
        platformFadeDelay = config.platformFadeDelay;
        platformTransitionDuration = config.platformTransitionDuration;
        fragmentMoveDuration = config.fragmentMoveDuration;
        fragmentPixelsPerUnit = config.fragmentPixelsPerUnit;
        mergePoint = config.mergePoint;
    }

    private void BuildEncounterHierarchy(bool runtimeState)
    {
        fragments.Clear();
        fragmentTargetIndices.Clear();
        Transform flyingPlatforms = GetOrCreateRoot("FlyingPlatforms", transform);
        Transform groupARoot = GetOrCreateRoot("PlatformGroup_A", flyingPlatforms);
        Transform groupBRoot = GetOrCreateRoot("PlatformGroup_B", flyingPlatforms);

        platformGroupA = new Level03PlatformGroupRuntime();
        platformGroupB = new Level03PlatformGroupRuntime();

        Vector3[] platformsA = CopyPlatformPositions(EncounterConfig.platformGroupA);
        Vector3[] platformsB = CopyPlatformPositions(EncounterConfig.platformGroupB);

        CreatePlatforms("Platform_A", platformsA, groupARoot, platformGroupA);
        CreatePlatforms("Platform_B", platformsB, groupBRoot, platformGroupB);

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
        CreateFragments(fragmentRoot, platformsA, platformsB);

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

        EnforceLevel03SolidCollisions();
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
        fragmentTargetsA = BuildFragmentTargets(platformsA, fragmentStandOffset);
        fragmentTargetsB = BuildFragmentTargets(platformsB, fragmentStandOffset);

        int firstStartIndex = PickSpawnTargetIndex(fragmentTargetsA, 0, 1, -1);
        int secondStartIndex = PickSpawnTargetIndex(fragmentTargetsA, 2, 4, firstStartIndex);

        fragments.Add(CreateFragment("Fragment_01", 1, leftFragment, fragmentTargetsA[firstStartIndex], parent));
        fragmentTargetIndices.Add(firstStartIndex);
        fragments.Add(CreateFragment("Fragment_02", 2, rightFragment, fragmentTargetsA[secondStartIndex], parent));
        fragmentTargetIndices.Add(secondStartIndex);
    }

    private Level03DragonFragment CreateFragment(
        string objectName,
        int index,
        Sprite sprite,
        Vector3 startPosition,
        Transform parent)
    {
        Transform fragmentTransform = GetOrCreateRoot(objectName, parent);
        GameObject fragmentObject = fragmentTransform.gameObject;
        fragmentObject.transform.localScale = Vector3.one * 0.8f;
        Level03DragonFragment fragment = GetOrCreateComponent<Level03DragonFragment>(fragmentObject);
        fragment.Initialize(this, index, sprite, startPosition, GetSpriteUnlitMaterial());
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
        collider.isTrigger = false;
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

    private void CreatePlatforms(
        string platformPrefix,
        Vector3[] positions,
        Transform parent,
        Level03PlatformGroupRuntime group)
    {
        for (int i = 0; i < positions.Length; i++)
        {
            CreatePlatform($"{platformPrefix}{i + 1}", positions[i], parent, group);
        }
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

    private static Vector3[] CopyPlatformPositions(Vector3[] sourcePositions)
    {
        if (sourcePositions == null || sourcePositions.Length == 0)
        {
            return new Vector3[0];
        }

        Vector3[] positions = new Vector3[sourcePositions.Length];
        for (int i = 0; i < sourcePositions.Length; i++)
        {
            positions[i] = sourcePositions[i];
        }

        return positions;
    }

    private static Vector3[] BuildFragmentTargets(
        Vector3[] platforms,
        Vector3 offset)
    {
        if (platforms == null || platforms.Length == 0)
        {
            return new[] { offset };
        }

        Vector3[] targets = new Vector3[platforms.Length];
        for (int i = 0; i < platforms.Length; i++)
        {
            targets[i] = platforms[i] + offset;
        }

        return targets;
    }

    private static int PickSpawnTargetIndex(Vector3[] targets, int minTier, int maxTier, int avoidIndex)
    {
        if (targets == null || targets.Length == 0)
        {
            return 0;
        }

        if (targets.Length == 1)
        {
            return Mathf.Clamp(avoidIndex == 0 ? targets.Length - 1 : 0, 0, targets.Length - 1);
        }

        List<int> candidates = new List<int>();
        for (int i = 0; i < targets.Length; i++)
        {
            int tier = GetFragmentTier(i);
            if (tier >= minTier && tier <= maxTier && i != avoidIndex)
            {
                candidates.Add(i);
            }
        }

        if (candidates.Count == 0)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (i != avoidIndex)
                {
                    candidates.Add(i);
                }
            }
        }

        if (candidates.Count == 0)
        {
            return 0;
        }

        return Application.isPlaying
            ? candidates[Random.Range(0, candidates.Count)]
            : candidates[0];
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

        MoveFragmentsToRuleTargets(isGroupAActive ? fragmentTargetsA : fragmentTargetsB);
    }

    private void MoveFragmentsToRuleTargets(Vector3[] targets)
    {
        if (targets == null || targets.Length == 0)
        {
            return;
        }

        HashSet<int> usedTargetIndices = new HashSet<int>();
        for (int i = 0; i < fragments.Count; i++)
        {
            Level03DragonFragment fragment = fragments[i];
            if (fragment == null || fragment.IsCollected)
            {
                continue;
            }

            int currentIndex = i < fragmentTargetIndices.Count
                ? Mathf.Clamp(fragmentTargetIndices[i], 0, targets.Length - 1)
                : 0;
            int selectedIndex = PickRuleTargetIndex(targets, currentIndex, fragment.FragmentIndex, usedTargetIndices);
            usedTargetIndices.Add(selectedIndex);
            SetFragmentTargetIndex(i, selectedIndex);
            fragment.MoveTo(targets[selectedIndex], fragmentMoveDuration);
        }
    }

    private static int PickRuleTargetIndex(
        Vector3[] targets,
        int currentIndex,
        int fragmentIndex,
        HashSet<int> usedTargetIndices)
    {
        List<int> candidates = BuildRuleCandidates(targets, currentIndex, usedTargetIndices);
        if (candidates.Count == 0 && usedTargetIndices.Count > 0)
        {
            candidates = BuildRuleCandidates(targets, currentIndex, null);
        }

        if (candidates.Count == 0)
        {
            return Mathf.Clamp(currentIndex, 0, targets.Length - 1);
        }

        int totalWeight = 0;
        int[] weights = new int[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            int weight = ScoreFragmentTarget(currentIndex, candidates[i], fragmentIndex);
            weights[i] = weight;
            totalWeight += weight;
        }

        if (!Application.isPlaying)
        {
            int bestIndex = 0;
            for (int i = 1; i < candidates.Count; i++)
            {
                if (weights[i] > weights[bestIndex])
                {
                    bestIndex = i;
                }
            }

            return candidates[bestIndex];
        }

        int roll = Random.Range(0, totalWeight);
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= weights[i];
            if (roll < 0)
            {
                return candidates[i];
            }
        }

        return candidates[candidates.Count - 1];
    }

    private static List<int> BuildRuleCandidates(
        Vector3[] targets,
        int currentIndex,
        HashSet<int> usedTargetIndices)
    {
        List<int> candidates = new List<int>();
        int currentTier = GetFragmentTier(currentIndex);
        int currentColumn = GetFragmentColumn(currentIndex);

        for (int i = 0; i < targets.Length; i++)
        {
            if (usedTargetIndices != null && usedTargetIndices.Contains(i))
            {
                continue;
            }

            int tierDelta = GetFragmentTier(i) - currentTier;
            int columnDelta = Mathf.Abs(GetFragmentColumn(i) - currentColumn);
            if (Mathf.Abs(tierDelta) <= 1 && columnDelta <= 1)
            {
                candidates.Add(i);
            }
        }

        if (candidates.Count > 1)
        {
            candidates.Remove(currentIndex);
        }

        return candidates;
    }

    private static int ScoreFragmentTarget(int currentIndex, int targetIndex, int fragmentIndex)
    {
        int currentTier = GetFragmentTier(currentIndex);
        int currentColumn = GetFragmentColumn(currentIndex);
        int targetTier = GetFragmentTier(targetIndex);
        int targetColumn = GetFragmentColumn(targetIndex);
        int score = 10;

        if (targetTier == currentTier + 1)
        {
            score += 45;
        }

        if (targetColumn != currentColumn)
        {
            score += 25;
        }

        if (currentTier >= 3 && targetTier == currentTier - 1)
        {
            score += 20;
        }

        if (fragmentIndex == 1 && targetColumn > currentColumn)
        {
            score += 10;
        }

        if (fragmentIndex == 2 && targetColumn < currentColumn)
        {
            score += 10;
        }

        return score;
    }

    private void SetFragmentTargetIndex(int fragmentListIndex, int targetIndex)
    {
        while (fragmentTargetIndices.Count <= fragmentListIndex)
        {
            fragmentTargetIndices.Add(0);
        }

        fragmentTargetIndices[fragmentListIndex] = targetIndex;
    }

    private static int GetFragmentTier(int targetIndex)
    {
        return targetIndex / FragmentGridColumns;
    }

    private static int GetFragmentColumn(int targetIndex)
    {
        return targetIndex % FragmentGridColumns;
    }

    private IEnumerator TransitionPlatforms(Level03PlatformGroupRuntime outgoing, Level03PlatformGroupRuntime incoming)
    {
        Level03PlatformGroupRuntime.SetColliderState(incoming, true);
        EnforceLevel03SolidCollisions();
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
        DisablePlatformCollidersWhenActorsClear(outgoing);
        EnforceLevel03SolidCollisions();
        platformTransition = null;
    }

    private IEnumerator MergeDragonGem()
    {
        CurrentState = Level03State.DragonGemMerging;
        ShowAnnouncement("Đang ghép Ngọc Rồng...", EncounterConfig.mergeAnnouncementDuration);
        SetPlayerControls(false);
        SetBossCombatController(false);

        if (sceneCamera != null && completeGemRenderer != null)
        {
            sceneCamera.SetTarget(completeGemRenderer.transform);
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

        HideEncounterPlatforms();
        bossShield.BreakShield();
        SetBossCombatController(true);

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

    private void HideEncounterPlatforms()
    {
        if (platformTransition != null)
        {
            StopCoroutine(platformTransition);
            platformTransition = null;
        }

        if (deferredPlatformColliderDisableRoutine != null)
        {
            StopCoroutine(deferredPlatformColliderDisableRoutine);
            deferredPlatformColliderDisableRoutine = null;
        }
        deferredPlatformColliderDisables.Clear();

        Level03PlatformGroupRuntime.SetState(platformGroupA, 0f, false);
        Level03PlatformGroupRuntime.SetState(platformGroupB, 0f, false);
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
