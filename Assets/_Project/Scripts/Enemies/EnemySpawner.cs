using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private EnemyPatrol enemyTemplate;
    [SerializeField] private Collider2D groundCollider;
    [SerializeField] private int maxEnemiesOnGround = 3;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float spawnXPadding = 1f;
    [SerializeField] private float spawnYOffset = 0.02f;
    [SerializeField] private Camera spawnCamera;
    [SerializeField] private Transform enemyParent;

    private readonly List<EnemyPatrol> activeEnemies = new List<EnemyPatrol>();
    private float nextSpawnTime;
    private bool hideSceneTemplate;

    private void Awake()
    {
        if (enemyTemplate == null)
        {
            enemyTemplate = FindAnyObjectByType<EnemyPatrol>();
        }

        hideSceneTemplate = enemyTemplate != null && enemyTemplate.gameObject.scene.IsValid();

        if (groundCollider == null)
        {
            groundCollider = FindGroundCollider();
        }

        if (spawnCamera == null)
        {
            spawnCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        }
    }

    private void Start()
    {
        if (hideSceneTemplate)
        {
            enemyTemplate.gameObject.SetActive(false);
        }

        FillEnemySlots();
    }

    private void Update()
    {
        RemoveMissingEnemies();

        if (Time.time < nextSpawnTime || activeEnemies.Count >= maxEnemiesOnGround)
        {
            return;
        }

        SpawnEnemy();
        nextSpawnTime = Time.time + spawnInterval;
    }

    private void FillEnemySlots()
    {
        RemoveMissingEnemies();

        while (activeEnemies.Count < maxEnemiesOnGround)
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
        if (enemyTemplate == null || groundCollider == null || maxEnemiesOnGround <= 0)
        {
            return false;
        }

        Vector3 spawnPosition = GetRandomGroundPosition();
        EnemyPatrol enemy = Instantiate(enemyTemplate, spawnPosition, enemyTemplate.transform.rotation, enemyParent);
        enemy.name = enemyTemplate.name;
        enemy.gameObject.SetActive(true);
        PlaceEnemyOnGround(enemy);

        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
        if (enemyHealth == null)
        {
            enemyHealth = enemy.gameObject.AddComponent<EnemyHealth>();
        }

        enemyHealth.ResetHealth();
        activeEnemies.Add(enemy);
        return true;
    }

    private Vector3 GetRandomGroundPosition()
    {
        Bounds groundBounds = groundCollider.bounds;
        GetSpawnXBounds(groundBounds, out float minX, out float maxX);

        return new Vector3(
            Random.Range(minX, maxX),
            groundBounds.max.y + spawnYOffset,
            enemyTemplate.transform.position.z
        );
    }

    private void GetSpawnXBounds(Bounds groundBounds, out float minX, out float maxX)
    {
        minX = groundBounds.min.x + spawnXPadding;
        maxX = groundBounds.max.x - spawnXPadding;

        if (spawnCamera != null)
        {
            Bounds cameraBounds = GetCameraBounds(spawnCamera);
            minX = Mathf.Max(minX, cameraBounds.min.x + spawnXPadding);
            maxX = Mathf.Min(maxX, cameraBounds.max.x - spawnXPadding);
        }

        if (minX <= maxX)
        {
            return;
        }

        minX = groundBounds.min.x;
        maxX = groundBounds.max.x;
    }

    private Bounds GetCameraBounds(Camera camera)
    {
        if (camera.orthographic)
        {
            float height = camera.orthographicSize * 2f;
            float width = height * camera.aspect;
            return new Bounds(camera.transform.position, new Vector3(width, height, 0f));
        }

        float depth = Mathf.Abs(camera.transform.position.z - groundCollider.bounds.center.z);
        Vector3 bottomLeft = camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        Vector3 topRight = camera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
        Bounds bounds = new Bounds();
        bounds.SetMinMax(bottomLeft, topRight);
        return bounds;
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

    private void RemoveMissingEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] == null)
            {
                activeEnemies.RemoveAt(i);
            }
        }
    }

    private Collider2D FindGroundCollider()
    {
        GameObject groundObject = GameObject.Find("Ground_Main_Collider");
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
