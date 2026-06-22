using UnityEngine;

// Lớp CHA chứa logic chung cho mọi loại đạn.
// Khi tạo loại đạn mới, chỉ cần: public class TênĐạn : BaseProjectile
// và ghi đè (override) những phần khác nếu cần.
public class BaseProjectile : MonoBehaviour
{
    private static Material spriteDefaultMaterial;
    private static Sprite fallbackSprite;

    // --- Struct gom dữ liệu khởi tạo đạn ---
    // Dùng struct để gọn hơn thay vì truyền 9 tham số riêng lẻ vào hàm.
    public struct ProjectileData
    {
        public Vector2 direction;    // Hướng bay của đạn
        public float speed;          // Tốc độ bay
        public int damage;           // Sát thương gây ra
        public float lifetime;       // Thời gian tồn tại (giây) trước khi tự hủy
        public Sprite sprite;        // Hình ảnh của đạn (kéo thả từ Inspector)
        public float scale;          // Kích thước của đạn
        public bool facesRight;      // Sprite gốc của đạn nhìn sang phải? (để flip đúng chiều)
        public string sortingLayer;  // Tên Sorting Layer (quyết định vẽ trên/dưới object khác)
        public int sortingOrder;     // Thứ tự vẽ trong layer (số lớn = vẽ trên cùng)
    }

    // --- Trạng thái nội bộ ---
    private int damage;
    private float lifetime;
    private float age; // Đã sống được bao nhiêu giây rồi

    // Hàm khởi tạo đạn - gọi ngay sau khi tạo GameObject đạn.
    public void Initialize(ProjectileData data)
    {
        damage = data.damage;
        lifetime = data.lifetime;
        transform.localScale = Vector3.one * data.scale;

        SetupPhysics(data.direction, data.speed);
        SetupVisual(data.sprite, data.direction.x, data.facesRight, data.sortingLayer, data.sortingOrder);
    }

    // Thêm Rigidbody2D và Collider để đạn có thể di chuyển và va chạm.
    private void SetupPhysics(Vector2 direction, float speed)
    {
        Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;               // Đạn không bị trọng lực kéo xuống
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.linearVelocity = direction.normalized * speed;

        CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true; // Trigger = phát hiện va chạm nhưng không đẩy vật lý
        col.radius = 0.16f;
    }

    // Thêm SpriteRenderer để vẽ hình ảnh đạn lên màn hình.
    private void SetupVisual(Sprite sprite, float directionX, bool facesRight, string sortingLayer, int sortingOrder)
    {
        SpriteRenderer sr = gameObject.AddComponent<SpriteRenderer>();
        bool hasCustomSprite = sprite != null;
        sr.sprite = hasCustomSprite ? sprite : CreateFallbackSprite();
        sr.color = hasCustomSprite ? Color.white : new Color(1f, 0.2f, 0.8f);
        sr.flipX = ShouldFlipX(directionX, facesRight);
        sr.sortingLayerName = string.IsNullOrWhiteSpace(sortingLayer) ? "Default" : sortingLayer;
        sr.sortingOrder = sortingOrder;
        SetUnlitSpriteMaterial(sr);
    }

    private void Update()
    {
        // Đếm thời gian sống. Khi vượt lifetime thì tự hủy.
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Tìm EnemyHealth trong object va chạm hoặc bất kỳ cha nào của nó.
        EnemyHealth enemyHealth = other.GetComponentInParent<EnemyHealth>();
        if (enemyHealth == null)
        {
            return; // Không phải kẻ thù → bỏ qua
        }

        enemyHealth.TakeDamage(damage);
        Destroy(gameObject);
    }

    // Tính xem có cần lật hình ảnh theo chiều ngang không.
    private bool ShouldFlipX(float directionX, bool facesRight)
    {
        if (Mathf.Abs(directionX) <= 0.01f) return false;
        bool movingRight = directionX > 0f;
        return facesRight ? !movingRight : movingRight;
    }

    private static void SetUnlitSpriteMaterial(SpriteRenderer renderer)
    {
        if (spriteDefaultMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
                spriteDefaultMaterial = new Material(shader);
        }

        if (spriteDefaultMaterial != null)
            renderer.sharedMaterial = spriteDefaultMaterial;
    }

    private static Sprite CreateFallbackSprite()
    {
        if (fallbackSprite != null)
            return fallbackSprite;

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
        fallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return fallbackSprite;
    }
}
