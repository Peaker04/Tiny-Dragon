using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private KeyCode attackKey = KeyCode.J;
    [SerializeField] private float attackCooldown = 0.35f;
    [SerializeField] private int projectileDamage = 1;
    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private float projectileLifetime = 2f;
    [SerializeField] private float projectileScale = 1.2f;
    [SerializeField] private Vector2 projectileSpawnOffset = new Vector2(0.6f, 0.15f);
    [SerializeField] private Sprite projectileSprite;
    [SerializeField] private bool projectileFacesRightByDefault = true;
    [SerializeField] private string projectileSortingLayerName = "Default";
    [SerializeField] private int projectileSortingOrder = 100;
    [Header("Scene Transitions")]
    [SerializeField] private bool enableSceneTransitions = true;
    [SerializeField] private string level01SceneName = "Level_01_guide";
    [SerializeField] private string level02SceneName = "Level_02";
    [SerializeField] private float transitionExitPadding = 0.35f;
    [SerializeField] private float transitionEntryPadding = 1f;
    [SerializeField] private float transitionCooldown = 0.75f;
    [SerializeField] private float transitionSpawnY = -3.57f;
    [SerializeField] private float levelLeftEdgeX = -12.16f;
    [SerializeField] private float levelRightEdgeX = 12.34f;
    [SerializeField] private bool restoreCameraOnStart = false;
    [SerializeField] private Vector3 cameraPosition = new Vector3(0.06f, 0f, -10f);
    [SerializeField] private float cameraOrthographicSize = 5f;

    private Rigidbody2D rb;
    private Animator animator;
    private float moveX;
    private bool jumpPressed;
    private bool isGrounded;
    private bool hasIsGroundedParameter;
    private bool hasAttackParameter;
    private float nextAttackTime;
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

    [Header("Tutorial State")]
    public bool canMove = true;
    public bool canJump = true;
    public bool canAttack = true;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        ApplyPendingSpawn();

        // Tim Animator tren Player truoc, neu khong co thi tim trong object con.
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        hasIsGroundedParameter = HasAnimatorParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        hasAttackParameter = HasAnimatorParameter("Attack", AnimatorControllerParameterType.Trigger);
    }

    private void Start()
    {
        RestoreSceneCamera();
    }

    private void Update()
    {
        // Doc input trong Update de khong bo lo phim bam giua cac frame vat ly.
        moveX = canMove ? ReadHorizontalInput() : 0f;

        if (canJump && (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space)))
        {
            jumpPressed = true;
        }

        // Bam attackKey de kich hoat animation chuong cua body.
        if (canAttack && Input.GetKeyDown(attackKey) && Time.time >= nextAttackTime && animator != null && hasAttackParameter)
        {
            animator.SetTrigger("Attack");
            nextAttackTime = Time.time + attackCooldown;
        }

        // Speed = 0 thi idle, Speed > 0.1 thi chuyen sang walk trong Animator.
        if (animator != null)
        {
            animator.SetFloat("Speed", Mathf.Abs(moveX));
        }

        // Lat huong Player theo chieu di chuyen.
        Flip(moveX);

        CheckSceneTransition();
    }

    private float ReadHorizontalInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        bool movingLeft = Input.GetKey(KeyCode.LeftArrow);
        bool movingRight = Input.GetKey(KeyCode.RightArrow);

        if (movingLeft == movingRight)
        {
            return horizontal;
        }

        if (movingLeft)
        {
            return -1f;
        }

        return 1f;
    }

    private void Flip(float horizontalInput)
    {
        // Di sang phai thi localScale.x duong.
        if (horizontalInput > 0 && transform.localScale.x < 0)
        {
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
        // Di sang trai thi localScale.x am.
        else if (horizontalInput < 0 && transform.localScale.x > 0)
        {
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
    }

    private void FixedUpdate()
    {
        // Kiem tra Player co dang cham ground layer khong.
        isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            0.2f,
            groundLayer
        );

        rb.linearVelocity = new Vector2(moveX * moveSpeed, rb.linearVelocity.y);

        // Jump chi thuc hien khi da bam nut va Player dang dung tren dat.
        if (jumpPressed && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }

        // Chi set IsGrounded neu Animator co parameter nay, tranh warning moi frame.
        if (animator != null && hasIsGroundedParameter)
        {
            animator.SetBool("IsGrounded", isGrounded);
        }

        jumpPressed = false;
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
    }

    public void ShootProjectile()
    {
        if (projectileSprite == null)
        {
            Debug.LogWarning("Player projectile sprite is missing. Assign 491_0 to Projectile Sprite on PlayerController.", this);
        }

        float facingDirection = transform.localScale.x >= 0f ? 1f : -1f;
        Vector3 spawnOffset = new Vector3(projectileSpawnOffset.x * facingDirection, projectileSpawnOffset.y, 0f);
        Vector3 spawnPosition = transform.position + spawnOffset;
        Vector2 projectileDirection = new Vector2(facingDirection, 0f);

        GameObject projectileObject = new GameObject("Player Projectile");
        projectileObject.transform.position = spawnPosition;

        PlayerProjectile projectile = projectileObject.AddComponent<PlayerProjectile>();
        projectile.Initialize(
            projectileDirection,
            projectileSpeed,
            projectileDamage,
            projectileLifetime,
            projectileSprite,
            projectileScale,
            projectileFacesRightByDefault,
            projectileSortingLayerName,
            projectileSortingOrder
        );
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
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    private void ApplyPendingSpawn()
    {
        if (!hasPendingSpawn)
        {
            return;
        }

        transform.position = pendingSpawnPosition;
        transform.localScale = new Vector3(
            Mathf.Abs(transform.localScale.x) * pendingFacingDirection,
            transform.localScale.y,
            transform.localScale.z
        );

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
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
