using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TinyDragon.Config;
using TinyDragon.Shared.Gameplay;
using TinyDragon.Shared.Unity;

public class EnemySpawner : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private TinyDragonRuntimeConfig runtimeConfig;

    [Header("Normal Enemies")]
    [SerializeField] private EnemyPatrol enemyTemplate;
    [SerializeField] private Collider2D groundCollider;
    [SerializeField] private int maxEnemiesOnGround = 3;
    [SerializeField] private int totalEnemiesBeforeBoss = 3;
    [SerializeField] private bool respawnKilledEnemies = true;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float spawnXPadding = 1f;
    [SerializeField] private float spawnYOffset = 0.02f;
    [SerializeField] private UnityEngine.Camera spawnCamera;
    [SerializeField] private Transform enemyParent;
    [SerializeField] private Transform[] fixedSpawnPoints;
    [Header("Boss Spawn")]
    [SerializeField] private bool spawnBossAfterNormalEnemies;
    [SerializeField] private BossAI bossTemplate;
    [SerializeField] private Transform bossSpawnPoint;
    [SerializeField] private Transform bossParent;
    [SerializeField] private float bossSpawnYOffset = 0.02f;

    private readonly List<EnemyPatrol> activeEnemies = new List<EnemyPatrol>();
    private float nextSpawnTime;
    private bool hideSceneTemplate;
    private bool hideBossTemplate;
    private int spawnedEnemyCount;
    private int defeatedEnemyCount;
    private bool bossSpawned;
    private int nextFixedSpawnPointIndex;
    private TinyDragonRuntimeConfig Config => TinyDragonRuntimeConfigProvider.Resolve(runtimeConfig);

    private void OnValidate()
    {
        totalEnemiesBeforeBoss = Mathf.Max(totalEnemiesBeforeBoss, 0);
        maxEnemiesOnGround = Mathf.Max(maxEnemiesOnGround, 0);
    }

    private void Awake()
    {
        ApplySceneDefaults();

        if (enemyTemplate == null)
        {
            enemyTemplate = ObjectLookup.Any<EnemyPatrol>();
        }

        hideSceneTemplate = enemyTemplate != null && enemyTemplate.gameObject.scene.IsValid();
        if (hideSceneTemplate)
        {
            enemyTemplate.gameObject.SetActive(false);
        }

        HideFixedSpawnTemplates();

        if (bossTemplate == null)
        {
            bossTemplate = ObjectLookup.Any<BossAI>();
        }

        hideBossTemplate = bossTemplate != null && bossTemplate.gameObject.scene.IsValid();
        if (hideBossTemplate)
        {
            bossTemplate.gameObject.SetActive(false);
        }

        if (groundCollider == null)
        {
            groundCollider = FindGroundCollider();
        }

        if (spawnCamera == null)
        {
            spawnCamera = UnityEngine.Camera.main != null ? UnityEngine.Camera.main : ObjectLookup.Any<UnityEngine.Camera>();
        }

        if (enemyParent == null)
        {
            enemyParent = RuntimeSceneRoot.GetChild("Enemies");
        }
    }

    private void Start()
    {
        if (hideSceneTemplate)
        {
            enemyTemplate.gameObject.SetActive(false);
        }

        if (hideBossTemplate)
        {
            bossTemplate.gameObject.SetActive(false);
        }

        FillEnemySlots();
    }

    private void ApplySceneDefaults()
    {
        TinyDragonRuntimeConfig config = Config;
        if (SceneManager.GetActiveScene().name != config.Scenes.level02SceneName)
        {
            return;
        }

        maxEnemiesOnGround = config.Gameplay.level02MaxEnemiesOnGround;
        respawnKilledEnemies = config.Gameplay.level02RespawnKilledEnemies;
        totalEnemiesBeforeBoss = config.Gameplay.level02TotalEnemiesBeforeBoss;
        spawnBossAfterNormalEnemies = config.Gameplay.level02SpawnBossAfterNormalEnemies;
    }

    private void Update()
    {
        RemoveMissingEnemies();

        if (spawnBossAfterNormalEnemies)
        {
            TrySpawnBoss();
        }

        if (!respawnKilledEnemies)
        {
            return;
        }

        if (Time.time < nextSpawnTime || activeEnemies.Count >= maxEnemiesOnGround)
        {
            return;
        }

        if (!CanSpawnMoreNormalEnemies())
        {
            return;
        }

        SpawnEnemy();
        nextSpawnTime = Time.time + spawnInterval;
    }

    private void FillEnemySlots()
    {
        RemoveMissingEnemies();

        while (activeEnemies.Count < maxEnemiesOnGround && CanSpawnMoreNormalEnemies())
        {
            if (!SpawnEnemy())
            {
                return;
            }
        }

        nextSpawnTime = Time.time + spawnInterval;
    }

    private bool SpawnEnemy()
    {
        if (enemyTemplate == null || groundCollider == null || maxEnemiesOnGround <= 0 || !CanSpawnMoreNormalEnemies())
        {
            return false;
        }

        Vector3 spawnPosition = GetRandomGroundPosition();
        EnemyPatrol enemy = Instantiate(enemyTemplate, spawnPosition, enemyTemplate.transform.rotation, enemyParent);
        enemy.name = enemyTemplate.name;
        enemy.gameObject.SetActive(true);
        PlaceEnemyOnGround(enemy);
        ConstrainEnemyToGround(enemy);

        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
        if (enemyHealth == null)
        {
            Debug.LogWarning("Spawned enemy is missing EnemyHealth. Add EnemyHealth to the enemy template.", enemy);
            activeEnemies.Add(enemy);
            return true;
        }

        enemyHealth.ResetHealth();
        activeEnemies.Add(enemy);
        spawnedEnemyCount++;
        return true;
    }

    private bool CanSpawnMoreNormalEnemies()
    {
        return !spawnBossAfterNormalEnemies || spawnedEnemyCount < totalEnemiesBeforeBoss;
    }

    private void TrySpawnBoss()
    {
        if (bossSpawned || bossTemplate == null)
        {
            return;
        }

        if (spawnedEnemyCount < totalEnemiesBeforeBoss || defeatedEnemyCount < totalEnemiesBeforeBoss || activeEnemies.Count > 0)
        {
            return;
        }

        SpawnBoss();
    }

    private void SpawnBoss()
    {
        Vector3 spawnPosition = GetBossSpawnPosition();
        Transform parent = bossParent != null ? bossParent : enemyParent;

        BossAI boss = hideBossTemplate
            ? bossTemplate
            : Instantiate(bossTemplate, spawnPosition, bossTemplate.transform.rotation, parent);

        boss.name = bossTemplate.name;
        if (parent != null)
        {
            boss.transform.SetParent(parent, true);
        }

        boss.transform.position = spawnPosition;
        boss.transform.rotation = bossTemplate.transform.rotation;
        boss.gameObject.SetActive(true);
        PlaceBossOnGround(boss);

        EnemyHealth bossHealth = boss.GetComponent<EnemyHealth>();
        if (bossHealth != null)
        {
            bossHealth.ResetHealth();
        }

        bossSpawned = true;
    }

    private Vector3 GetBossSpawnPosition()
    {
        if (bossSpawnPoint != null)
        {
            return bossSpawnPoint.position;
        }

        if (groundCollider == null)
        {
            return bossTemplate.transform.position;
        }

        Bounds groundBounds = groundCollider.bounds;
        float spawnX = spawnCamera != null
            ? SpawnGeometry2D.GetCameraBounds(spawnCamera, groundBounds).max.x - spawnXPadding
            : groundBounds.max.x - spawnXPadding;

        spawnX = Mathf.Clamp(spawnX, groundBounds.min.x + spawnXPadding, groundBounds.max.x - spawnXPadding);
        return new Vector3(spawnX, groundBounds.max.y + bossSpawnYOffset, bossTemplate.transform.position.z);
    }

    private Vector3 GetRandomGroundPosition()
    {
        Transform fixedSpawnPoint = GetNextFixedSpawnPoint();
        if (fixedSpawnPoint != null)
        {
            Bounds fixedSpawnGroundBounds = groundCollider.bounds;
            float spawnX = Mathf.Clamp(
                fixedSpawnPoint.position.x,
                fixedSpawnGroundBounds.min.x + spawnXPadding,
                fixedSpawnGroundBounds.max.x - spawnXPadding
            );

            return new Vector3(
                spawnX,
                fixedSpawnPoint.position.y,
                enemyTemplate.transform.position.z
            );
        }

        Bounds groundBounds = groundCollider.bounds;
        SpawnGeometry2D.GetSpawnXBounds(groundBounds, spawnCamera, spawnXPadding, out float minX, out float maxX);

        return new Vector3(
            Random.Range(minX, maxX),
            groundBounds.max.y + spawnYOffset,
            enemyTemplate.transform.position.z
        );
    }

    private Transform GetNextFixedSpawnPoint()
    {
        if (fixedSpawnPoints == null || fixedSpawnPoints.Length == 0)
        {
            return null;
        }

        for (int attempt = 0; attempt < fixedSpawnPoints.Length; attempt++)
        {
            int index = nextFixedSpawnPointIndex % fixedSpawnPoints.Length;
            nextFixedSpawnPointIndex++;

            if (fixedSpawnPoints[index] != null)
            {
                return fixedSpawnPoints[index];
            }
        }

        return null;
    }

    private void HideFixedSpawnTemplates()
    {
        if (fixedSpawnPoints == null)
        {
            return;
        }

        foreach (Transform spawnPoint in fixedSpawnPoints)
        {
            if (spawnPoint == null || (enemyTemplate != null && spawnPoint == enemyTemplate.transform))
            {
                continue;
            }

            if (spawnPoint.TryGetComponent(out EnemyPatrol sceneEnemy)
                && sceneEnemy.gameObject.scene.IsValid())
            {
                sceneEnemy.gameObject.SetActive(false);
            }
        }
    }

    private void PlaceEnemyOnGround(EnemyPatrol enemy)
    {
        Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
        if (enemyCollider == null)
        {
            return;
        }

        Vector3 position = enemy.transform.position;
        float bottomOffset = position.y - enemyCollider.bounds.min.y;
        position.y = groundCollider.bounds.max.y + bottomOffset + spawnYOffset;
        enemy.transform.position = position;
    }

    private void ConstrainEnemyToGround(EnemyPatrol enemy)
    {
        if (groundCollider == null)
        {
            return;
        }

        Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
        float halfEnemyWidth = enemyCollider != null ? enemyCollider.bounds.extents.x : 0f;
        Bounds groundBounds = groundCollider.bounds;
        enemy.SetPatrolBounds(
            groundBounds.min.x + halfEnemyWidth,
            groundBounds.max.x - halfEnemyWidth
        );
    }

    private void PlaceBossOnGround(BossAI boss)
    {
        if (groundCollider == null)
        {
            return;
        }

        Collider2D bossCollider = boss.GetComponent<Collider2D>();
        if (bossCollider == null)
        {
            return;
        }

        Vector3 position = boss.transform.position;
        float bottomOffset = position.y - bossCollider.bounds.min.y;
        position.y = groundCollider.bounds.max.y + bottomOffset + bossSpawnYOffset;
        boss.transform.position = position;
    }

    private void RemoveMissingEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] == null)
            {
                activeEnemies.RemoveAt(i);
                defeatedEnemyCount++;
            }
        }
    }

    private Collider2D FindGroundCollider()
    {
        GameObject groundObject = ObjectLookup.SceneObject("Ground_Main_Collider");
        if (groundObject != null && groundObject.TryGetComponent(out Collider2D foundGroundCollider))
        {
            return foundGroundCollider;
        }

        int groundLayer = LayerMask.NameToLayer("Ground");
        Collider2D[] colliders = FindObjectsByType<Collider2D>(FindObjectsInactive.Exclude);
        foreach (Collider2D collider in colliders)
        {
            if (collider.gameObject.layer == groundLayer || collider.name.Contains("Ground"))
            {
                return collider;
            }
        }

        return null;
    }
}
