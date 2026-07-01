using TinyDragon.Combat;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerProjectile : MonoBehaviour
{
    private static Material spriteDefaultMaterial;

    private int damage;
    private PlayerDamageSource damageSource = PlayerDamageSource.Generic;
    private float lifetime;
    private float age;
    private Rigidbody2D rb;
    private CircleCollider2D projectileCollider;
    private SpriteRenderer spriteRenderer;
    private System.Action<PlayerProjectile> releaseToPool;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        projectileCollider = GetComponent<CircleCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        projectileCollider.isTrigger = true;
        projectileCollider.radius = 0.16f;
    }

    public void Initialize(
        Vector2 direction,
        float speed,
        int projectileDamage,
        PlayerDamageSource projectileDamageSource,
        float projectileLifetime,
        Sprite projectileSprite,
        float projectileScale,
        bool projectileFacesRightByDefault,
        string projectileSortingLayerName,
        int projectileSortingOrder,
        System.Action<PlayerProjectile> releaseHandler = null
    )
    {
        damage = projectileDamage;
        damageSource = projectileDamageSource;
        lifetime = projectileLifetime;
        age = 0f;
        releaseToPool = releaseHandler;
        transform.localScale = Vector3.one * projectileScale;

        rb.linearVelocity = direction.normalized * speed;

        bool hasCustomSprite = projectileSprite != null;
        spriteRenderer.sprite = hasCustomSprite ? projectileSprite : CreateDefaultSprite();
        spriteRenderer.color = hasCustomSprite ? Color.white : new Color(1f, 0.2f, 0.8f);
        spriteRenderer.flipX = ShouldFlipSprite(direction.x, projectileFacesRightByDefault);
        SetUnlitSpriteMaterial(spriteRenderer);
        spriteRenderer.sortingLayerName = string.IsNullOrWhiteSpace(projectileSortingLayerName)
            ? "Default"
            : projectileSortingLayerName;
        spriteRenderer.sortingOrder = projectileSortingOrder;
    }

    private void Update()
    {
        age += Time.deltaTime;

        if (age >= lifetime)
        {
            Release();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        EnemyHealth enemyHealth = other.GetComponentInParent<EnemyHealth>();
        if (enemyHealth == null)
        {
            EnemyPatrol enemy = other.GetComponentInParent<EnemyPatrol>();
            if (enemy == null)
            {
                return;
            }

            enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth == null)
            {
                Debug.LogWarning("Projectile hit an enemy without EnemyHealth. Add EnemyHealth to the enemy prefab/template.", enemy);
                Release();
                return;
            }
        }

        Debug.Log($"Player projectile hit enemy for {damage} damage.");
        enemyHealth.TakeDamage(damage, damageSource);
        BossAI hitBoss = enemyHealth.GetComponent<BossAI>();
        if (hitBoss != null)
        {
            hitBoss.PlayHit();
        }

        Release();
    }

    private void Release()
    {
        rb.linearVelocity = Vector2.zero;

        if (releaseToPool != null)
        {
            releaseToPool(this);
            return;
        }

        Destroy(gameObject);
    }

    private static bool ShouldFlipSprite(float directionX, bool facesRightByDefault)
    {
        if (Mathf.Abs(directionX) <= 0.01f)
        {
            return false;
        }

        bool movingRight = directionX > 0f;
        return facesRightByDefault ? movingRight : !movingRight;
    }

    private static Sprite CreateDefaultSprite()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size);
        texture.filterMode = FilterMode.Point;

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size * 0.4f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = distance <= radius ? 1f : 0f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static void SetUnlitSpriteMaterial(SpriteRenderer renderer)
    {
        if (spriteDefaultMaterial == null)
        {
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader == null)
            {
                return;
            }

            spriteDefaultMaterial = new Material(spriteShader);
        }

        renderer.sharedMaterial = spriteDefaultMaterial;
    }
}
