using UnityEngine;
using UnityEngine.SceneManagement;
using TinyDragon.Data;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerSceneTransition : MonoBehaviour
{
    [SerializeField] private bool enableSceneTransitions = true;
    [SerializeField] private string level01SceneName = "Level_01_guide";
    [SerializeField] private string level02SceneName = "Level_02";
    [SerializeField] private string level03SceneName = "Level_03";
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
        float leftExitX = levelLeftEdgeX + transitionExitPadding;

        if (activeSceneName == level01SceneName && transform.position.x >= rightExitX)
        {
            LoadLinkedScene(level02SceneName, levelLeftEdgeX + transitionEntryPadding, 1f);
        }
        else if (activeSceneName == level02SceneName && transform.position.x <= leftExitX)
        {
            LoadLinkedScene(level01SceneName, levelRightEdgeX - transitionEntryPadding, -1f);
        }
        else if (activeSceneName == level02SceneName && transform.position.x >= rightExitX)
        {
            LoadLinkedScene(level03SceneName, levelLeftEdgeX + transitionEntryPadding, 1f);
        }
        else if (activeSceneName == level03SceneName && transform.position.x <= leftExitX)
        {
            LoadLinkedScene(level02SceneName, levelRightEdgeX - transitionEntryPadding, -1f);
        }
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
        pendingSpawnPosition = new Vector3(spawnX, transitionSpawnY, transform.position.z);
        pendingFacingDirection = facingDirection;
        TinyDragonSaveManager.Instance.SaveCurrentPlayer();
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
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
