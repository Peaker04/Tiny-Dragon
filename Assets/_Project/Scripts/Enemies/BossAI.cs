using TinyDragon.Combat.Projectiles;
using TinyDragon.Data;
using TinyDragon.Shared.Animation;
using TinyDragon.Shared.Unity;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BossAI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;
    [SerializeField] private float detectRange = 8f;
    [SerializeField] private float stopDistance = 1.15f;
    [SerializeField] private float verticalTolerance = 1.25f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = GameplayBalanceDefaults.BossSpeed;
    [SerializeField] private bool flipWithDirection = true;
    [SerializeField] private bool facesRightByDefault = true;

    [Header("Close Attack")]
    [SerializeField] private float meleeRange = 1.25f;
    [SerializeField] private float meleeCooldown = 1.1f;
    [SerializeField] private int meleeDamage = GameplayBalanceDefaults.BossMeleeDamage;
    [SerializeField] private string meleeTriggerName = "SlashAttack";
    [SerializeField] private string meleeStateName = "Boss_slash_attack";

    [Header("Energy Attack")]
    [SerializeField] private float energyRange = 5f;
    [SerializeField] private float energyCooldown = 2.4f;
    [SerializeField] private float energyProjectileDelay = 0.28f;
    [SerializeField] private int energyDamage = GameplayBalanceDefaults.BossEnergyDamage;
    [SerializeField] private float projectileScale = 0.45f;
    [SerializeField] private Vector2 projectileSpawnOffset = new Vector2(0.8f, 0.15f);
    [SerializeField] private Sprite projectileSprite;
    [SerializeField] private Sprite[] projectileAnimationSprites;
    [SerializeField] private float projectileAnimationFrameRate = 12f;
    [SerializeField] private bool useAnimationBridgeProjectileEffect = true;
    [SerializeField] private bool projectileFacesRightByDefault = true;
    [SerializeField] private EnemyProjectileShooter projectileShooter;
    [SerializeField] private string energyTriggerName = "EnergyBlast";
    [SerializeField] private string energyStateName = "Boss_energy_blast";

    [Header("Combo Attack")]
    [SerializeField] private bool useComboAttack = true;
    [SerializeField] private float comboRange = 2.4f;
    [SerializeField] private float comboCooldown = 3.5f;
    [SerializeField] private string comboTriggerName = "ComboSlashBlast";
    [SerializeField] private string comboStateName = "Boss_combo_slash_blast";

    [Header("Animator")]
    [SerializeField] private string movingParameterName = "MoveDash";
    [SerializeField] private string idleStateName = "Boss_idle";
    [SerializeField] private string movingStateName = "Boss_move_dash";
    [SerializeField] private string hitTriggerName = "Hit";

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private PlayerHealth playerHealth;
    private float nextMeleeTime;
    private float nextEnergyTime;
    private float nextComboTime;
    private float attackLockUntil;
    private float pendingEnergyShotTime;
    private bool hasPendingEnergyShot;
    private bool isMovingAnimation;
    private string currentAttackState;
    private bool hasArenaAwarenessBounds;
    private float arenaAwarenessMinX;
    private float arenaAwarenessMaxX;
    private float arenaAwarenessMinY;
    private float arenaAwarenessMaxY;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponent<Animator>();
        if (projectileShooter == null)
        {
            projectileShooter = GetComponent<EnemyProjectileShooter>();
        }

        if (projectileShooter == null)
        {
            projectileShooter = gameObject.AddComponent<EnemyProjectileShooter>();
        }
    }

    private void Start()
    {
        if (player == null)
        {
            PlayerController playerController = ObjectLookup.Any<PlayerController>();
            if (playerController != null)
            {
                player = playerController.transform;
            }
        }

        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }

        ConfigureProjectileEffectFromSource();
        ConfigureProjectileShooterVisual();
    }

    private void ConfigureProjectileEffectFromSource()
    {
        if (!useAnimationBridgeProjectileEffect || HasAnimationSprites(projectileAnimationSprites))
        {
            return;
        }

        IProjectileEffectSource effectSource = FindProjectileEffectSource();
        if (effectSource == null)
        {
            return;
        }

        if (!effectSource.TryGetProjectileEffect(out Sprite[] effectSprites, out float effectFrameRate))
        {
            return;
        }

        projectileAnimationSprites = effectSprites;
        projectileAnimationFrameRate = Mathf.Max(1f, effectFrameRate);
        projectileSprite = effectSprites[0];
    }

    private void ConfigureProjectileShooterVisual()
    {
        if (projectileShooter == null)
        {
            return;
        }

        projectileShooter.ConfigureVisual(new ProjectileVisualProfile
        {
            sprite = projectileSprite,
            animationSprites = projectileAnimationSprites,
            animationFrameRate = projectileAnimationFrameRate,
            scale = projectileScale,
            spawnOffset = projectileSpawnOffset,
            facesRightByDefault = projectileFacesRightByDefault
        });
    }

    private IProjectileEffectSource FindProjectileEffectSource()
    {
        MonoBehaviour[] components = GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour component in components)
        {
            if (component is IProjectileEffectSource effectSource)
            {
                return effectSource;
            }
        }

        return null;
    }

    private void FixedUpdate()
    {
        TryShootPendingEnergyProjectile();

        if (player == null)
        {
            SetMoving(false);
            StopMoving();
            return;
        }

        if (Time.time < attackLockUntil)
        {
            SetMoving(false);
            StopMoving();
            FacePlayer();
            return;
        }

        if (!IsPlayerInsideArenaAwareness())
        {
            SetMoving(false);
            StopMoving();
            return;
        }

        float horizontalDistance = Mathf.Abs(player.position.x - transform.position.x);
        float verticalDistance = Mathf.Abs(player.position.y - transform.position.y);

        if (horizontalDistance > detectRange || verticalDistance > verticalTolerance)
        {
            SetMoving(false);
            StopMoving();
            return;
        }

        FacePlayer();

        if (horizontalDistance <= meleeRange && Time.time >= nextMeleeTime)
        {
            DoMeleeAttack();
            return;
        }

        if (useComboAttack && horizontalDistance <= comboRange && Time.time >= nextComboTime)
        {
            DoComboAttack();
            return;
        }

        if (horizontalDistance <= energyRange && horizontalDistance > meleeRange && Time.time >= nextEnergyTime)
        {
            DoEnergyAttack();
            return;
        }

        if (horizontalDistance > stopDistance)
        {
            ChasePlayer();
            return;
        }

        SetMoving(false);
        StopMoving();
    }

    public void PlayHit()
    {
        PlayAnimation(hitTriggerName, "Boss_hit_or_special");
    }

    public void ApplyCombatStats(float speed, int closeDamage, int rangedDamage)
    {
        moveSpeed = Mathf.Max(speed, 0.1f);
        meleeDamage = Mathf.Max(closeDamage, 1);
        energyDamage = Mathf.Max(rangedDamage, 1);
    }

    public void ApplyPhaseMultipliers(float movementMultiplier, float attackIntervalMultiplier)
    {
        moveSpeed = Mathf.Max(moveSpeed * movementMultiplier, 0.1f);
        meleeCooldown = Mathf.Max(meleeCooldown * attackIntervalMultiplier, 0.01f);
        energyCooldown = Mathf.Max(energyCooldown * attackIntervalMultiplier, 0.01f);
        comboCooldown = Mathf.Max(comboCooldown * attackIntervalMultiplier, 0.01f);
    }

    public void ConfigureAnimationProfile(
        string movingParameter,
        string idleState,
        string movingState,
        string meleeTrigger,
        string meleeState,
        string energyTrigger,
        string energyState,
        string comboTrigger,
        string comboState,
        string hitTrigger)
    {
        movingParameterName = movingParameter ?? string.Empty;
        idleStateName = string.IsNullOrWhiteSpace(idleState) ? idleStateName : idleState;
        movingStateName = string.IsNullOrWhiteSpace(movingState) ? movingStateName : movingState;
        meleeTriggerName = string.IsNullOrWhiteSpace(meleeTrigger) ? meleeTriggerName : meleeTrigger;
        meleeStateName = string.IsNullOrWhiteSpace(meleeState) ? meleeStateName : meleeState;
        energyTriggerName = string.IsNullOrWhiteSpace(energyTrigger) ? energyTriggerName : energyTrigger;
        energyStateName = string.IsNullOrWhiteSpace(energyState) ? energyStateName : energyState;
        comboTriggerName = string.IsNullOrWhiteSpace(comboTrigger) ? comboTriggerName : comboTrigger;
        comboStateName = string.IsNullOrWhiteSpace(comboState) ? comboStateName : comboState;
        hitTriggerName = string.IsNullOrWhiteSpace(hitTrigger) ? hitTriggerName : hitTrigger;
    }

    public void SetArenaAwareness(float horizontalRange, float verticalRange)
    {
        detectRange = Mathf.Max(detectRange, horizontalRange);
        energyRange = Mathf.Max(energyRange, horizontalRange);
        verticalTolerance = Mathf.Max(verticalTolerance, verticalRange);
        hasArenaAwarenessBounds = false;
    }

    public void SetDetectionAwareness(float horizontalRange, float verticalRange)
    {
        detectRange = Mathf.Max(detectRange, horizontalRange);
        verticalTolerance = Mathf.Max(verticalTolerance, verticalRange);
        hasArenaAwarenessBounds = false;
    }

    public void SetArenaAwarenessBounds(Bounds groundBounds, float horizontalPadding, float belowGroundTolerance, float aboveGroundTolerance)
    {
        detectRange = Mathf.Max(detectRange, groundBounds.size.x + horizontalPadding * 2f);
        energyRange = Mathf.Max(energyRange, groundBounds.size.x + horizontalPadding * 2f);
        verticalTolerance = Mathf.Max(verticalTolerance, belowGroundTolerance + aboveGroundTolerance);

        arenaAwarenessMinX = groundBounds.min.x - horizontalPadding;
        arenaAwarenessMaxX = groundBounds.max.x + horizontalPadding;
        arenaAwarenessMinY = groundBounds.max.y - belowGroundTolerance;
        arenaAwarenessMaxY = groundBounds.max.y + aboveGroundTolerance;
        hasArenaAwarenessBounds = true;
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

    public void DealMeleeDamage()
    {
        if (playerHealth == null || player == null || !IsPlayerInsideArenaAwareness())
        {
            return;
        }

        float horizontalDistance = Mathf.Abs(player.position.x - transform.position.x);
        float verticalDistance = Mathf.Abs(player.position.y - transform.position.y);
        if (horizontalDistance <= meleeRange && verticalDistance <= verticalTolerance)
        {
            playerHealth.TakeDamage(meleeDamage);
        }
    }

    public void ShootEnergyProjectile()
    {
        if (projectileShooter == null || player == null || !IsPlayerInsideArenaAwareness())
        {
            return;
        }

        projectileShooter.ShootAt(player.position, energyDamage);
    }

    private static bool HasAnimationSprites(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
        {
            return false;
        }

        foreach (Sprite sprite in sprites)
        {
            if (sprite != null)
            {
                return true;
            }
        }

        return false;
    }

    private void ChasePlayer()
    {
        float direction = Mathf.Sign(player.position.x - transform.position.x);
        rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
        SetMoving(true);
    }

    private void StopMoving()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void DoMeleeAttack()
    {
        StopMoving();
        SetMoving(false);
        PlayAnimation(meleeTriggerName, meleeStateName);
        DealMeleeDamage();
        nextMeleeTime = Time.time + meleeCooldown;
        LockAttack(0.45f);
    }

    private void DoEnergyAttack()
    {
        StopMoving();
        SetMoving(false);
        PlayAnimation(energyTriggerName, energyStateName);
        QueueEnergyProjectile();
        nextEnergyTime = Time.time + energyCooldown;
        LockAttack(Mathf.Max(0.55f, energyProjectileDelay + 0.2f));
    }

    private void DoComboAttack()
    {
        StopMoving();
        SetMoving(false);
        PlayAnimation(comboTriggerName, comboStateName);
        DealMeleeDamage();
        nextComboTime = Time.time + comboCooldown;
        nextMeleeTime = Time.time + meleeCooldown;
        LockAttack(0.65f);
    }

    private void LockAttack(float duration)
    {
        attackLockUntil = Time.time + duration;
    }

    private void QueueEnergyProjectile()
    {
        hasPendingEnergyShot = true;
        pendingEnergyShotTime = Time.time + energyProjectileDelay;
    }

    private void TryShootPendingEnergyProjectile()
    {
        if (!hasPendingEnergyShot || Time.time < pendingEnergyShotTime)
        {
            return;
        }

        hasPendingEnergyShot = false;
        ShootEnergyProjectile();
    }

    private void FacePlayer()
    {
        if (player == null)
        {
            return;
        }

        FlipToDirection(player.position.x - transform.position.x);
    }

    private void FlipToDirection(float directionX)
    {
        if (!flipWithDirection || Mathf.Abs(directionX) <= 0.01f)
        {
            return;
        }

        bool facingRight = directionX > 0f;
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = facesRightByDefault ? !facingRight : facingRight;
            return;
        }

        float scaleX = Mathf.Abs(transform.localScale.x) * (facingRight ? 1f : -1f);
        transform.localScale = new Vector3(scaleX, transform.localScale.y, transform.localScale.z);
    }

    private void SetMoving(bool isMoving)
    {
        if (animator == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(movingParameterName))
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.name != movingParameterName)
                {
                    continue;
                }

                if (parameter.type == AnimatorControllerParameterType.Bool)
                {
                    animator.SetBool(movingParameterName, isMoving);
                }
                else if (isMoving && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger(movingParameterName);
                }

                return;
            }
        }

        if (isMoving == isMovingAnimation)
        {
            return;
        }

        isMovingAnimation = isMoving;
        animator.CrossFade(isMoving ? movingStateName : idleStateName, 0f);
    }

    private void PlayAnimation(string triggerName, string fallbackStateName)
    {
        if (animator == null)
        {
            return;
        }

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

        if (currentAttackState == fallbackStateName && Time.time < attackLockUntil)
        {
            return;
        }

        currentAttackState = fallbackStateName;
        isMovingAnimation = false;
        animator.CrossFade(fallbackStateName, 0f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, energyRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, comboRange);
    }
}
