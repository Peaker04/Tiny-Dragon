using UnityEngine;
using TinyDragon.Shared.Unity;

[RequireComponent(typeof(Collider2D))]
public sealed class SceneExitOnPlayerContact : MonoBehaviour
{
    private enum EnemyGateMode
    {
        Automatic,
        RequireEnemyClear,
        IgnoreEnemyClear
    }

    [SerializeField] private string targetSceneName;
    [SerializeField] private bool useTargetSpawnPosition;
    [SerializeField] private Vector3 targetSpawnPosition;
    [SerializeField] private float targetFacingDirection = 1f;
    [SerializeField] private bool healOnExit;
    [SerializeField] private EnemyGateMode enemyGateMode;
    [SerializeField] private bool enableDebugLogging;

    private bool isLoadingScene;
    private bool hasLoggedContact;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        LogContact(collision.collider);
        TryLoadTargetScene(collision.collider);
        TryLoadTargetScene(collision.otherCollider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryLoadTargetScene(collision.collider);
        TryLoadTargetScene(collision.otherCollider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        LogContact(other);
        TryLoadTargetScene(other);
    }

    private void OnTriggerStay2D(Collider2D other)
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
        if (isLoadingScene || string.IsNullOrWhiteSpace(targetSceneName) || playerCollider == null)
        {
            return;
        }

        PlayerController playerController = playerCollider.GetComponentInParent<PlayerController>();
        if (playerController == null)
        {
            return;
        }

        bool requiresEnemyClear = enemyGateMode switch
        {
            EnemyGateMode.RequireEnemyClear => true,
            EnemyGateMode.IgnoreEnemyClear => false,
            _ => !SceneClearTracker.IsSceneCleared(targetSceneName)
        };

        if (requiresEnemyClear)
        {
            if (AnyAliveEnemyInScene())
            {
                Debug.Log("Scene locked: defeat all enemies before proceeding!");
                return;
            }

            SceneClearTracker.MarkSceneCleared(gameObject.scene.name);
        }

        isLoadingScene = true;

        if (enableDebugLogging)
        {
            Debug.Log(
                $"[SceneExitOnPlayerContact] Player detected on '{name}'. " +
                $"Loading '{targetSceneName}' from exit position {transform.position}.",
                this
            );
        }

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

    private void LogContact(Collider2D other)
    {
        if (!enableDebugLogging || hasLoggedContact || other == null)
        {
            return;
        }

        hasLoggedContact = true;
        PlayerController playerController = other.GetComponentInParent<PlayerController>();
        Debug.Log(
            $"[SceneExitOnPlayerContact] '{name}' received collision/trigger from " +
            $"'{other.name}'. PlayerDetected={playerController != null}, " +
            $"exitWorldPosition={transform.position}, exitBounds={GetComponent<Collider2D>().bounds}.",
            this
        );
    }
}
