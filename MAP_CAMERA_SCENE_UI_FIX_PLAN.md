# Plan xử lý map, camera, scene flow và Pause/Settings

## Hiện trạng đã xác định

- `LangAru` đang để camera offset `(1.38, 1.91)`, gây lệch tâm.
- `Rigidbody2D Interpolate = None`, dễ gây rung camera.
- Camera hiện chỉ giới hạn trục X và đoán collider theo tên object.
- Player trong `LangAru` có `powerShotSprite = null`, nên rơi về asset chưởng thường cũ.
- Asset chưởng tốn mana đúng trong nhánh `dev` là `Assets/_Project/Art/491.png`.
- Pause/Settings mới chỉ được gắn tại `Level_02`; hai prefab còn reference chéo bị null.
- Nhiều tên scene cũ vẫn tồn tại: `Level_01_guide`, `Level_01_Original`.

## Wave 1 — Chuẩn hóa scene flow

- Tạo một nguồn tên scene dùng chung:
  - Khởi động: `Intro`.
  - Sau Intro hoặc Skip: `Level_01_Origin`.
  - Nhấn Start: `LangAru`.
- Sửa `IntroManager`, `MainMenu`, save fallback, Game Over, HUD, Inventory và editor start-scene để bỏ toàn bộ tên scene cũ.
- Khi đã xem Intro, mở game vẫn đi qua scene `Intro` nhưng tự chuyển tới `Level_01_Origin`.
- Return Menu từ Pause hoặc Game Over đi thẳng về `Level_01_Origin`.
- Kiểm tra để mọi scene được tham chiếu đều tồn tại trong Build Settings.

## Wave 2 — Map bounds và camera

- Tạo `MapBounds2D` rõ ràng cho từng gameplay scene, không tìm collider bằng tên.
- Bounds gồm:
  - Collider vật lý ngăn player vượt trái, phải và rơi khỏi đáy map.
  - Vùng giới hạn camera trên cả X và Y.
- Refactor `CameraFollow`:
  - Bám tâm collider của player.
  - Offset mặc định `(0, 0.75, -10)` để giữa X và nâng nhẹ Y.
  - Dùng `SmoothDamp` trong `LateUpdate`.
  - Snap ngay khi load hoặc respawn, sau đó mới nội suy.
  - Clamp theo kích thước camera và aspect ratio.
  - Bỏ cơ chế tự gắn vào camera của Intro/menu.
- Bật `Rigidbody2D Interpolate` cho Player prefab và các player đang nhúng trong scene.
- Áp dụng tại `LangAru`, `DoiHoaCuc`, `ThungLungTre`, `VoDaiXenBoHung`, `Level_02`, `Level_03`.

## Wave 3 — Gắn đúng asset chưởng mana

- Gắn `Assets/_Project/Art/491.png` làm `powerShotSprite` chuẩn trong Player prefab.
- Thay player nhúng trong `LangAru` bằng prefab instance hoặc đồng bộ toàn bộ override; không để scene tự giữ cấu hình projectile riêng.
- Chuẩn hóa import asset: Sprite, alpha transparency, không mipmap, Point Filter và pivot giữa.
- Giữ nguyên gameplay:
  - Phím `K`.
  - Tốn 50% Ki.
  - Damage và cooldown hiện tại.
  - Chưởng thường không bị đổi asset.
- Audit mọi gameplay scene để không còn `powerShotSprite = null` hoặc fallback sang chưởng thường.

## Wave 4 — Pause và Settings dùng chung

- Tạo một `GameMenuOverlay` prefab chứa:
  - Nút Pause.
  - Pause Canvas.
  - Settings Canvas.
  - Nút Settings trong Pause.
  - Liên kết manager đầy đủ, không còn reference null.
- Gắn Pause và Settings vào sáu gameplay scene.
- `Level_01_Origin` chỉ gắn Settings.
- `Intro` chỉ giữ Skip, không gắn Pause/Settings.
- Quy tắc trạng thái:
  - Pause khóa physics, player input và gameplay SFX.
  - Mở Settings từ Pause rồi đóng sẽ quay lại Pause.
  - Settings ở menu không thay đổi `Time.timeScale`.
  - Load scene hoặc hủy overlay luôn khôi phục `Time.timeScale`, input và audio.
  - Mỗi scene chỉ có một overlay và một EventSystem.
- Hoàn thiện Settings:
  - BGM slider điều khiển BGM.
  - SFX slider thực sự điều khiển SFX; hiện tại slider này chưa có tác dụng.
  - Lưu PlayerPrefs và áp dụng lại ngay sau khi chuyển scene.

## Kiểm thử chấp nhận

- Chạy mới đúng flow: `Intro → Level_01_Origin → Start → LangAru`.
- Skip Intro và trường hợp đã xem Intro đều về đúng menu.
- Player không vượt viền map; camera không lộ ngoài map ở tỉ lệ 16:9 và 4:3.
- Camera không lệch X, nâng nhẹ Y và không rung khi chạy hoặc nhảy.
- Chưởng `K` hiển thị đúng asset `491.png`, trừ đúng mana và không ảnh hưởng chưởng thường.
- Pause/Settings hoạt động trên mọi gameplay scene; Settings hoạt động tại menu.
- Flow `Pause → Settings → Back → Resume` không làm kẹt `Time.timeScale = 0`.
- Không còn reference tới `Level_01_guide` hoặc `Level_01_Original`.
- Unity compile sạch và build thành công toàn bộ scene trong Build Settings.

## Giả định đã chốt

- Camera khóa giữa theo trục X và nâng nhẹ Y với offset mặc định `0.75` world unit.
- Pause và Settings xuất hiện ở gameplay; menu chính chỉ có Settings; Intro chỉ có Skip.
- Asset chưởng tốn mana mới sử dụng file `Assets/_Project/Art/491.png` từ nhánh `dev` cũ.
- Các sửa đổi kéo theo thuộc scene flow, save fallback, HUD visibility, input, audio và prefab override đều nằm trong phạm vi xử lý.
