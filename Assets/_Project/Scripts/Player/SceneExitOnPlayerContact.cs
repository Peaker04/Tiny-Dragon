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

        // [SceneRule] Tự động nhận biết tiến/lùi dựa trên scene đích
        // - Nếu targetSceneName đã clear → đây là exit lùi về map cũ:
        //     bỏ qua kiểm tra quái, cho qua luôn, không mark scene hiện tại
        // - Nếu targetSceneName chưa clear → đây là exit tiến sang map mới:
        //     kiểm tra còn quái không, nếu hết thì mark scene hiện tại clear + cho qua
        bool goingBack = SceneClearTracker.IsSceneCleared(targetSceneName);
        if (!goingBack)
        {
            if (AnyAliveEnemyInScene())
            {
                Debug.Log("Scene locked: defeat all enemies before proceeding!");
                return;
            }

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
