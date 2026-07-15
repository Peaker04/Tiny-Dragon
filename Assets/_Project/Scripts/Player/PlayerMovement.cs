using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 15f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;

    private Rigidbody2D rb;

    // [Bug#2-hotfix] knockbackTimer: khóa movement trong thời gian ngắn sau knockback
    // - Tránh FixedUpdate() ghi đè vận tốc x ngay sau khi AddForce(Impulse)
    // - knockbackDuration = 0.15s: đủ để physics rendering thấy được cú đẩy lùi
    // - Trong khoảng này FixedUpdate() không set rb.linearVelocity.x, giữ nguyên impulse từ knockback
    private float knockbackEndTime;
    private const float KnockbackDuration = 0.08f;

    public bool IsGrounded { get; private set; }
    public float HorizontalInput => inputReader != null ? inputReader.Horizontal : 0f;
    public float FacingDirection => transform.localScale.x >= 0f ? 1f : -1f;

    /// <summary>Fired the moment the player leaves the ground via a jump.</summary>
    public event System.Action JumpPerformed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (inputReader == null)
        {
            inputReader = GetComponent<PlayerInputReader>();
        }
    }

    private void FixedUpdate()
    {
        IsGrounded = groundCheck != null
            && Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // [Bug#2-hotfix] Kiểm tra knockbackTimer: nếu đang knockback thì KHÔNG ghi đè rb.linearVelocity.x
        // - knockbackEndTime: thời điểm kết thúc knockback (Time.time + 0.15s)
        // - Khi knockback: player giữ nguyên impulse, không bị FixedUpdate() reset vận tốc ngang
        // - Khi knockback hết: hoạt động bình thường, di chuyển theo input
        if (Time.time >= knockbackEndTime)
        {
            float horizontalInput = HorizontalInput;
            rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);
            Flip(horizontalInput);
        }

        if (inputReader != null && inputReader.ConsumeJumpPressed() && IsGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            JumpPerformed?.Invoke();
        }
    }

    public void Stop()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    // [Bug#2-hotfix] Knockback: đẩy player theo hướng direction với lực force (dạng velocity)
    // - Được gọi từ PlayerHealth.TakeDamage() sau khi nhận damage
    // - Dùng gán velocity TRỰC TIẾP thay vì AddForce(Impulse) vì:
    //     AddForce bị physics engine batching làm chậm 1 physics step, không đẩy ngay được
    //     Set velocity: thay đổi vận tốc ngay lập tức, FixedUpdate() kế tôn trọng knockback timer
    // - force đã bao gồm hướng (direction * magnitude) từ caller (vd: Vector2(-10, 0))
    // - knockbackEndTime = Time.time + KnockbackDuration: khóa FixedUpdate() không ghi đè vận tốc x
    //     trong KnockbackDuration giây (0.15s)
    // - Sau KnockbackDuration, FixedUpdate() hoạt động lại bình thường
    // Luồng gọi: PlayerHealth.TakeDamage() -> playerMovement.Knockback(vectorForce)
    //     -> rb.linearVelocity = force (ngay lập tức) + set knockbackEndTime
    //     -> FixedUpdate() kế tiếp: Time.time < knockbackEndTime -> bỏ qua set vận tốc x
    //     -> player bị đẩy lùi visible ngay frame đó
    //     -> sau 0.15s: FixedUpdate() set vận tốc x bình thường lại dựa trên input
    public void Knockback(Vector2 force)
    {
        if (rb == null) return;
        // [Bug#2-hotfix] Chỉ set X velocity từ force (đẩy ngang), preserve Y velocity (trọng lực/nhảy)
        // - force.x = -facingDir * knockbackForce (vd: facing phải → -10, trái → +10)
        // - rb.linearVelocity.y giữ nguyên để player vẫn rơi đúng theo gravity khi knockback
        rb.linearVelocity = new Vector2(force.x, rb.linearVelocity.y);
        knockbackEndTime = Time.time + KnockbackDuration;
    }

    public void ApplyMoveSpeed(float speed)
    {
        moveSpeed = Mathf.Max(speed, 0.1f);
    }

    public void Face(float facingDirection)
    {
        if (Mathf.Abs(facingDirection) <= 0.01f)
        {
            return;
        }

        transform.localScale = new Vector3(
            Mathf.Abs(transform.localScale.x) * Mathf.Sign(facingDirection),
            transform.localScale.y,
            transform.localScale.z
        );
    }

    private void Flip(float horizontalInput)
    {
        if (Mathf.Abs(horizontalInput) <= 0.01f)
        {
            return;
        }

        Face(horizontalInput);
    }
}
