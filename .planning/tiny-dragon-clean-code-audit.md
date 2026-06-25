# Tiny Dragon Clean Code Audit

Date: 2026-06-25

## Scope Implemented

- Added a shared runtime configuration asset path: `Assets/_Project/Resources/Config/TinyDragonRuntimeConfig.asset`.
- Added shared runtime helpers under `Assets/_Project/Scripts/Shared`:
  - `Shared/Unity/ResourceLoader.cs`
  - `Shared/Unity/SceneNavigator.cs`
  - `Shared/Unity/ObjectLookup.cs`
  - `Shared/UI/UiFactory.cs`
  - `Shared/Gameplay/SpawnGeometry2D.cs`
- Added config foundation under `Assets/_Project/Scripts/Config`.
- Moved editor-only scripts out of runtime script compilation into `Assets/_Project/Editor`.
- Removed stale root `old_hud.cs`.
- Replaced the deleted `EnemySpawnGeometry` with shared `SpawnGeometry2D`.

## Structure Assessment

The project is now closer to a Unity-friendly layout:

- Runtime gameplay scripts stay under `Assets/_Project/Scripts`.
- Editor scripts live under `Assets/_Project/Editor`, so Unity excludes them from runtime player builds.
- Cross-cutting helpers are grouped under `Shared`, avoiding helper classes scattered across feature folders.
- Runtime tuning and resource/scene names are centralized in `TinyDragonRuntimeConfig` instead of being duplicated across UI, player, enemy, and Level03 scripts.

Remaining structure risk:

- Several large MonoBehaviours still own too many responsibilities, especially `InventoryPanel`, `TinyDragonSaveManager`, `Level03Manager`, and `PlayerStatusHud`.
- Some scene dependencies are still resolved dynamically. They now go through `ObjectLookup`, but the next wave should prefer serialized fields or scene installers where prefab/scene setup is verified.
- Unity scene names and Resources paths now have a config home, but the project still needs scene smoke tests to confirm every configured value matches build settings and assets.

## Hardcoded Value Cleanup

Moved into config or shared defaults:

- Common scene names for main menu, intro, game over, guide, new game, Level02, HUD hidden scenes.
- Resources paths for database schema/seed, background music, damage popup, player projectile, enemy projectile, and HUD sprites.
- Level02 spawn tuning.
- Inventory tab colors and HUD target text colors.
- Level03 required fragments, timings, platform geometry, fragment scale, collider sizing, and merge point.

Still intentionally local:

- Fine-grained UI layout coordinates inside `PlayerStatusHud`.
- Enemy animation parameter names and attack ranges, because these should become per-prefab serialized tuning or ScriptableObject enemy profiles in a later pass.
- Scene object names such as `BackgroundMusicPlayer`, `RuntimeObjects`, and `Ground_Main_Collider`; they are now routed through helpers but should become constants/config only if multiple systems need to share them.

## Next Safe Waves

1. Split `InventoryPanel` into inventory binding, tab state, item rendering, and input handling.
2. Split `TinyDragonSaveManager` into database repository, snapshot service, inventory/balance service, and scene save bridge.
3. Split `Level03Manager` into encounter state machine, boss shield coordinator, platform coordinator, and dragon gem effect coordinator.
4. Add enemy ScriptableObject profiles for patrol, boss, projectile, and damage tuning.
5. Replace dynamic object lookups with serialized dependencies scene by scene after Unity smoke testing.

## Verification

- `dotnet build ".\Tiny Dragon.slnx" -v:minimal`: passed with 0 warnings and 0 errors.
- C# `.meta` coverage under `_Project/Scripts`, `_Project/Resources`, and `_Project/Editor`: passed with 0 missing meta files.
- Static scan now shows direct `Resources.Load`, `SceneManager.LoadScene`, `FindAnyObjectByType`, and `GameObject.Find` usage only inside the shared/config wrapper layer, plus indirect HUD sprite helper calls.

