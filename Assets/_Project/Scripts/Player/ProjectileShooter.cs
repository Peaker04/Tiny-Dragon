using TinyDragon.Data;
using UnityEngine;

public class ProjectileShooter : MonoBehaviour
{
    private const string DefaultProjectilePrefabPath = "Combat/PlayerProjectile";

    [SerializeField] private PlayerProjectile projectilePrefab;
    [SerializeField] private int projectilePoolPrewarmCount = 4;
    [SerializeField] private int projectileDamage = GameplayBalanceDefaults.PlayerBaseAttack;
    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private float projectileLifetime = 2f;
    [SerializeField] private float projectileScale = 1.2f;
    [SerializeField] private Vector2 projectileSpawnOffset = new Vector2(0.6f, 0.15f);
    [SerializeField] private Sprite projectileSprite;
    [SerializeField] private bool projectileFacesRightByDefault = true;
    [SerializeField] private string projectileSortingLayerName = "Default";
    [SerializeField] private int projectileSortingOrder = 100;
    [Header("Power Shot")]
    [SerializeField] private int powerShotDamage = GameplayBalanceDefaults.PlayerPowerShotDamage;
    [SerializeField] private float powerShotSpeed = 6f;
    [SerializeField] private float powerShotLifetime = 3f;
    [SerializeField] private float powerShotScale = 1f;
    [SerializeField] private Vector2 powerShotSpawnOffset = new Vector2(0.8f, 0.2f);
    [SerializeField] private Sprite powerShotSprite;
    [SerializeField] private bool powerShotFacesRightByDefault = true;
    [SerializeField] private string powerShotSortingLayerName = "Default";
    [SerializeField] private int powerShotSortingOrder = 100;

    private ComponentPool<PlayerProjectile> projectilePool;
    private bool suppressNextShot;
    private float suppressNextShotUntil;

    private void Awake()
    {
        EnsurePool();
    }

    public void Shoot()
    {
        if (ShouldSuppressShot())
        {
            return;
        }

        ShootProjectile(
            projectileDamage,
            projectileSpeed,
            projectileLifetime,
            projectileScale,
            projectileSpawnOffset,
            projectileSprite,
            projectileFacesRightByDefault,
            projectileSortingLayerName,
            projectileSortingOrder,
            true
        );
    }

    public void ShootPower()
    {
        ShootProjectile(
            powerShotDamage,
            powerShotSpeed,
            powerShotLifetime,
            powerShotScale,
            powerShotSpawnOffset,
            powerShotSprite != null ? powerShotSprite : projectileSprite,
            powerShotFacesRightByDefault,
            powerShotSortingLayerName,
            powerShotSortingOrder,
            false
        );
    }

    public void SuppressNextShot(float duration)
    {
        suppressNextShot = true;
        suppressNextShotUntil = Time.time + Mathf.Max(0f, duration);
    }

    public void ApplyProjectileDamage(int damage)
    {
        projectileDamage = Mathf.Max(damage, 1);
    }

    public void ApplyPowerShotDamage(int damage)
    {
        powerShotDamage = Mathf.Max(damage, 1);
    }

    private void ShootProjectile(
        int damage,
        float speed,
        float lifetime,
        float scale,
        Vector2 spawnOffset,
        Sprite sprite,
        bool facesRightByDefault,
        string sortingLayerName,
        int sortingOrder,
        bool warnIfSpriteMissing
    )
    {
        if (sprite == null && warnIfSpriteMissing)
        {
            Debug.LogWarning("Player projectile sprite is missing. Assign a sprite to ProjectileShooter.", this);
        }

        float facingDirection = transform.localScale.x >= 0f ? 1f : -1f;
        Vector3 scaledSpawnOffset = new Vector3(spawnOffset.x * facingDirection, spawnOffset.y, 0f);
        Vector3 spawnPosition = transform.position + scaledSpawnOffset;
        Vector2 projectileDirection = new Vector2(facingDirection, 0f);

        EnsurePool();
        if (projectilePool == null)
        {
            Debug.LogWarning("Player projectile prefab is missing. Assign Resources/Combat/PlayerProjectile to ProjectileShooter.", this);
            return;
        }

        PlayerProjectile projectile = projectilePool.Get(spawnPosition, Quaternion.identity);
        projectile.Initialize(
            projectileDirection,
            speed,
            damage,
            lifetime,
            sprite,
            scale,
            facesRightByDefault,
            sortingLayerName,
            sortingOrder,
            projectilePool.Release
        );
    }

    private bool ShouldSuppressShot()
    {
        if (!suppressNextShot)
        {
            return false;
        }

        suppressNextShot = false;
        return Time.time <= suppressNextShotUntil;
    }

    private void EnsurePool()
    {
        if (projectilePool != null)
        {
            return;
        }

        if (projectilePrefab == null)
        {
            GameObject projectilePrefabObject = Resources.Load<GameObject>(DefaultProjectilePrefabPath);
            if (projectilePrefabObject != null)
            {
                projectilePrefab = projectilePrefabObject.GetComponent<PlayerProjectile>();
            }
        }

        if (projectilePrefab != null)
        {
            projectilePool = new ComponentPool<PlayerProjectile>(
                projectilePrefab,
                RuntimeSceneRoot.GetChild("ProjectilePool"),
                projectilePoolPrewarmCount
            );
        }
    }
}
