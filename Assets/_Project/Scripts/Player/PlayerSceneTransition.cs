using UnityEngine;
using UnityEngine.SceneManagement;
using TinyDragon.Data;
using TinyDragon.Shared.Unity;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerSceneTransition : MonoBehaviour
{
    [SerializeField] private bool enableSceneTransitions = true;

    [SerializeField] private float transitionExitPadding = 0.35f;
    [SerializeField] private float transitionEntryPadding = 1f;
    [SerializeField] private float transitionCooldown = 0.75f;
    [SerializeField] private float transitionSpawnY = -3.57f;
    [SerializeField] private float levelLeftEdgeX = -12.16f;
    [SerializeField] private float levelRightEdgeX = 12.34f;
    [SerializeField] private bool restoreCameraOnStart = false;
    [SerializeField] private Vector3 cameraPosition = new Vector3(0.06f, 0f, -10f);
    [SerializeField] private float cameraOrthographicSize = 5f;

    private PlayerMovement movement;
    private Rigidbody2D rb;
    private bool isTransitioning;

    private static bool hasPendingSpawn;
    private static Vector3 pendingSpawnPosition;
    private static float pendingFacingDirection = 1f;
    private static bool globalTransitionInProgress;
    private static float transitionsLockedUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetTransitionState()
    {
        hasPendingSpawn = false;
        pendingSpawnPosition = Vector3.zero;
        pendingFacingDirection = 1f;
        globalTransitionInProgress = false;
        transitionsLockedUntil = 0f;
    }

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody2D>();
        ApplyPendingSpawn();
    }

    private void Start()
    {
        RestoreSceneCamera();

        // [SceneRule] Auto-clear các màn trước khi vào Level_03 (chế độ test)
        // - Khi player đến được màn 3, tự động đánh dấu các màn trước đó đã clear
        // - Mục đích: cho phép quay về các màn cũ để test mà không cần đánh lại
        // - Khi build chính thức, có thể comment hoặc xóa block này
        if (gameObject.scene.name == "Level_03")
        {
            SceneClearTracker.MarkSceneCleared("LangAru");
            SceneClearTracker.MarkSceneCleared("ThungLungTre");
            SceneClearTracker.MarkSceneCleared("DoiHoaCuc");
            SceneClearTracker.MarkSceneCleared("VoDaiXenBoHung");
        }

        // [SceneRule] Kiểm tra scene hiện tại đã được clear trước đó chưa
        // - SceneClearTracker.IsSceneCleared(): kiểm tra HashSet<string> clearedScenes
        // - Nếu scene đã clear: dùng Invoke("DelayedEnemyCleanup", 0f) để dọn quái sau tất cả Start()
        //     Lý do: spawners có thể spawn quái trong Start() của chúng, cần đợi chúng chạy xong
        //     Invoke(0f) chạy vào đầu Update frame kế tiếp, sau tất cả Start()
        // - Cleanup: Destroy toàn bộ EnemyHealth + disable EnemySpawner / FixedMobRespawner / Encounter
        if (SceneClearTracker.IsSceneCleared(gameObject.scene.name))
        {
            Invoke(nameof(DelayedEnemyCleanup), 0f);
        }
    }

    // [SceneRule] Dọn quái khi vào scene đã clear — chạy sau tất cả Start() hoàn tất
    // - Được gọi bởi Invoke("DelayedEnemyCleanup", 0f) trong Start()
    // - SceneClearTracker.DisableEnemiesInScene():
    //     FindObjectsByType<EnemyHealth> → Destroy gameObject
    //     FindObjectsByType<EnemySpawner> → Destroy gameObject
    //     FindObjectsByType<FixedMobRespawner> → Destroy gameObject
    //     FindObjectsByType<VoDaiXenBoHungEncounter> → Destroy gameObject
    private void DelayedEnemyCleanup()
    {
        SceneClearTracker.DisableEnemiesInScene();
    }

    private void Update()
    {
        CheckSceneTransition();
    }

    /// <summary>
    /// Edge-based transition is intentionally disabled.
    /// Scene transitions use trigger-based SceneExitOnPlayerContact instead.
    /// This method is kept as the guard-only stub so the Update loop
    /// can be reactivated if edge-based flow is needed in the future.
    /// </summary>
    private void CheckSceneTransition()
    {
        if (!enableSceneTransitions || isTransitioning || globalTransitionInProgress || Time.time < transitionsLockedUntil)
        {
            return;
        }

        if (!SceneNavigator.IsActiveScene(gameObject.scene))
        {
            return;
        }

        // Edge-based transition body removed — use SceneExitOnPlayerContact triggers.
    }

    private void LoadLinkedScene(string sceneName, float spawnX, float facingDirection)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        isTransitioning = true;
        globalTransitionInProgress = true;
        transitionsLockedUntil = Time.time + transitionCooldown;
        hasPendingSpawn = true;
        float resolvedFacingDirection = Mathf.Approximately(facingDirection, 0f) ? 1f : Mathf.Sign(facingDirection);
        pendingSpawnPosition = new Vector3(
            spawnX + transitionEntryPadding * resolvedFacingDirection,
            transitionSpawnY,
            transform.position.z
        );
        pendingFacingDirection = resolvedFacingDirection;
        TinyDragonSaveManager.Instance.SaveCurrentPlayer();
        SceneNavigator.LoadSceneIfSet(sceneName, LoadSceneMode.Single);
    }

    /// <summary>
    /// Static helper used by SceneExitOnPlayerContact to load a scene and
    /// set a specific world-space spawn position for the player on arrival.
    /// </summary>
    public static void LoadSceneWithPlayerSpawn(string sceneName, Vector3 spawnPosition, float facingDirection = 1f)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        globalTransitionInProgress = true;
        hasPendingSpawn = true;
        pendingSpawnPosition = spawnPosition;
        pendingFacingDirection = facingDirection;
        SceneNavigator.LoadSceneIfSet(sceneName, LoadSceneMode.Single);
    }

    private void ApplyPendingSpawn()
    {
        if (!hasPendingSpawn)
        {
            return;
        }

        transform.position = pendingSpawnPosition;

        if (movement != null)
        {
            movement.Face(pendingFacingDirection);
            movement.Stop();
        }
        else
        {
            transform.localScale = new Vector3(
                Mathf.Abs(transform.localScale.x) * pendingFacingDirection,
                transform.localScale.y,
                transform.localScale.z
            );

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }

        hasPendingSpawn = false;
        globalTransitionInProgress = false;
        transitionsLockedUntil = Time.time + transitionCooldown;
    }

    private void RestoreSceneCamera()
    {
        if (!restoreCameraOnStart)
        {
            return;
        }

        UnityEngine.Camera sceneCamera = UnityEngine.Camera.main;
        if (sceneCamera == null)
        {
            return;
        }

        sceneCamera.transform.position = cameraPosition;
        sceneCamera.orthographic = true;
        sceneCamera.orthographicSize = cameraOrthographicSize;
    }
}
