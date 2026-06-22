using TinyDragon.Data;
using UnityEngine;

public class EnemyPatrol : MonoBehaviour
{
    private const string DefaultProjectilePrefabPath = "Combat/EnemyProjectile";

    [SerializeField] private float moveSpeed = GameplayBalanceDefaults.NormalEnemySpeed;
    [SerializeField] private float leftDistance = 1.5f;
    [SerializeField] private float rightDistance = 1.5f;
    [SerializeField] private float pointReachDistance = 0.05f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float verticalAttackTolerance = 1f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private int attackDamage = GameplayBalanceDefaults.NormalEnemyDamage;
    [SerializeField] private string attackTriggerName = "attack";
    [SerializeField] private string attackStateName = "Attack";
    [SerializeField] private float rangeAttackRange = 4f;
    [SerializeField] private float rangeAttackCooldown = 2f;
    [SerializeField] private int rangeAttackDamage = GameplayBalanceDefaults.NormalEnemyDamage;
    [SerializeField] private float projectileSpeed = 5f;
    [SerializeField] private float projectileLifetime = 3f;
    [SerializeField] private float projectileScale = 0.35f;
    [SerializeField] private Vector2 projectileSpawnOffset = new Vector2(0.45f, -0.05f);
    [SerializeField] private EnemyProjectile projectilePrefab;
    [SerializeField] private int projectilePoolPrewarmCount = 2;
    [SerializeField] private Sprite projectileSprite;
    [SerializeField] private bool projectileFacesRightByDefault = true;
    [SerializeField] private string rangeAttackTriggerName = "rangeAttack";
    [SerializeField] private string rangeAttackStateName = "rangeAttack";
    [SerializeField] private bool startMovingRight = true;
    [SerializeField] private bool flipWithDirection = true;
    [SerializeField] private bool facesRightByDefault = true;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Transform player;
    private PlayerHealth playerHealth;
    private Vector2 patrolOrigin;
    private int direction;
    private float nextAttackTime;
    private float nextRangeAttackTime;
    private ComponentPool<EnemyProjectile> projectilePool;
    private bool hasPatrolBounds;
    private float patrolMinX;
    private float patrolMaxX;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponent<Animator>();
        EnsureProjectilePool();
    }

    private void Start()
    {
        patrolOrigin = transform.position;
        direction = startMovingRight ? 1 : -1;

        PlayerController playerController = FindAnyObjectByType<PlayerController>();
        if (playerController != null)
        {
            player = playerController.transform;
            playerHealth = playerController.GetComponent<PlayerHealth>();

            if (playerHealth == null)
            {
                Debug.LogWarning("PlayerHealth is missing on the player. Enemy attacks will not damage the player.", playerController);
            }
        }
    }

    private void FixedUpdate()
    {
        if (CanAttackPlayer())
        {
            AttackPlayer();
            return;
        }

        if (CanRangeAttackPlayer())
        {
            RangeAttackPlayer();
            return;
        }

        Patrol();
    }

    private void Patrol()
    {
        float currentX = rb != null ? rb.position.x : transform.position.x;
        float targetX = GetTargetX();

        if (Mathf.Abs(currentX - targetX) <= pointReachDistance)
        {
            direction *= -1;
            targetX = GetTargetX();
        }

        float moveDirection = Mathf.Sign(targetX - currentX);

        MoveHorizontally(moveDirection);
        FlipToDirection(moveDirection);
    }

    private bool CanAttackPlayer()
    {
        if (player == null)
        {
            return false;
        }

        float horizontalDistance = Mathf.Abs(player.position.x - transform.position.x);
        float verticalDistance = Mathf.Abs(player.position.y - transform.position.y);

        return horizontalDistance <= attackRange && verticalDistance <= verticalAttackTolerance;
    }

    private bool CanRangeAttackPlayer()
    {
        if (player == null)
        {
            return false;
        }

        float horizontalDistance = Mathf.Abs(player.position.x - transform.position.x);
        float verticalDistance = Mathf.Abs(player.position.y - transform.position.y);

        return horizontalDistance > attackRange
            && horizontalDistance <= rangeAttackRange
            && verticalDistance <= verticalAttackTolerance;
    }

    private void AttackPlayer()
    {
        StopMovingHorizontally();
        FlipToDirection(player.position.x - transform.position.x);

        if (Time.time < nextAttackTime || animator == null)
        {
            return;
        }

        PlayAttackAnimation();
        DealDamage();
        nextAttackTime = Time.time + attackCooldown;
    }

    private void RangeAttackPlayer()
    {
        StopMovingHorizontally();
        FlipToDirection(player.position.x - transform.position.x);

        if (Time.time < nextRangeAttackTime || animator == null)
        {
            return;
        }

        PlayAnimation(rangeAttackTriggerName, rangeAttackStateName);
        ShootProjectile();
        nextRangeAttackTime = Time.time + rangeAttackCooldown;
    }

    private void PlayAttackAnimation()
    {
        PlayAnimation(attackTriggerName, attackStateName);
    }

    private void PlayAnimation(string triggerName, string stateName)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name != triggerName)
            {
                continue;
            }

            if (parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(triggerName);
                return;
            }

            if (parameter.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(triggerName, true);
                return;
            }
        }

        animator.CrossFade(stateName, 0f);
    }

    public void DealDamage()
    {
        if (!CanAttackPlayer() || playerHealth == null)
        {
            return;
        }

        Debug.Log($"Enemy dealt {attackDamage} damage to player.");
        playerHealth.TakeDamage(attackDamage);
    }

    public void ApplyCombatStats(
        float speed,
        int meleeDamage,
        int rangedDamage,
        float meleeCooldown,
        float rangedCooldown
    )
    {
        moveSpeed = Mathf.Max(speed, 0.1f);
        attackDamage = Mathf.Max(meleeDamage, 1);
        rangeAttackDamage = Mathf.Max(rangedDamage, 1);
        attackCooldown = Mathf.Max(meleeCooldown, 0.01f);
        rangeAttackCooldown = Mathf.Max(rangedCooldown, 0.01f);
    }

    public void SetPatrolBounds(float minimumX, float maximumX)
    {
        patrolMinX = Mathf.Min(minimumX, maximumX);
        patrolMaxX = Mathf.Max(minimumX, maximumX);
        hasPatrolBounds = patrolMinX < patrolMaxX;
    }

    public void ShootProjectile()
    {
        if (player == null)
        {
            return;
        }

        float facingDirection = player.position.x >= transform.position.x ? 1f : -1f;
        Vector3 spawnOffset = new Vector3(projectileSpawnOffset.x * facingDirection, projectileSpawnOffset.y, 0f);
        Vector3 spawnPosition = transform.position + spawnOffset;
        Vector2 projectileDirection = new Vector2(facingDirection, 0f);

        EnsureProjectilePool();
        if (projectilePool == null)
        {
            Debug.LogWarning("Enemy projectile prefab is missing. Assign Resources/Combat/EnemyProjectile to EnemyPatrol.", this);
            return;
        }

        EnemyProjectile projectile = projectilePool.Get(spawnPosition, Quaternion.identity);
        projectile.Initialize(
            projectileDirection,
            projectileSpeed,
            rangeAttackDamage,
            projectileLifetime,
            projectileSprite,
            projectileScale,
            projectileFacesRightByDefault,
            projectilePool.Release
        );
    }

    private void EnsureProjectilePool()
    {
        if (projectilePool != null)
        {
            return;
        }

        if (projectilePrefab == null)
        {
            GameObject projectilePrefabObject = Resources.Load<GameObject>(DefaultProjectilePrefabPath);
            if (projectilePrefabObject != null)
            {
                projectilePrefab = projectilePrefabObject.GetComponent<EnemyProjectile>();
            }
        }

        if (projectilePrefab != null)
        {
            projectilePool = new ComponentPool<EnemyProjectile>(
                projectilePrefab,
                RuntimeSceneRoot.GetChild("ProjectilePool"),
                projectilePoolPrewarmCount
            );
        }
    }

    private float GetTargetX()
    {
        float distance = direction > 0 ? rightDistance : -leftDistance;
        float targetX = patrolOrigin.x + distance;
        return hasPatrolBounds ? Mathf.Clamp(targetX, patrolMinX, patrolMaxX) : targetX;
    }

    private void MoveHorizontally(float moveDirection)
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(moveDirection * moveSpeed, rb.linearVelocity.y);
            return;
        }

        transform.Translate(Vector2.right * moveDirection * moveSpeed * Time.fixedDeltaTime);
    }

    private void StopMovingHorizontally()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    private void FlipToDirection(float directionX)
    {
        if (!flipWithDirection || spriteRenderer == null || Mathf.Abs(directionX) <= 0.01f)
        {
            return;
        }

        bool movingRight = directionX > 0f;
        spriteRenderer.flipX = facesRightByDefault ? !movingRight : movingRight;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying ? (Vector3)patrolOrigin : transform.position;
        Vector3 leftPoint = origin + Vector3.left * leftDistance;
        Vector3 rightPoint = origin + Vector3.right * rightDistance;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(leftPoint, rightPoint);
        Gizmos.DrawSphere(leftPoint, 0.08f);
        Gizmos.DrawSphere(rightPoint, 0.08f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, rangeAttackRange);
    }
}
