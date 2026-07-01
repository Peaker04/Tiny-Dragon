using TinyDragon.Combat;
using TinyDragon.Data;
using TinyDragon.Config;
using TinyDragon.Shared.Unity;
using UnityEngine;

/// <summary>
/// Manages player projectile spawning and pooling.
/// Normal shot and power shot are configured via ProjectileSpec structs,
/// replacing the previous long parameter lists.
/// </summary>
public class ProjectileShooter : MonoBehaviour
{
    [SerializeField] private TinyDragonRuntimeConfig runtimeConfig;
    [SerializeField] private PlayerProjectile projectilePrefab;
    [SerializeField] private int projectilePoolPrewarmCount = 4;

    [Header("Normal Shot")]
    [SerializeField] private ProjectileSpec normalShot = new ProjectileSpec
    {
        damage = GameplayBalanceDefaults.PlayerBaseAttack,
        speed = 8f,
        lifetime = 2f,
        scale = 1.2f,
        spawnOffset = new Vector2(0.6f, 0.15f),
        facesRightByDefault = true,
        sortingLayerName = "Default",
        sortingOrder = 100
    };

    [Header("Power Shot")]
    [SerializeField] private ProjectileSpec powerShot = new ProjectileSpec
    {
        damage = GameplayBalanceDefaults.PlayerPowerShotDamage,
        speed = 6f,
        lifetime = 3f,
        scale = 1f,
        spawnOffset = new Vector2(0.8f, 0.2f),
        facesRightByDefault = true,
        sortingLayerName = "Default",
        sortingOrder = 100
    };

    private ComponentPool<PlayerProjectile> projectilePool;
    private bool suppressNextShot;
    private float suppressNextShotUntil;
    private TinyDragonRuntimeConfig Config => TinyDragonRuntimeConfigProvider.Resolve(runtimeConfig);

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

        ShootProjectile(normalShot, true);
    }

    public void ShootPower()
    {
        Sprite resolvedSprite = powerShot.sprite != null ? powerShot.sprite : normalShot.sprite;
        ProjectileSpec resolved = powerShot;
        resolved.sprite = resolvedSprite;
        ShootProjectile(resolved, false);
    }

    public void SuppressNextShot(float duration)
    {
        suppressNextShot = true;
        suppressNextShotUntil = Time.time + Mathf.Max(0f, duration);
    }

    public void ApplyProjectileDamage(int damage)
    {
        normalShot.damage = Mathf.Max(damage, 1);
    }

    public void ApplyPowerShotDamage(int damage)
    {
        powerShot.damage = Mathf.Max(damage, 1);
    }

    private void ShootProjectile(ProjectileSpec spec, bool warnIfSpriteMissing)
    {
        if (spec.sprite == null && warnIfSpriteMissing)
        {
            Debug.LogWarning("Player projectile sprite is missing. Assign a sprite to ProjectileShooter.", this);
        }

        float facingDirection = transform.localScale.x >= 0f ? 1f : -1f;
        Vector3 scaledSpawnOffset = new Vector3(spec.spawnOffset.x * facingDirection, spec.spawnOffset.y, 0f);
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
            spec.speed,
            spec.damage,
            spec.lifetime,
            spec.sprite,
            spec.scale,
            spec.facesRightByDefault,
            spec.sortingLayerName,
            spec.sortingOrder,
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
            GameObject projectilePrefabObject = ResourceLoader.Load<GameObject>(Config.Resources.playerProjectilePrefabPath);
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
