using UnityEngine;
using UnityEngine.SceneManagement;
using TinyDragon.Data;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerSceneTransition : MonoBehaviour
{
    [SerializeField] private bool enableSceneTransitions = true;
    [SerializeField] private string level01SceneName = "LangAru";
    [SerializeField] private string level02SceneName = "Level_02";
    [SerializeField] private float transitionExitPadding = 0.35f;
    [SerializeField] private float transitionEntryPadding = 1f;
    [SerializeField] private float transitionCooldown = 0.75f;
    [SerializeField] private float transitionSpawnY = -3.57f;
    [SerializeField] private float levelLeftEdgeX = -12.16f;
    [SerializeField] private float levelRightEdgeX = 12.34f;
    [SerializeField] private string leftExitColliderName;
    [SerializeField] private bool exitColliderIsOnLeft = true;
    [SerializeField] private bool exitOnColliderContact;
    [SerializeField] private bool restoreCameraOnStart = false;
    [SerializeField] private Vector3 cameraPosition = new Vector3(0.06f, 0f, -10f);
    [SerializeField] private float cameraOrthographicSize = 5f;

    private PlayerMovement movement;
    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private Collider2D leftExitCollider;
    private bool isTransitioning;

    private static bool hasPendingSpawn;
    private static Vector3 pendingSpawnPosition;
    private static float pendingFacingDirection = 1f;
    private static bool globalTransitionInProgress;
    private static float transitionsLockedUntil;

    public static void LoadSceneWithPlayerSpawn(string sceneName, Vector3 spawnPosition, float facingDirection)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        hasPendingSpawn = true;
        pendingSpawnPosition = spawnPosition;
        pendingFacingDirection = Mathf.Approximately(facingDirection, 0f) ? 1f : Mathf.Sign(facingDirection);
        globalTransitionInProgress = true;
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

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
        playerCollider = GetComponent<Collider2D>();
        ApplyPendingSpawn();
    }

    private void Start()
    {
        RestoreSceneCamera();
    }

    private void Update()
    {
        CheckSceneTransition();
    }

    private void CheckSceneTransition()
    {
        if (!enableSceneTransitions || isTransitioning || globalTransitionInProgress || Time.time < transitionsLockedUntil)
        {
            return;
        }

        if (gameObject.scene != SceneManager.GetActiveScene())
        {
            return;
        }

        string activeSceneName = SceneManager.GetActiveScene().name;
        float rightExitX = levelRightEdgeX - transitionExitPadding;
        if (activeSceneName == level01SceneName && transform.position.x >= rightExitX)
        {
            LoadLinkedScene(level02SceneName, levelLeftEdgeX + transitionEntryPadding, 1f);
        }
        else if (activeSceneName == level02SceneName && HasReachedLinkedSceneExit())
        {
            LoadLinkedScene(level01SceneName, levelRightEdgeX - transitionEntryPadding, -1f);
        }
    }

    private bool HasReachedLinkedSceneExit()
    {
        if (string.IsNullOrWhiteSpace(leftExitColliderName))
        {
            return transform.position.x <= levelLeftEdgeX + transitionExitPadding;
        }

        if (leftExitCollider == null)
        {
            GameObject exitObject = GameObject.Find(leftExitColliderName);
            leftExitCollider = exitObject != null ? exitObject.GetComponent<Collider2D>() : null;
        }

        if (leftExitCollider == null)
        {
            return transform.position.x <= levelLeftEdgeX + transitionExitPadding;
        }

        if (exitOnColliderContact && playerCollider != null)
        {
            return playerCollider.IsTouching(leftExitCollider);
        }

        float exitX = exitColliderIsOnLeft
            ? leftExitCollider.bounds.max.x + transitionExitPadding
            : leftExitCollider.bounds.min.x - transitionExitPadding;

        return exitColliderIsOnLeft
            ? transform.position.x <= exitX
            : transform.position.x >= exitX;
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
        TinyDragonSaveManager.Instance.SaveCurrentPlayer();
        LoadSceneWithPlayerSpawn(
            sceneName,
            new Vector3(spawnX, transitionSpawnY, transform.position.z),
            facingDirection
        );
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

        Camera sceneCamera = Camera.main;
        if (sceneCamera == null)
        {
            return;
        }

        sceneCamera.transform.position = cameraPosition;
        sceneCamera.orthographic = true;
        sceneCamera.orthographicSize = cameraOrthographicSize;
    }
}
