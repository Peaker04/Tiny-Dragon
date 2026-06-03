using UnityEngine;

public class EnemyPatrol : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float leftDistance = 1.5f;
    [SerializeField] private float rightDistance = 1.5f;
    [SerializeField] private float pointReachDistance = 0.05f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float verticalAttackTolerance = 1f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private string attackTriggerName = "attack";
    [SerializeField] private string attackStateName = "Attack";
    [SerializeField] private float rangeAttackRange = 4f;
    [SerializeField] private float rangeAttackCooldown = 2f;
    [SerializeField] private int rangeAttackDamage = 1;
    [SerializeField] private float projectileSpeed = 5f;
    [SerializeField] private float projectileLifetime = 3f;
    [SerializeField] private float projectileScale = 0.35f;
    [SerializeField] private Vector2 projectileSpawnOffset = new Vector2(0.45f, -0.05f);
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

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponent<Animator>();
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
                playerHealth = playerController.gameObject.AddComponent<PlayerHealth>();
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

        GameObject projectileObject = new GameObject("Enemy Projectile");
        projectileObject.transform.position = spawnPosition;

        EnemyProjectile projectile = projectileObject.AddComponent<EnemyProjectile>();
        projectile.Initialize(
            projectileDirection,
            projectileSpeed,
            rangeAttackDamage,
            projectileLifetime,
            projectileSprite,
            projectileScale,
            projectileFacesRightByDefault
        );
    }

    private float GetTargetX()
    {
        float distance = direction > 0 ? rightDistance : -leftDistance;
        return patrolOrigin.x + distance;
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
