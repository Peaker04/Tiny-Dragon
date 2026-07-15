// Đạn LỚN của Player - bắn bằng phím K khi mana đầy.
// Kế thừa từ BaseProjectile. Sự khác biệt (damage cao hơn, to hơ, sprite riêng)
// được truyền vào qua ProjectileData từ PlayerController.
// Nếu muốn thêm hiệu ứng đặc biệt (ví dụ: nổ ra vùng AOE khi trúng),
// thì override OnTriggerEnter2D ở đây.
public class PlayerPowerShot : BaseProjectile
{
    // Tất cả logic (di chuyển, va chạm, damage) đã có sẵn ở BaseProjectile.
}
