using System.Collections.Generic;
using UnityEngine;

public static class SceneClearTracker
{
    private static readonly HashSet<string> clearedScenes = new HashSet<string>();

    // [SceneRule] Đánh dấu sceneName đã clear (tất cả quái đã bị tiêu diệt)
    // - Được gọi từ SceneExitOnPlayerContact khi player thoát scene thành công (hết quái)
    // - clearedScenes là HashSet<string> lưu tên scene, không persist giữa các phiên chơi
    public static void MarkSceneCleared(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
            clearedScenes.Add(sceneName);
    }

    // [SceneRule] Kiểm tra sceneName đã được clear trước đó chưa
    // - Được gọi từ PlayerSceneTransition.Start() khi player spawn vào scene
    // - Nếu true, PlayerSceneTransition sẽ dọn sạch quái + disable spawners
    public static bool IsSceneCleared(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) && clearedScenes.Contains(sceneName);
    }

    // [SceneRule] Dọn toàn bộ enemy + spawner trong scene hiện tại
    // - Destroy toàn bộ GameObject có EnemyHealth (quái đứng im / patrol)
    // - Destroy toàn bộ GameObject có EnemySpawner (spawn quái theo làn)
    // - Destroy toàn bộ GameObject có FixedMobRespawner (respawn quái cố định)
    // - Destroy toàn bộ GameObject có VoDaiXenBoHungEncounter (miniboss arena)
    // - Dùng Destroy (cuối frame) thay vì DestroyImmediate để tránh lỗi trong vòng lặp
    public static void DisableEnemiesInScene()
    {
        foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
        {
            if (enemy != null)
                Object.Destroy(enemy.gameObject);
        }

        foreach (EnemySpawner spawner in FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
        {
            if (spawner != null)
                Object.Destroy(spawner.gameObject);
        }

        foreach (FixedMobRespawner respawner in FindObjectsByType<FixedMobRespawner>(FindObjectsSortMode.None))
        {
            if (respawner != null)
                Object.Destroy(respawner.gameObject);
        }

        foreach (VoDaiXenBoHungEncounter encounter in FindObjectsByType<VoDaiXenBoHungEncounter>(FindObjectsSortMode.None))
        {
            if (encounter != null)
                Object.Destroy(encounter.gameObject);
        }
    }

    // [SceneRule] Reset toàn bộ dữ liệu clear — gọi khi bắt đầu game mới
    // - Được gọi từ PlayerController hoặc GameManager khi new game
    public static void ResetForNewGame()
    {
        clearedScenes.Clear();
    }
}
