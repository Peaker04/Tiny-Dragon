using TinyDragon.Data;
using TinyDragon.Config;
using TinyDragon.Shared.Unity;
using UnityEngine;

/// <summary>
/// Manages enemy projectile spawning, pooling, and firing.
/// Extracted from EnemyPatrol so patrol/state logic doesn't
/// manage projectile internals directly.
/// </summary>
public class EnemyProjectileShooter : MonoBehaviour
{
    [SerializeField] private TinyDragonRuntimeConfig runtimeConfig;
    [SerializeField] private EnemyProjectile projectilePrefab;
    [SerializeField] private int projectilePoolPrewarmCount = 2;
    [SerializeField] private float projectileSpeed = 5f;
    [SerializeField] private float projectileLifetime = 3f;
    [SerializeField] private float projectileScale = 0.35f;
    [SerializeField] private Vector2 projectileSpawnOffset = new Vector2(0.45f, -0.05f);
    [SerializeField] private Sprite projectileSprite;
    [SerializeField] private bool projectileFacesRightByDefault = true;

    private ComponentPool<EnemyProjectile> projectilePool;
    private TinyDragonRuntimeConfig Config => TinyDragonRuntimeConfigProvider.Resolve(runtimeConfig);

    private void Awake()
    {
        EnsureProjectilePool();
    }

    /// <summary>
    /// Fires a projectile toward the given target position.
    /// </summary>
    public void ShootAt(Vector3 targetPosition, int damage)
    {
        float facingDirection = targetPosition.x >= transform.position.x ? 1f : -1f;
        Vector3 spawnOffset = new Vector3(projectileSpawnOffset.x * facingDirection, projectileSpawnOffset.y, 0f);
        Vector3 spawnPosition = transform.position + spawnOffset;
        Vector2 projectileDirection = new Vector2(facingDirection, 0f);

        EnsureProjectilePool();
        if (projectilePool == null)
        {
            Debug.LogWarning("Enemy projectile prefab is missing. Assign Resources/Combat/EnemyProjectile to EnemyProjectileShooter.", this);
            return;
        }

        EnemyProjectile projectile = projectilePool.Get(spawnPosition, Quaternion.identity);
        projectile.Initialize(
            projectileDirection,
            projectileSpeed,
            damage,
            projectileLifetime,
            projectileSprite,
            projectileScale,
            projectileFacesRightByDefault,
            projectilePool.Release
        );
    }

    private void EnsureProjectilePool()
    {
        if (projectilePool != null)
        {
            return;
        }

        if (projectilePrefab == null)
        {
            GameObject projectilePrefabObject = ResourceLoader.Load<GameObject>(Config.Resources.enemyProjectilePrefabPath);
            if (projectilePrefabObject != null)
            {
                projectilePrefab = projectilePrefabObject.GetComponent<EnemyProjectile>();
            }
        }

        if (projectilePrefab != null)
        {
            projectilePool = new ComponentPool<EnemyProjectile>(
                projectilePrefab,
                RuntimeSceneRoot.GetChild("ProjectilePool"),
                projectilePoolPrewarmCount
            );
        }
    }
}
