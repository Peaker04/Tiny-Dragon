using System.Collections.Generic;
using TinyDragon.Combat;
using TinyDragon.Shared.Animation;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public sealed class VoDaiXenBoHungEncounter : MonoBehaviour
{
    [System.Serializable]
    private struct MinibossEntry
    {
        public string resourcePath;
        public string textureResourcePath;
        public string jsonResourcePath;
        public float normalizedX;
    }

    [SerializeField] private string sceneName = "VoDaiXenBoHung";
    [SerializeField] private BossAI boss;
    [SerializeField] private Collider2D groundCollider;
    [SerializeField] private float spawnPadding = 0.8f;
    [SerializeField] private float spawnYOffset = 0.02f;
    [SerializeField] private float minibossVisualScale = 0.95f;
    [SerializeField] private Vector2 minibossMinimumColliderSize = new Vector2(1.15f, 1.35f);
    [SerializeField] private float minibossColliderVisualPadding = 0.12f;
    [SerializeField] private float minibossProjectileScale = 0.62f;
    [SerializeField] private Vector2 minibossProjectileSpawnOffset = new Vector2(0.65f, 0.45f);
    [SerializeField] private float minibossRangedAttackDistance = 4.25f;
    [SerializeField] private float playerBelowGroundTolerance = 0.65f;
    [SerializeField] private float playerAboveGroundAwareness = 3.5f;
    [SerializeField] private bool ensureMinibossesVisibleInEditMode = true;
    [SerializeField] private bool allowActorsToPassThroughEachOther = true;
    [SerializeField] private float inactiveMinibossBobAmplitude = 0.12f;
    [SerializeField] private float inactiveMinibossBobFrequency = 3f;
    [SerializeField] private MinibossEntry[] minibosses =
    {
        new MinibossEntry
        {
            resourcePath = "Enemies/CellJrMinibosses/CellJr_58_NormalShot",
            textureResourcePath = "Enemies/CellJrMinibosses/Textures/58",
            jsonResourcePath = "Enemies/CellJrMinibosses/Data/58",
            normalizedX = 0.2f
        },
        new MinibossEntry
        {
            resourcePath = "Enemies/CellJrMinibosses/CellJr_63_PowerShot",
            textureResourcePath = "Enemies/CellJrMinibosses/Textures/63",
            jsonResourcePath = "Enemies/CellJrMinibosses/Data/63",
            normalizedX = 0.4f
        },
        new MinibossEntry
        {
            resourcePath = "Enemies/CellJrMinibosses/CellJr_64_Punch",
            textureResourcePath = "Enemies/CellJrMinibosses/Textures/64",
            jsonResourcePath = "Enemies/CellJrMinibosses/Data/64",
            normalizedX = 0.6f
        },
        new MinibossEntry
        {
            resourcePath = "Enemies/CellJrMinibosses/CellJr_65_Kick",
            textureResourcePath = "Enemies/CellJrMinibosses/Textures/65",
            jsonResourcePath = "Enemies/CellJrMinibosses/Data/65",
            normalizedX = 0.8f
        }
    };

    private readonly List<EnemyHealth> activeMinibossHealth = new List<EnemyHealth>();
    private Transform minibossParent;
    private bool bossActivated;
    private bool minibossesSpawned;
    private int activeMinibossIndex = -1;
    private float nextPassThroughRefreshTime;

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            EnsureSceneMinibossesForEditing();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall += EnsureSceneMinibossesForEditing;
        }
    }
#endif

    private void Awake()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (SceneManager.GetActiveScene().name != sceneName)
        {
            enabled = false;
            return;
        }

        ResolveSceneReferences();
        HideBossUntilMinibossesDefeated();
    }

    private void Start()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!enabled || minibossesSpawned)
        {
            return;
        }

        SpawnMinibosses();
    }

    private void FixedUpdate()
    {
        if (!Application.isPlaying || !allowActorsToPassThroughEachOther || Time.time < nextPassThroughRefreshTime)
        {
            return;
        }

        nextPassThroughRefreshTime = Time.time + 0.2f;
        ConfigureActorPassThrough();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || activeMinibossHealth.Count == 0)
        {
            return;
        }

        AnimateInactiveMinibossesInPlace();
    }

    private void OnDestroy()
    {
        foreach (EnemyHealth health in activeMinibossHealth)
        {
            if (health != null)
            {
                health.Died -= HandleMinibossDied;
            }
        }
    }

    private void ResolveSceneReferences()
    {
        if (boss == null)
        {
            boss = FindBoss();
        }

        groundCollider = FindArenaGround(groundCollider);
        minibossParent = GetOrCreateChild("Minibosses");
    }

    private void HideBossUntilMinibossesDefeated()
    {
        if (boss == null)
        {
            Debug.LogWarning("VoDaiXenBoHungEncounter could not find the main boss.", this);
            return;
        }

        ConfigureBossAwareness();
        boss.gameObject.SetActive(false);
    }

    private void SpawnMinibosses()
    {
        if (groundCollider == null)
        {
            Debug.LogWarning("VoDaiXenBoHungEncounter could not find a ground collider.", this);
            return;
        }

        minibossesSpawned = true;
        activeMinibossHealth.Clear();

        for (int i = 0; i < minibosses.Length; i++)
        {
            EnemyHealth health = ConfigureSceneOrSpawnedMiniboss(minibosses[i]);
            if (health == null)
            {
                continue;
            }

            health.ResetHealth();
            health.Died += HandleMinibossDied;
            activeMinibossHealth.Add(health);
            SetMinibossActive(health, false);
        }

        if (activeMinibossHealth.Count == 0)
        {
            ActivateBoss();
            return;
        }

        ActivateMinibossAt(0);
        ConfigureActorPassThrough();
    }

    private EnemyHealth ConfigureSceneOrSpawnedMiniboss(MinibossEntry entry)
    {
        EnemyPatrol miniboss = FindSceneMiniboss(entry);
        if (miniboss == null)
        {
            miniboss = SpawnMiniboss(entry);
        }

        if (miniboss == null)
        {
            return null;
        }

        ConfigureMiniboss(miniboss, entry);

        EnemyHealth health = miniboss.GetComponent<EnemyHealth>();
        if (health == null)
        {
            Debug.LogWarning("Configured miniboss is missing EnemyHealth.", miniboss);
            return null;
        }

        return health;
    }

    private EnemyPatrol SpawnMiniboss(MinibossEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.resourcePath))
        {
            return null;
        }

        GameObject templateObject = Resources.Load<GameObject>(entry.resourcePath);
        if (templateObject == null)
        {
            Debug.LogWarning($"Missing miniboss prefab at Resources/{entry.resourcePath}.", this);
            return null;
        }

        EnemyPatrol template = templateObject.GetComponent<EnemyPatrol>();
        if (template == null)
        {
            Debug.LogWarning($"Miniboss prefab at Resources/{entry.resourcePath} is missing EnemyPatrol.", this);
            return null;
        }

        Vector3 spawnPosition = GetGroundSpawnPosition(entry.normalizedX, templateObject.transform.position.z);
        GameObject minibossObject = Instantiate(templateObject, spawnPosition, templateObject.transform.rotation, minibossParent);
        EnemyPatrol miniboss = minibossObject.GetComponent<EnemyPatrol>();
        miniboss.name = templateObject.name;
        return miniboss;
    }

    private void ConfigureMiniboss(EnemyPatrol miniboss, MinibossEntry entry)
    {
        ConfigureMinibossBridge(miniboss, entry);
        miniboss.gameObject.SetActive(true);
        ConfigureMinibossProjectile(miniboss);
        FitMinibossColliderToVisual(miniboss.gameObject);
        PlaceOnGround(miniboss.transform, miniboss.GetComponent<Collider2D>(), spawnYOffset);
        ConfigureMinibossArena(miniboss);
    }

    private void ConfigureMinibossBridge(EnemyPatrol miniboss, MinibossEntry entry)
    {
        JsonMultipartAnimationBridge bridge = miniboss.GetComponent<JsonMultipartAnimationBridge>();
        if (bridge == null)
        {
            return;
        }

        bridge.texture = Resources.Load<Texture2D>(entry.textureResourcePath);
        bridge.jsonFile = Resources.Load<TextAsset>(entry.jsonResourcePath);
        bridge.textureCoordinateScale = 4f;
        bridge.scale = minibossVisualScale;
    }

    private void ConfigureMinibossProjectile(EnemyPatrol miniboss)
    {
        JsonMultipartAnimationBridge bridge = miniboss.GetComponent<JsonMultipartAnimationBridge>();
        EnemyProjectileShooter shooter = miniboss.GetComponent<EnemyProjectileShooter>();
        if (bridge == null || shooter == null)
        {
            return;
        }

        Sprite[] projectileSprites = bridge.CreateRangedAttackEffectSprites();
        if (projectileSprites != null && projectileSprites.Length > 0)
        {
            shooter.ConfigureProjectileAnimation(projectileSprites, bridge.frameRate, minibossProjectileScale, minibossProjectileSpawnOffset);
            return;
        }

        Sprite projectileSprite = bridge.CreateLargestAttackEffectSprite();
        shooter.ConfigureProjectileVisual(projectileSprite, minibossProjectileScale, minibossProjectileSpawnOffset);
    }

    private void FitMinibossColliderToVisual(GameObject minibossObject)
    {
        if (minibossObject == null)
        {
            return;
        }

        BoxCollider2D boxCollider = minibossObject.GetComponent<BoxCollider2D>();
        if (boxCollider == null)
        {
            return;
        }

        if (!TryGetLocalVisualBounds(minibossObject.transform, out Bounds visualBounds))
        {
            boxCollider.size = minibossMinimumColliderSize;
            boxCollider.offset = new Vector2(0f, minibossMinimumColliderSize.y * 0.5f);
            return;
        }

        float width = Mathf.Max(minibossMinimumColliderSize.x, visualBounds.size.x + minibossColliderVisualPadding);
        float height = Mathf.Max(minibossMinimumColliderSize.y, visualBounds.size.y + minibossColliderVisualPadding);
        float bottom = visualBounds.min.y - minibossColliderVisualPadding * 0.5f;
        float centerY = bottom + height * 0.5f;

        boxCollider.size = new Vector2(width, height);
        boxCollider.offset = new Vector2(visualBounds.center.x, centerY);
    }

    private static bool TryGetLocalVisualBounds(Transform root, out Bounds localBounds)
    {
        localBounds = default;
        bool hasBounds = false;
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || renderer.sprite == null || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds rendererBounds = renderer.bounds;
            Vector3 localMin = root.InverseTransformPoint(rendererBounds.min);
            Vector3 localMax = root.InverseTransformPoint(rendererBounds.max);
            Bounds rendererLocalBounds = new Bounds((localMin + localMax) * 0.5f, localMax - localMin);

            if (!hasBounds)
            {
                localBounds = rendererLocalBounds;
                hasBounds = true;
                continue;
            }

            localBounds.Encapsulate(rendererLocalBounds.min);
            localBounds.Encapsulate(rendererLocalBounds.max);
        }

        return hasBounds;
    }

    private Vector3 GetGroundSpawnPosition(float normalizedX, float z)
    {
        Bounds bounds = groundCollider.bounds;
        float minX = bounds.min.x + spawnPadding;
        float maxX = bounds.max.x - spawnPadding;
        float spawnX = Mathf.Lerp(minX, maxX, Mathf.Clamp01(normalizedX));
        return new Vector3(spawnX, bounds.max.y + spawnYOffset, z);
    }

    private void ConfigureMinibossArena(EnemyPatrol miniboss)
    {
        Bounds bounds = groundCollider.bounds;
        Collider2D minibossCollider = miniboss.GetComponent<Collider2D>();
        float halfWidth = minibossCollider != null ? minibossCollider.bounds.extents.x : 0f;
        miniboss.SetPatrolBounds(bounds.min.x + halfWidth, bounds.max.x - halfWidth);
        miniboss.SetArenaAwarenessBounds(
            bounds,
            spawnPadding,
            playerBelowGroundTolerance,
            playerAboveGroundAwareness,
            minibossRangedAttackDistance,
            true
        );
    }

    private void HandleMinibossDied(EnemyHealth defeated)
    {
        if (defeated != null)
        {
            defeated.Died -= HandleMinibossDied;
        }

        if (activeMinibossIndex >= 0 && activeMinibossIndex < activeMinibossHealth.Count && activeMinibossHealth[activeMinibossIndex] == defeated)
        {
            ActivateMinibossAt(activeMinibossIndex + 1);
            return;
        }

        if (NoRemainingMinibosses())
        {
            ActivateBoss();
        }
    }

    private void ActivateMinibossAt(int index)
    {
        activeMinibossIndex = index;
        while (activeMinibossIndex < activeMinibossHealth.Count && activeMinibossHealth[activeMinibossIndex] == null)
        {
            activeMinibossIndex++;
        }

        if (activeMinibossIndex >= activeMinibossHealth.Count)
        {
            ActivateBoss();
            return;
        }

        for (int i = 0; i < activeMinibossHealth.Count; i++)
        {
            EnemyHealth health = activeMinibossHealth[i];
            if (health == null)
            {
                continue;
            }

            bool isActiveMiniboss = i == activeMinibossIndex;
            SetMinibossActive(health, isActiveMiniboss);
            if (isActiveMiniboss)
            {
                health.ResetHealth();
            }
        }

        ConfigureActorPassThrough();
    }

    private bool NoRemainingMinibosses()
    {
        foreach (EnemyHealth health in activeMinibossHealth)
        {
            if (health != null)
            {
                return false;
            }
        }

        return true;
    }

    private static void SetMinibossActive(EnemyHealth health, bool isActiveMiniboss)
    {
        if (health == null)
        {
            return;
        }

        EnemyPatrol patrol = health.GetComponent<EnemyPatrol>();
        if (patrol != null)
        {
            patrol.enabled = isActiveMiniboss;
        }

        EnemyProjectileShooter shooter = health.GetComponent<EnemyProjectileShooter>();
        if (shooter != null)
        {
            shooter.enabled = isActiveMiniboss;
        }

        Rigidbody2D body = health.GetComponent<Rigidbody2D>();
        if (body != null && !isActiveMiniboss)
        {
            body.linearVelocity = Vector2.zero;
        }

        RequiredPlayerDamageSourceFilter filter = health.GetComponent<RequiredPlayerDamageSourceFilter>();
        if (filter != null)
        {
            filter.SetDamageEnabled(isActiveMiniboss);
        }

        if (isActiveMiniboss)
        {
            JsonMultipartAnimationBridge bridge = health.GetComponent<JsonMultipartAnimationBridge>();
            if (bridge != null)
            {
                bridge.SetRuntimeVisualOffsetY(0f);
            }
        }
    }

    private void AnimateInactiveMinibossesInPlace()
    {
        float frequency = Mathf.Max(0.01f, inactiveMinibossBobFrequency);

        for (int i = 0; i < activeMinibossHealth.Count; i++)
        {
            EnemyHealth health = activeMinibossHealth[i];
            if (health == null)
            {
                continue;
            }

            JsonMultipartAnimationBridge bridge = health.GetComponent<JsonMultipartAnimationBridge>();
            if (bridge == null)
            {
                continue;
            }

            if (i == activeMinibossIndex)
            {
                bridge.SetRuntimeVisualOffsetY(0f);
                continue;
            }

            float phase = i * 0.8f;
            float offsetY = Mathf.Sin(Time.time * frequency + phase) * inactiveMinibossBobAmplitude;
            bridge.SetRuntimeVisualOffsetY(offsetY);
        }
    }

    private void ActivateBoss()
    {
        if (bossActivated || boss == null)
        {
            return;
        }

        bossActivated = true;
        boss.gameObject.SetActive(true);
        PlaceOnGround(boss.transform, boss.GetComponent<Collider2D>(), spawnYOffset);
        ConfigureBossAwareness();
        ConfigureActorPassThrough();

        EnemyHealth bossHealth = boss.GetComponent<EnemyHealth>();
        if (bossHealth != null)
        {
            bossHealth.ResetHealth();
        }
    }

    private void ConfigureBossAwareness()
    {
        if (boss == null || groundCollider == null)
        {
            return;
        }

        Bounds bounds = groundCollider.bounds;
        boss.SetArenaAwarenessBounds(bounds, spawnPadding, playerBelowGroundTolerance, playerAboveGroundAwareness);
    }

    private void ConfigureActorPassThrough()
    {
        if (!Application.isPlaying || !allowActorsToPassThroughEachOther)
        {
            return;
        }

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            return;
        }

        List<Collider2D> playerColliders = new List<Collider2D>();
        List<Collider2D> actorColliders = new List<Collider2D>();

        AddSolidColliders(player.gameObject, playerColliders);

        if (boss != null)
        {
            AddSolidColliders(boss.gameObject, actorColliders);
        }

        AddMinibossSolidColliders(actorColliders);

        SetIgnoredCollisionPairs(playerColliders, actorColliders, true);
        SetIgnoredCollisionPairs(actorColliders, actorColliders, true);
    }

    private void AddMinibossSolidColliders(List<Collider2D> actorColliders)
    {
        if (minibossParent == null)
        {
            return;
        }

        EnemyHealth[] minibossHealthComponents = minibossParent.GetComponentsInChildren<EnemyHealth>(true);
        foreach (EnemyHealth minibossHealth in minibossHealthComponents)
        {
            if (minibossHealth != null && minibossHealth.gameObject.activeInHierarchy)
            {
                AddSolidColliders(minibossHealth.gameObject, actorColliders);
            }
        }
    }

    private static void AddSolidColliders(GameObject root, List<Collider2D> colliders)
    {
        if (root == null)
        {
            return;
        }

        Collider2D[] rootColliders = root.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D collider in rootColliders)
        {
            if (collider == null || collider.isTrigger)
            {
                continue;
            }

            colliders.Add(collider);
        }
    }

    private static void SetIgnoredCollisionPairs(List<Collider2D> firstGroup, List<Collider2D> secondGroup, bool ignore)
    {
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

    private void PlaceOnGround(Transform target, Collider2D bodyCollider, float yOffset)
    {
        if (target == null || bodyCollider == null || groundCollider == null)
        {
            return;
        }

        Vector3 position = target.position;
        float bottomOffset = position.y - bodyCollider.bounds.min.y;
        position.y = groundCollider.bounds.max.y + bottomOffset + yOffset;
        target.position = position;
    }

    private static BossAI FindBoss()
    {
        BossAI[] bosses = FindObjectsByType<BossAI>(FindObjectsInactive.Include);
        foreach (BossAI candidate in bosses)
        {
            if (candidate.name.Contains("MonsterBoss_Act1"))
            {
                return candidate;
            }
        }

        return bosses.Length > 0 ? bosses[0] : null;
    }

    private Transform GetOrCreateChild(string childName)
    {
        Transform existing = transform.Find(childName);
        if (existing != null)
        {
            return existing;
        }

        GameObject childObject = new GameObject(childName);
        childObject.transform.SetParent(transform, false);
        return childObject.transform;
    }

    private EnemyPatrol FindSceneMiniboss(MinibossEntry entry)
    {
        if (minibossParent == null || string.IsNullOrWhiteSpace(entry.resourcePath))
        {
            return null;
        }

        string objectName = ResourcePathToObjectName(entry.resourcePath);
        Transform child = minibossParent.Find(objectName);
        return child != null ? child.GetComponent<EnemyPatrol>() : null;
    }

    private static string ResourcePathToObjectName(string resourcePath)
    {
        int slashIndex = resourcePath.LastIndexOf('/');
        return slashIndex >= 0 ? resourcePath.Substring(slashIndex + 1) : resourcePath;
    }

    private void EnsureSceneMinibossesForEditing()
    {
        if (Application.isPlaying || !ensureMinibossesVisibleInEditMode || this == null)
        {
            return;
        }

        if (!gameObject.scene.IsValid() || (!string.IsNullOrWhiteSpace(sceneName) && gameObject.scene.name != sceneName))
        {
            return;
        }

        ResolveSceneReferences();
        if (groundCollider == null || minibossParent == null)
        {
            return;
        }

        for (int i = 0; i < minibosses.Length; i++)
        {
            EnsureEditableMiniboss(minibosses[i]);
        }

#if UNITY_EDITOR
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    private void EnsureEditableMiniboss(MinibossEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.resourcePath) || FindSceneMiniboss(entry) != null)
        {
            return;
        }

        GameObject templateObject = Resources.Load<GameObject>(entry.resourcePath);
        if (templateObject == null)
        {
            return;
        }

        GameObject minibossObject = CreateEditableMinibossObject(templateObject);
        if (minibossObject == null)
        {
            return;
        }

        minibossObject.name = ResourcePathToObjectName(entry.resourcePath);
        minibossObject.transform.SetParent(minibossParent, false);
        minibossObject.transform.position = GetGroundSpawnPosition(entry.normalizedX, templateObject.transform.position.z);
        minibossObject.transform.rotation = templateObject.transform.rotation;
        minibossObject.SetActive(true);

        EnemyPatrol miniboss = minibossObject.GetComponent<EnemyPatrol>();
        if (miniboss != null)
        {
            ConfigureMiniboss(miniboss, entry);
        }
    }

    private GameObject CreateEditableMinibossObject(GameObject templateObject)
    {
#if UNITY_EDITOR
        GameObject prefabInstance = PrefabUtility.InstantiatePrefab(templateObject, gameObject.scene) as GameObject;
        if (prefabInstance != null)
        {
            Undo.RegisterCreatedObjectUndo(prefabInstance, "Create Vo Dai Xen Bo Hung miniboss");
            return prefabInstance;
        }
#endif

        return Instantiate(templateObject);
    }

    private static Collider2D FindArenaGround(Collider2D fallbackGround = null)
    {
        Collider2D[] colliders = FindObjectsByType<Collider2D>(FindObjectsInactive.Exclude);
        Collider2D topGround = null;
        float topGroundY = float.NegativeInfinity;

        foreach (Collider2D candidate in colliders)
        {
            if (!candidate.name.Contains("Ground"))
            {
                continue;
            }

            Bounds bounds = candidate.bounds;
            if (bounds.size.x <= bounds.size.y || bounds.max.y <= topGroundY)
            {
                continue;
            }

            topGround = candidate;
            topGroundY = bounds.max.y;
        }

        return topGround != null ? topGround : fallbackGround;
    }
}
