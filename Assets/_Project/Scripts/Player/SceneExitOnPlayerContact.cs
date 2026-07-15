using UnityEngine;
using TinyDragon.Shared.Unity;

[RequireComponent(typeof(Collider2D))]
public sealed class SceneExitOnPlayerContact : MonoBehaviour
{
    [SerializeField] private string targetSceneName;
    [SerializeField] private bool useTargetSpawnPosition;
    [SerializeField] private Vector3 targetSpawnPosition;
    [SerializeField] private float targetFacingDirection = 1f;
    [SerializeField] private bool healOnExit;

    // [SceneRule] isForwardExit = true (default): exit tiến sang map mới
    //   - Kiểm tra còn quái không: nếu còn → chặn
    //   - Nếu hết quái → đánh dấu scene đã clear, cho đi tiếp
    // - isForwardExit = false: exit lùi về map cũ
    //   - Bỏ qua kiểm tra quái, cho đi tự do
    //   - Không đánh dấu scene clear (vì chưa hoàn thành)
    //   - Dành cho các trigger đưa player về scene trước đó
    [SerializeField] private bool isForwardExit = true;

    private bool isLoadingScene;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryLoadTargetScene(collision.collider);
        TryLoadTargetScene(collision.otherCollider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryLoadTargetScene(other);
    }

    // [SceneRule] Kiểm tra còn quái sống trong scene không
    // - Dùng FindObjectsByType để tìm tất cả EnemyHealth đang active
    // - Chỉ kiểm tra enemy.CurrentHealth > 0 (còn sống)
    // - Nếu còn quái: log warning + reset isLoadingScene để player có thể trigger lại sau
    // - Nếu hết quái: cho phép chuyển scene
    private static bool AnyAliveEnemyInScene()
    {
        EnemyHealth[] enemies = Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
        foreach (EnemyHealth enemy in enemies)
        {
            if (enemy != null && enemy.isActiveAndEnabled && enemy.CurrentHealth > 0)
            {
                return true;
            }
        }
        return false;
    }

    private void TryLoadTargetScene(Collider2D playerCollider)
    {
        if (isLoadingScene || string.IsNullOrWhiteSpace(targetSceneName))
        {
            return;
        }

        if (playerCollider.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        // [SceneRule] Exit tiến (isForwardExit = true): kiểm tra quái trước khi cho qua
        // - isForwardExit = false: exit lùi về map cũ → bỏ qua kiểm tra, không mark clear
        if (isForwardExit)
        {
            if (AnyAliveEnemyInScene())
            {
                Debug.Log("Scene locked: defeat all enemies before proceeding!");
                return;
            }

            // [SceneRule] Khi tất cả quái đã chết và player rời scene → đánh dấu scene này đã clear
            // - SceneClearTracker.MarkSceneCleared() lưu tên scene vào HashSet<string>
            // - Lần sau vào lại scene này, PlayerSceneTransition.Start() sẽ dọn sạch quái
            // - gameObject.scene.name: tên scene hiện tại (của trigger exit collider)
            SceneClearTracker.MarkSceneCleared(gameObject.scene.name);
        }

        isLoadingScene = true;

        if (healOnExit)
        {
            PlayerHealth playerHealth = playerCollider.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.Revive();
            }
        }

        if (useTargetSpawnPosition)
        {
            PlayerSceneTransition.LoadSceneWithPlayerSpawn(
                targetSceneName,
                targetSpawnPosition,
                targetFacingDirection
            );
            return;
        }

        SceneNavigator.LoadSceneIfSet(targetSceneName);
    }
}
