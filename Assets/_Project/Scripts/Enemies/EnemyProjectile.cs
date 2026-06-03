using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    private int damage;
    private float lifetime;
    private float age;

    public void Initialize(
        Vector2 direction,
        float speed,
        int projectileDamage,
        float projectileLifetime,
        Sprite projectileSprite,
        float projectileScale,
        bool projectileFacesRightByDefault
    )
    {
        damage = projectileDamage;
        lifetime = projectileLifetime;
        transform.localScale = Vector3.one * projectileScale;

        Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.linearVelocity = direction.normalized * speed;

        CircleCollider2D collider = gameObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.12f;

        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        bool hasCustomSprite = projectileSprite != null;
        renderer.sprite = hasCustomSprite ? projectileSprite : CreateDefaultSprite();
        renderer.color = hasCustomSprite ? Color.white : new Color(1f, 0.8f, 0.05f);
        renderer.flipX = ShouldFlipSprite(direction.x, projectileFacesRightByDefault);
        renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        renderer.sortingOrder = 50;
    }

    private void Update()
    {
        age += Time.deltaTime;

        if (age >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.TakeDamage(damage);
        Destroy(gameObject);
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

    private static bool ShouldFlipSprite(float directionX, bool facesRightByDefault)
    {
        if (Mathf.Abs(directionX) <= 0.01f)
        {
            return false;
        }

        bool movingRight = directionX > 0f;
        return facesRightByDefault ? !movingRight : movingRight;
    }
}
