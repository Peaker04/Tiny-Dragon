# Refactor JSON Multipart Animation Helper cho Tiny-Dragon

## 1. Trạng thái hiện tại

Đã implement Phase 1-5 trên branch:

```text
refactor/json-animation-helper
```

Mục tiêu chính đã đạt:

- `BossAI` không còn phụ thuộc trực tiếp vào helper mang tên `Mob77`.
- `Mob77JsonAnimationBridge` đã được rename thành helper chung `JsonMultipartAnimationBridge`.
- Script JSON multipart animation đã được tách thành parser, sprite factory, action mapper, frame renderer và effect provider.
- Projectile effect đi qua interface `IProjectileEffectSource`.
- `BossAI` bắn projectile qua `EnemyProjectileShooter` thay vì tự tạo `GameObject`.

Không làm Phase 6 `EnemyBalanceApplier` trong lượt này.

## 2. Thay đổi đã thực hiện

### Shared Animation

Tạo nhóm helper chung dưới:

```text
Assets/_Project/Scripts/Shared/Animation
```

Các file đã thêm/tách:

- `IProjectileEffectSource.cs`
- `JsonMultipartAnimationData.cs`
- `JsonMultipartAnimationBridge.cs`
- `JsonMultipartAnimationParser.cs`
- `JsonMultipartSpriteFactory.cs`
- `JsonMultipartFrameRenderer.cs`
- `JsonAnimationActionMapper.cs`
- `JsonAnimationEffectSpriteProvider.cs`

`JsonMultipartAnimationBridge` hiện chỉ giữ vai trò MonoBehaviour điều phối:

- Load/parse JSON animation data.
- Tạo sprite cache qua `JsonMultipartSpriteFactory`.
- Map animator state qua `JsonAnimationActionMapper`.
- Render multipart frame qua `JsonMultipartFrameRenderer`.
- Cung cấp projectile effect qua `IProjectileEffectSource`.

### Unity GUID / Prefab Migration

Đã dùng rename an toàn:

```text
Assets/_Project/Scripts/Enemies/Mob77JsonAnimationBridge.cs
-> Assets/_Project/Scripts/Shared/Animation/JsonMultipartAnimationBridge.cs
```

File `.meta` cũng được move theo, nên GUID script cũ vẫn được giữ:

```text
a8c918f4aebfeea42957ef91e98e8c7a
```

Các prefab đã đổi `m_EditorClassIdentifier` sang:

```text
Assembly-CSharp::TinyDragon.Shared.Animation.JsonMultipartAnimationBridge
```

Prefab đã migrate:

- `Assets/_Project/Prefabs/Enemies/monster_77.prefab`
- `Assets/_Project/Resources/Enemies/CellJrMinibosses/CellJr_58_NormalShot.prefab`
- `Assets/_Project/Resources/Enemies/CellJrMinibosses/CellJr_63_PowerShot.prefab`
- `Assets/_Project/Resources/Enemies/CellJrMinibosses/CellJr_64_Punch.prefab`
- `Assets/_Project/Resources/Enemies/CellJrMinibosses/CellJr_65_Kick.prefab`

### BossAI

Đã thay đổi:

- Xóa phụ thuộc trực tiếp vào `Mob77JsonAnimationBridge`.
- Đổi `useMob77BridgeProjectileAnimation` thành `useAnimationBridgeProjectileEffect`.
- Đổi `ConfigureMob77ProjectileAnimation()` thành `ConfigureProjectileEffectFromSource()`.
- Lấy effect qua `IProjectileEffectSource`.
- Dùng `EnemyProjectileShooter.ShootAt(player.position, energyDamage)` khi bắn.
- Nếu boss chưa có `EnemyProjectileShooter`, `BossAI` sẽ add runtime fallback để scene cũ vẫn chạy được.

### Projectile

Tạo:

```text
Assets/_Project/Scripts/Combat/Projectiles/ProjectileVisualProfile.cs
```

`EnemyProjectileShooter` đã có thêm:

```csharp
public void ConfigureVisual(ProjectileVisualProfile profile)
```

Mục tiêu: boss/enemy cùng dùng một đường cấu hình projectile visual, gồm sprite tĩnh, animation sprites, frame rate, scale, spawn offset và hướng flip.

### Callers Updated

Đã đổi reference từ `Mob77JsonAnimationBridge` sang `JsonMultipartAnimationBridge` trong:

- `BossAI`
- `VoDaiXenBoHungEncounter`
- `Level03Manager`

## 3. Checklist thực hiện

- [x] Add `IProjectileEffectSource`.
- [x] Update `BossAI` to depend on `IProjectileEffectSource`.
- [x] Rename `useMob77BridgeProjectileAnimation` to `useAnimationBridgeProjectileEffect`.
- [x] Rename `ConfigureMob77ProjectileAnimation()` to `ConfigureProjectileEffectFromSource()`.
- [x] Create `JsonMultipartAnimationBridge`.
- [x] Migrate prefab/scene references from `Mob77JsonAnimationBridge` to `JsonMultipartAnimationBridge`.
- [x] Move raw JSON parsing into `JsonMultipartAnimationParser`.
- [x] Move sprite creation into `JsonMultipartSpriteFactory`.
- [x] Move Animator mapping into `JsonAnimationActionMapper`.
- [x] Move multipart rendering into `JsonMultipartFrameRenderer`.
- [x] Move projectile effect extraction into `JsonAnimationEffectSpriteProvider`.
- [x] Add `ProjectileVisualProfile`.
- [x] Let `BossAI` use `EnemyProjectileShooter` instead of creating projectile GameObjects directly.
- [x] Verify static prefab script GUID/class identifier migration.
- [x] Verify build succeeds.
- [ ] Verify no Missing Script inside Unity Editor.
- [ ] Verify boss idle/move/attack animations in Unity play mode.
- [ ] Verify boss projectile effect in Unity play mode.
- [ ] Verify VoDaiXenBoHung miniboss flow in Unity play mode.

## 4. Verification đã chạy

Build:

```powershell
dotnet build ".\Tiny Dragon.slnx" -v:minimal
```

Kết quả:

- Build succeeded.
- 0 errors.
- 3 warnings cũ trong `PlayerSceneTransition`:
  - `transitionExitPadding` assigned but never used.
  - `levelLeftEdgeX` assigned but never used.
  - `levelRightEdgeX` assigned but never used.

Static checks:

```powershell
rg "Mob77" "Assets/_Project/Scripts"
```

Kết quả:

```text
NO_MATCH
```

```powershell
rg "Mob77JsonAnimationBridge|useMob77BridgeProjectileAnimation|ConfigureMob77ProjectileAnimation" "Assets"
```

Kết quả:

```text
NO_MATCH
```

Prefab GUID/class identifier check:

```text
m_Script guid: a8c918f4aebfeea42957ef91e98e8c7a
m_EditorClassIdentifier: Assembly-CSharp::TinyDragon.Shared.Animation.JsonMultipartAnimationBridge
```

Đã xác nhận trên `monster_77` và 4 CellJr prefab.

## 5. Acceptance Criteria

- [x] `BossAI.cs` không còn chữ `Mob77`.
- [x] Không còn field `useMob77BridgeProjectileAnimation`.
- [x] Không còn method `ConfigureMob77ProjectileAnimation`.
- [x] `BossAI` lấy projectile effect thông qua `IProjectileEffectSource`.
- [x] Helper JSON animation dùng tên chung, không phụ thuộc tên file boss.
- [x] `mob77` chỉ còn nên xuất hiện ở asset path, prefab name hoặc data file.
- [x] Build pass.
- [x] Không còn reference `Mob77JsonAnimationBridge` trong `Assets`.
- [ ] Boss trong scene vẫn chạy animation đúng trong Unity play mode.
- [ ] Boss vẫn bắn projectile đúng trong Unity play mode.
- [ ] Không có Missing Script trong Unity Inspector.
- [ ] Không phá logic enemy thường/miniboss trong Unity play mode.

## 6. Scope không đưa vào refactor

Không đưa vào scope:

- `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset`
- Phase 6 `EnemyBalanceApplier`
- Commit/push

File này hiện là tài liệu tracking cho refactor. Chỉ commit file này nếu muốn lưu lại trạng thái implementation cùng branch refactor.

## 7. Ghi chú tiếp theo

Trước khi merge/push nên làm thêm trong Unity:

1. Mở prefab `monster_77` và 4 CellJr prefab, xác nhận không có Missing Script.
2. Play `Level_03`, kiểm tra boss idle/move/melee/ranged.
3. Play `VoDaiXenBoHung`, kiểm tra miniboss vẫn spawn/activate tuần tự và projectile không lẫn melee effect.
4. Nếu ổn, stage toàn bộ refactor trừ TextMesh Pro dirty file, rồi commit.
