using TinyDragon.Data;
using TinyDragon.Config;
using TinyDragon.Shared.Unity;
using UnityEngine;

/// <summary>
/// Controls enemy patrol, melee attack, and ranged attack behavior
/// using an explicit state machine (EnemyState enum).
/// Projectile management is delegated to EnemyProjectileShooter.
/// </summary>
public class EnemyPatrol : MonoBehaviour
{
    private enum EnemyState
    {
        Patrol,
        Chase,
        MeleeAttack,
        RangedAttack
    }

    private enum RangedAttackMode
    {
        Projectile = 0,
        DirectHit = 1
    }

    [SerializeField] private TinyDragonRuntimeConfig runtimeConfig;
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
    [SerializeField] private RangedAttackMode rangedAttackMode = RangedAttackMode.Projectile;
    [SerializeField] private string rangeAttackTriggerName = "rangeAttack";
    [SerializeField] private string rangeAttackStateName = "rangeAttack";
    [SerializeField] private bool startMovingRight = true;
    [SerializeField] private bool flipWithDirection = true;
    [SerializeField] private bool facesRightByDefault = true;

    [Header("Projectile (delegated to EnemyProjectileShooter)")]
    [SerializeField] private EnemyProjectileShooter projectileShooter;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Transform player;
    private PlayerHealth playerHealth;
    private Vector2 patrolOrigin;
    private int direction;
    private float nextAttackTime;
    private float nextRangeAttackTime;
    private EnemyState currentState = EnemyState.Patrol;
    private bool hasPatrolBounds;
    private float patrolMinX;
    private float patrolMaxX;
    private bool hasArenaAwarenessBounds;
    private bool chasePlayerInsideArenaBounds;
    private float arenaAwarenessMinX;
    private float arenaAwarenessMaxX;
    private float arenaAwarenessMinY;
    private float arenaAwarenessMaxY;
    private TinyDragonRuntimeConfig Config => TinyDragonRuntimeConfigProvider.Resolve(runtimeConfig);

    private void Awake()
    {
        if (!Application.isPlaying) return;

        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponent<Animator>();

        if (projectileShooter == null)
        {
            projectileShooter = GetComponent<EnemyProjectileShooter>();
        }
    }

    private void Start()
    {
        if (!Application.isPlaying) return;

        patrolOrigin = transform.position;
        direction = startMovingRight ? 1 : -1;

        PlayerController playerController = ObjectLookup.Any<PlayerController>();
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
        currentState = DecideState();
        TickState(currentState);
    }

    // --- State machine ---

    private EnemyState DecideState()
    {
        if (CanAttackPlayer())
        {
            return EnemyState.MeleeAttack;
        }

        if (CanRangeAttackPlayer())
        {
            return EnemyState.RangedAttack;
        }

        if (ShouldChasePlayer())
        {
            return EnemyState.Chase;
        }

        return EnemyState.Patrol;
    }

    private void TickState(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.MeleeAttack:
                AttackPlayer();
                break;
            case EnemyState.RangedAttack:
                RangeAttackPlayer();
                break;
            case EnemyState.Chase:
                ChasePlayer();
                break;
            case EnemyState.Patrol:
            default:
                Patrol();
                break;
        }
    }

    // --- Patrol ---

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

    private bool ShouldChasePlayer()
    {
        if (!chasePlayerInsideArenaBounds || player == null || !IsPlayerInsideArenaAwareness())
        {
            return false;
        }

        float horizontalDistance = Mathf.Abs(player.position.x - transform.position.x);
        float verticalDistance = Mathf.Abs(player.position.y - transform.position.y);
        return horizontalDistance > rangeAttackRange && verticalDistance <= verticalAttackTolerance;
    }

    private void ChasePlayer()
    {
        float directionToPlayer = Mathf.Sign(player.position.x - transform.position.x);
        float currentX = rb != null ? rb.position.x : transform.position.x;

        if (hasPatrolBounds)
        {
            if ((directionToPlayer < 0f && currentX <= patrolMinX) || (directionToPlayer > 0f && currentX >= patrolMaxX))
            {
                StopMovingHorizontally();
                FlipToDirection(directionToPlayer);
                return;
            }
        }

        MoveHorizontally(directionToPlayer);
        FlipToDirection(directionToPlayer);
    }

    // --- Melee attack ---

    private bool CanAttackPlayer()
    {
        if (player == null)
        {
            return false;
        }

        if (!IsPlayerInsideArenaAwareness())
        {
            return false;
        }

        float horizontalDistance = Mathf.Abs(player.position.x - transform.position.x);
        float verticalDistance = Mathf.Abs(player.position.y - transform.position.y);

        return horizontalDistance <= attackRange && verticalDistance <= verticalAttackTolerance;
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

    public void DealDamage()
    {
        if (!CanAttackPlayer() || playerHealth == null)
        {
            return;
        }

        Debug.Log($"Enemy dealt {attackDamage} damage to player.");
        playerHealth.TakeDamage(attackDamage);
    }

    // --- Ranged attack ---

    private bool CanRangeAttackPlayer()
    {
        if (player == null)
        {
            return false;
        }

        if (!IsPlayerInsideArenaAwareness())
        {
            return false;
        }

        float horizontalDistance = Mathf.Abs(player.position.x - transform.position.x);
        float verticalDistance = Mathf.Abs(player.position.y - transform.position.y);

        return horizontalDistance > attackRange
            && horizontalDistance <= rangeAttackRange
            && verticalDistance <= verticalAttackTolerance;
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
        if (rangedAttackMode == RangedAttackMode.DirectHit)
        {
            DealRangeDamage();
        }
        else
        {
            ShootProjectile();
        }

        nextRangeAttackTime = Time.time + rangeAttackCooldown;
    }

    private void DealRangeDamage()
    {
        if (!CanRangeAttackPlayer() || playerHealth == null)
        {
            return;
        }

        Debug.Log($"Enemy dealt {rangeAttackDamage} ranged damage to player.");
        playerHealth.TakeDamage(rangeAttackDamage);
    }

    public void ShootProjectile()
    {
        if (player == null)
        {
            return;
        }

        if (projectileShooter != null)
        {
            projectileShooter.ShootAt(player.position, rangeAttackDamage);
        }
        else
        {
            Debug.LogWarning("EnemyProjectileShooter is not assigned. Add EnemyProjectileShooter component to this enemy.", this);
        }
    }

    // --- Animation ---

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

    // --- Public config API ---

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

    /// <summary>Scale movement speed and attack intervals by the given multipliers (Phase 2 transition).</summary>
    public void ApplyPhaseMultipliers(float movementMultiplier, float attackIntervalMultiplier)
    {
        moveSpeed = Mathf.Max(moveSpeed * movementMultiplier, 0.1f);
        attackCooldown = Mathf.Max(attackCooldown * attackIntervalMultiplier, 0.01f);
        rangeAttackCooldown = Mathf.Max(rangeAttackCooldown * attackIntervalMultiplier, 0.01f);
    }

    public void SetPatrolBounds(float minimumX, float maximumX)
    {
        patrolMinX = Mathf.Min(minimumX, maximumX);
        patrolMaxX = Mathf.Max(minimumX, maximumX);
        hasPatrolBounds = patrolMinX < patrolMaxX;
    }

    public void SetArenaAwareness(float horizontalRange, float verticalRange)
    {
        rangeAttackRange = Mathf.Max(rangeAttackRange, horizontalRange);
        verticalAttackTolerance = Mathf.Max(verticalAttackTolerance, verticalRange);
        hasArenaAwarenessBounds = false;
        chasePlayerInsideArenaBounds = false;
    }

    public void SetArenaAwarenessBounds(Bounds groundBounds, float horizontalPadding, float belowGroundTolerance, float aboveGroundTolerance)
    {
        SetArenaAwarenessBounds(groundBounds, horizontalPadding, belowGroundTolerance, aboveGroundTolerance, rangeAttackRange, true);
    }

    public void SetArenaAwarenessBounds(
        Bounds groundBounds,
        float horizontalPadding,
        float belowGroundTolerance,
        float aboveGroundTolerance,
        float rangedAttackDistance,
        bool chaseWhenAware
    )
    {
        rangeAttackRange = Mathf.Max(attackRange + 0.1f, rangedAttackDistance);
        verticalAttackTolerance = Mathf.Max(verticalAttackTolerance, belowGroundTolerance + aboveGroundTolerance);

        arenaAwarenessMinX = groundBounds.min.x - horizontalPadding;
        arenaAwarenessMaxX = groundBounds.max.x + horizontalPadding;
        arenaAwarenessMinY = groundBounds.max.y - belowGroundTolerance;
        arenaAwarenessMaxY = groundBounds.max.y + aboveGroundTolerance;
        hasArenaAwarenessBounds = true;
        chasePlayerInsideArenaBounds = chaseWhenAware;
    }

    private bool IsPlayerInsideArenaAwareness()
    {
        if (!hasArenaAwarenessBounds || player == null)
        {
            return true;
        }

        Vector3 playerPosition = player.position;
        return playerPosition.x >= arenaAwarenessMinX
            && playerPosition.x <= arenaAwarenessMaxX
            && playerPosition.y >= arenaAwarenessMinY
            && playerPosition.y <= arenaAwarenessMaxY;
    }

    // --- Movement helpers ---

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

    // --- Gizmos ---

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
