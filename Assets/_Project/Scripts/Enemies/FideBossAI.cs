using TinyDragon.Data;
using TinyDragon.Shared.Unity;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(EnemyHealth))]
public sealed class FideBossAI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;
    [SerializeField] private float detectRange = 10f;
    [SerializeField] private float stopDistance = 1.45f;
    [SerializeField] private float verticalTolerance = 3f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = GameplayBalanceDefaults.BossSpeed;
    [SerializeField] private bool flipWithDirection = true;
    [SerializeField] private bool facesRightByDefault;

    [Header("Slash Attack")]
    [SerializeField] private float meleeRange = 1.6f;
    [SerializeField] private float meleeCooldown = 1.15f;
    [SerializeField] private int meleeDamage = GameplayBalanceDefaults.BossMeleeDamage;
    [SerializeField] private float meleeDamageDelay = 0.22f;

    [Header("Energy Attack")]
    [SerializeField] private float energyRange = 7f;
    [SerializeField] private float energyCooldown = 2.5f;
    [SerializeField] private float energyProjectileDelay = 0.34f;
    [SerializeField] private int energyDamage = GameplayBalanceDefaults.BossEnergyDamage;
    [SerializeField] private float projectileSpeed = 6f;
    [SerializeField] private float projectileLifetime = 3.5f;
    [SerializeField] private float projectileScale = 0.5f;
    [SerializeField] private Vector2 projectileSpawnOffset = new Vector2(0.85f, 0.2f);
    [SerializeField] private Sprite projectileSprite;
    [SerializeField] private bool projectileFacesRightByDefault = true;

    [Header("Combo Attack")]
    [SerializeField] private bool useComboAttack = true;
    [SerializeField] private float comboRange = 2.6f;
    [SerializeField] private float comboCooldown = 4f;
    [SerializeField] private int comboDamage = 32;
    [SerializeField] private float comboDamageDelay = 0.35f;

    [Header("Energy Barrage")]
    [SerializeField] private bool useEnergyBarrage = true;
    [SerializeField] private float barrageRange = 8f;
    [SerializeField] private float barrageCooldown = 6f;
    [SerializeField] private int barrageProjectileCount = 3;
    [SerializeField] private float barrageProjectileInterval = 0.12f;
    [SerializeField] private float barrageSpreadAngle = 10f;
    [SerializeField] private int barrageDamage = 16;

    [Header("Area Burst")]
    [SerializeField] private bool useAreaBurst = true;
    [SerializeField] private float areaBurstRange = 3.2f;
    [SerializeField] private float areaBurstCooldown = 7f;
    [SerializeField] private int areaBurstDamage = 38;
    [SerializeField] private float areaBurstDelay = 0.42f;

    [Header("Teleport Special")]
    [SerializeField] private float specialTriggerRange = 11f;
    [SerializeField] private float specialCooldown = 3.5f;
    [SerializeField] private float specialTeleportDelay = 0.25f;
    [SerializeField] private float specialActionDuration = 0.6f;
    [SerializeField] private float specialLandingDistance = 1.35f;
    [SerializeField] private float specialYOffset;
    [SerializeField] private bool specialDealsDamage = true;
    [SerializeField] private int specialDamage = 18;
    [SerializeField] private float specialDamageRange = 1.9f;
    [SerializeField] private float specialDamageDelay = 0.1f;

    [Header("Phase Two")]
    [SerializeField] private bool usePhaseTwo = true;
    [SerializeField, Range(0.05f, 0.95f)] private float phaseTwoHealthPercent = 0.5f;
    [SerializeField] private float phaseTwoMoveSpeedMultiplier = 1.2f;
    [SerializeField] private float phaseTwoCooldownMultiplier = 0.75f;
    [SerializeField] private float phaseTwoProjectileSpeedMultiplier = 1.15f;
    [SerializeField] private Color phaseTwoTint = new Color(1f, 0.72f, 0.72f, 1f);

    [Header("Animation States")]
    [SerializeField] private string movingParameterName = "IsMoving";
    [SerializeField] private string slashTriggerName = "SlashAttack";
    [SerializeField] private string energyTriggerName = "EnergyBlast";
    [SerializeField] private string comboTriggerName = "ComboSlashBlast";
    [SerializeField] private string hitTriggerName = "Hit";
    [SerializeField] private string idleStateName = "FideBoss_idle";
    [SerializeField] private string moveStateName = "FideBoss_move_dash";
    [SerializeField] private string slashStateName = "FideBoss_slash_attack";
    [SerializeField] private string energyStateName = "FideBoss_energy_blast";
    [SerializeField] private string comboStateName = "FideBoss_combo_slash_blast";
    [SerializeField] private string hitStateName = "FideBoss_hit_or_special";

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private EnemyHealth enemyHealth;
    private PlayerHealth playerHealth;
    private string currentStateName;
    private float baseScaleX;
    private float nextMeleeTime;
    private float nextEnergyTime;
    private float nextComboTime;
    private float nextBarrageTime;
    private float nextAreaBurstTime;
    private float nextSpecialTime;
    private float actionLockUntil;
    private float pendingMeleeDamageTime;
    private float pendingComboDamageTime;
    private float pendingEnergyShotTime;
    private float pendingBarrageShotTime;
    private float pendingAreaBurstDamageTime;
    private float pendingSpecialTeleportTime;
    private float pendingSpecialDamageTime;
    private bool hasPendingMeleeDamage;
    private bool hasPendingComboDamage;
    private bool hasPendingEnergyShot;
    private bool hasPendingBarrageShot;
    private bool hasPendingAreaBurstDamage;
    private bool hasPendingSpecialTeleport;
    private bool hasPendingSpecialDamage;
    private int pendingBarrageShotsRemaining;
    private int pendingBarrageShotIndex;
    private bool phaseTwoActive;
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
        enemyHealth = GetComponent<EnemyHealth>();
        baseScaleX = Mathf.Abs(transform.localScale.x);
    }

    private void Start()
    {
        ResolvePlayer();
        PlayState(idleStateName, true);
    }

    private void FixedUpdate()
    {
        ResolvePlayer();
        TickPendingActions();
        UpdatePhaseState();

        if (player == null)
        {
            StopMoving();
            SetMoving(false);
            return;
        }

        if (!IsPlayerInsideArenaAwareness())
        {
            StopMoving();
            SetMoving(false);
            return;
        }

        FacePlayer();

        if (Time.time < actionLockUntil)
        {
            StopMoving();
            SetMoving(false);
            return;
        }

        float horizontalDistance = Mathf.Abs(player.position.x - transform.position.x);
        float verticalDistance = Mathf.Abs(player.position.y - transform.position.y);

        if (horizontalDistance > detectRange || verticalDistance > verticalTolerance)
        {
            if (TryStartTeleportSpecial(horizontalDistance, verticalDistance))
            {
                return;
            }

            StopMoving();
            SetMoving(false);
            return;
        }

        if (TryStartAttack(horizontalDistance))
        {
            return;
        }

        if (horizontalDistance > stopDistance)
        {
            ChasePlayer();
            return;
        }

        StopMoving();
        SetMoving(false);
    }

    private void LateUpdate()
    {
        ResolvePlayer();
        FacePlayer();
    }

    public void ApplyCombatStats(float speed, int closeDamage, int rangedDamage)
    {
        moveSpeed = Mathf.Max(speed, 0.1f);
        meleeDamage = Mathf.Max(closeDamage, 1);
        comboDamage = Mathf.Max(closeDamage + Mathf.CeilToInt(closeDamage * 0.35f), 1);
        energyDamage = Mathf.Max(rangedDamage, 1);
        barrageDamage = Mathf.Max(Mathf.RoundToInt(rangedDamage * 0.85f), 1);
        areaBurstDamage = Mathf.Max(closeDamage + rangedDamage, 1);
        specialDamage = Mathf.Max(Mathf.RoundToInt(closeDamage * 0.75f), 1);
    }

    public void PlayHit()
    {
        DoTeleportSpecial();
    }

    public void SetArenaAwareness(float horizontalRange, float verticalRange)
    {
        detectRange = Mathf.Max(detectRange, horizontalRange);
        energyRange = Mathf.Max(energyRange, horizontalRange);
        barrageRange = Mathf.Max(barrageRange, horizontalRange);
        verticalTolerance = Mathf.Max(verticalTolerance, verticalRange);
        hasArenaAwarenessBounds = false;
    }

    public void SetArenaAwarenessBounds(Bounds groundBounds, float horizontalPadding, float belowGroundTolerance, float aboveGroundTolerance)
    {
        float horizontalRange = groundBounds.size.x + horizontalPadding * 2f;
        detectRange = Mathf.Max(detectRange, horizontalRange);
        energyRange = Mathf.Max(energyRange, horizontalRange);
        barrageRange = Mathf.Max(barrageRange, horizontalRange);
        verticalTolerance = Mathf.Max(verticalTolerance, belowGroundTolerance + aboveGroundTolerance);

        arenaAwarenessMinX = groundBounds.min.x - horizontalPadding;
        arenaAwarenessMaxX = groundBounds.max.x + horizontalPadding;
        arenaAwarenessMinY = groundBounds.max.y - belowGroundTolerance;
        arenaAwarenessMaxY = groundBounds.max.y + aboveGroundTolerance;
        hasArenaAwarenessBounds = true;
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            if (playerHealth == null)
            {
                playerHealth = player.GetComponent<PlayerHealth>();
            }

            return;
        }

        PlayerController playerController = ObjectLookup.Any<PlayerController>();
        if (playerController == null)
        {
            playerHealth = null;
            return;
        }

        player = playerController.transform;
        playerHealth = player.GetComponent<PlayerHealth>();
    }

    private bool TryStartAttack(float horizontalDistance)
    {
        if (useAreaBurst && horizontalDistance <= areaBurstRange && Time.time >= nextAreaBurstTime)
        {
            DoAreaBurst();
            return true;
        }

        if (useComboAttack && horizontalDistance <= comboRange && Time.time >= nextComboTime)
        {
            DoComboAttack();
            return true;
        }

        if (horizontalDistance <= meleeRange && Time.time >= nextMeleeTime)
        {
            DoMeleeAttack();
            return true;
        }

        if (useEnergyBarrage && phaseTwoActive && horizontalDistance <= barrageRange && horizontalDistance > meleeRange && Time.time >= nextBarrageTime)
        {
            DoEnergyBarrage();
            return true;
        }

        if (horizontalDistance <= energyRange && horizontalDistance > meleeRange && Time.time >= nextEnergyTime)
        {
            DoEnergyAttack();
            return true;
        }

        return false;
    }

    private bool TryStartTeleportSpecial(float horizontalDistance, float verticalDistance)
    {
        if (Time.time < nextSpecialTime)
        {
            return false;
        }

        if (horizontalDistance > specialTriggerRange || verticalDistance > verticalTolerance * 2f)
        {
            return false;
        }

        DoTeleportSpecial();
        return true;
    }

    private void DoMeleeAttack()
    {
        StopMoving();
        FacePlayer();
        SetMoving(false);
        PlayAction(slashTriggerName, slashStateName);

        hasPendingMeleeDamage = true;
        pendingMeleeDamageTime = Time.time + meleeDamageDelay;
        nextMeleeTime = Time.time + CurrentCooldown(meleeCooldown);
        LockAction(0.5f);
    }

    private void DoEnergyAttack()
    {
        StopMoving();
        FacePlayer();
        SetMoving(false);
        PlayAction(energyTriggerName, energyStateName);

        hasPendingEnergyShot = true;
        pendingEnergyShotTime = Time.time + energyProjectileDelay;
        nextEnergyTime = Time.time + CurrentCooldown(energyCooldown);
        LockAction(Mathf.Max(0.7f, energyProjectileDelay + 0.25f));
    }

    private void DoComboAttack()
    {
        StopMoving();
        FacePlayer();
        SetMoving(false);
        PlayAction(comboTriggerName, comboStateName);

        hasPendingComboDamage = true;
        pendingComboDamageTime = Time.time + comboDamageDelay;
        nextComboTime = Time.time + CurrentCooldown(comboCooldown);
        nextMeleeTime = Time.time + CurrentCooldown(meleeCooldown);
        LockAction(0.9f);
    }

    private void DoEnergyBarrage()
    {
        StopMoving();
        FacePlayer();
        SetMoving(false);
        PlayAction(energyTriggerName, energyStateName);

        pendingBarrageShotsRemaining = Mathf.Max(barrageProjectileCount, 1);
        pendingBarrageShotIndex = 0;
        hasPendingBarrageShot = true;
        pendingBarrageShotTime = Time.time + energyProjectileDelay;
        nextBarrageTime = Time.time + CurrentCooldown(barrageCooldown);
        nextEnergyTime = Mathf.Max(nextEnergyTime, Time.time + CurrentCooldown(energyCooldown * 0.55f));
        LockAction(Mathf.Max(0.85f, energyProjectileDelay + barrageProjectileInterval * pendingBarrageShotsRemaining + 0.15f));
    }

    private void DoAreaBurst()
    {
        StopMoving();
        FacePlayer();
        SetMoving(false);
        PlayAction(hitTriggerName, hitStateName);

        hasPendingAreaBurstDamage = true;
        pendingAreaBurstDamageTime = Time.time + areaBurstDelay;
        nextAreaBurstTime = Time.time + CurrentCooldown(areaBurstCooldown);
        nextComboTime = Mathf.Max(nextComboTime, Time.time + CurrentCooldown(comboCooldown * 0.45f));
        LockAction(Mathf.Max(0.75f, areaBurstDelay + 0.2f));
    }

    private void DoTeleportSpecial()
    {
        StopMoving();
        FacePlayer();
        SetMoving(false);
        PlayAction(hitTriggerName, hitStateName);

        hasPendingSpecialTeleport = true;
        pendingSpecialTeleportTime = Time.time + specialTeleportDelay;
        hasPendingSpecialDamage = specialDealsDamage;
        pendingSpecialDamageTime = Time.time + specialTeleportDelay + specialDamageDelay;
        nextSpecialTime = Time.time + CurrentCooldown(specialCooldown);
        LockAction(Mathf.Max(specialActionDuration, specialTeleportDelay + specialDamageDelay + 0.15f));
    }

    private void LockAction(float duration)
    {
        actionLockUntil = Time.time + duration;
    }

    private void TickPendingActions()
    {
        if (hasPendingMeleeDamage && Time.time >= pendingMeleeDamageTime)
        {
            hasPendingMeleeDamage = false;
            DealMeleeDamage(meleeDamage, meleeRange);
        }

        if (hasPendingComboDamage && Time.time >= pendingComboDamageTime)
        {
            hasPendingComboDamage = false;
            DealMeleeDamage(comboDamage, comboRange);
        }

        if (hasPendingEnergyShot && Time.time >= pendingEnergyShotTime)
        {
            hasPendingEnergyShot = false;
            ShootEnergyProjectile();
        }

        if (hasPendingBarrageShot && Time.time >= pendingBarrageShotTime)
        {
            ShootBarrageProjectile(pendingBarrageShotIndex);
            pendingBarrageShotIndex++;
            pendingBarrageShotsRemaining--;

            if (pendingBarrageShotsRemaining > 0)
            {
                pendingBarrageShotTime = Time.time + barrageProjectileInterval;
            }
            else
            {
                hasPendingBarrageShot = false;
            }
        }

        if (hasPendingAreaBurstDamage && Time.time >= pendingAreaBurstDamageTime)
        {
            hasPendingAreaBurstDamage = false;
            DealMeleeDamage(areaBurstDamage, areaBurstRange);
        }

        if (hasPendingSpecialTeleport && Time.time >= pendingSpecialTeleportTime)
        {
            hasPendingSpecialTeleport = false;
            TeleportNearPlayer();
        }

        if (hasPendingSpecialDamage && Time.time >= pendingSpecialDamageTime)
        {
            hasPendingSpecialDamage = false;
            DealMeleeDamage(specialDamage, specialDamageRange);
        }
    }

    private void DealMeleeDamage(int damage, float range)
    {
        if (player == null || playerHealth == null || !IsPlayerInsideArenaAwareness())
        {
            return;
        }

        float horizontalDistance = Mathf.Abs(player.position.x - transform.position.x);
        float verticalDistance = Mathf.Abs(player.position.y - transform.position.y);
        if (horizontalDistance <= range && verticalDistance <= verticalTolerance)
        {
            playerHealth.TakeDamage(damage);
        }
    }

    private void ShootEnergyProjectile()
    {
        if (player == null)
        {
            return;
        }

        float facingDirection = player.position.x >= transform.position.x ? 1f : -1f;
        Vector3 spawnOffset = new Vector3(projectileSpawnOffset.x * facingDirection, projectileSpawnOffset.y, 0f);
        Vector3 spawnPosition = transform.position + spawnOffset;
        Vector2 projectileDirection = new Vector2(facingDirection, 0f);

        GameObject projectileObject = new GameObject("Fide Energy Projectile");
        projectileObject.transform.position = spawnPosition;

        EnemyProjectile projectile = projectileObject.AddComponent<EnemyProjectile>();
        projectile.Initialize(
            projectileDirection,
            CurrentProjectileSpeed(),
            energyDamage,
            projectileLifetime,
            projectileSprite,
            projectileScale,
            projectileFacesRightByDefault
        );
    }

    private void ShootBarrageProjectile(int shotIndex)
    {
        if (player == null)
        {
            return;
        }

        float facingDirection = player.position.x >= transform.position.x ? 1f : -1f;
        Vector3 spawnOffset = new Vector3(projectileSpawnOffset.x * facingDirection, projectileSpawnOffset.y, 0f);
        Vector3 spawnPosition = transform.position + spawnOffset;
        int projectileCount = Mathf.Max(barrageProjectileCount, 1);
        float middle = (projectileCount - 1) * 0.5f;
        float angle = (shotIndex - middle) * barrageSpreadAngle;
        Vector2 baseDirection = new Vector2(facingDirection, 0f);
        Vector2 projectileDirection = Rotate(baseDirection, angle);

        GameObject projectileObject = new GameObject("Fide Barrage Projectile");
        projectileObject.transform.position = spawnPosition;

        EnemyProjectile projectile = projectileObject.AddComponent<EnemyProjectile>();
        projectile.Initialize(
            projectileDirection,
            CurrentProjectileSpeed(),
            barrageDamage,
            projectileLifetime,
            projectileSprite,
            projectileScale,
            projectileFacesRightByDefault
        );
    }

    private void TeleportNearPlayer()
    {
        if (player == null)
        {
            return;
        }

        float sideFromPlayer = transform.position.x <= player.position.x ? -1f : 1f;
        Vector3 landingPosition = new Vector3(
            player.position.x + sideFromPlayer * specialLandingDistance,
            player.position.y + specialYOffset,
            transform.position.z
        );

        rb.linearVelocity = Vector2.zero;
        rb.position = landingPosition;
        transform.position = landingPosition;
        FacePlayer();
    }

    private void ChasePlayer()
    {
        float direction = Mathf.Sign(player.position.x - transform.position.x);
        rb.linearVelocity = new Vector2(direction * CurrentMoveSpeed(), rb.linearVelocity.y);
        SetMoving(true);
    }

    private void StopMoving()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
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
            spriteRenderer.flipX = false;
        }

        bool usePositiveScale = facesRightByDefault ? facingRight : !facingRight;
        float scaleX = baseScaleX * (usePositiveScale ? 1f : -1f);
        transform.localScale = new Vector3(scaleX, transform.localScale.y, transform.localScale.z);
    }

    private void PlayState(string stateName, bool force = false)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        if (!force && currentStateName == stateName)
        {
            return;
        }

        currentStateName = stateName;
        animator.CrossFade(stateName, 0f);
    }

    private void SetMoving(bool isMoving)
    {
        if (animator == null)
        {
            return;
        }

        if (HasParameter(movingParameterName, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(movingParameterName, isMoving);
            currentStateName = isMoving ? moveStateName : idleStateName;
            return;
        }

        PlayState(isMoving ? moveStateName : idleStateName);
    }

    private void PlayAction(string triggerName, string fallbackStateName)
    {
        if (animator == null)
        {
            return;
        }

        if (HasParameter(triggerName, AnimatorControllerParameterType.Trigger))
        {
            ResetTriggerIfPresent(slashTriggerName);
            ResetTriggerIfPresent(energyTriggerName);
            ResetTriggerIfPresent(comboTriggerName);
            ResetTriggerIfPresent(hitTriggerName);
            animator.SetTrigger(triggerName);
            currentStateName = fallbackStateName;
            return;
        }

        PlayState(fallbackStateName, true);
    }

    private void ResetTriggerIfPresent(string triggerName)
    {
        if (HasParameter(triggerName, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(triggerName);
        }
    }

    private bool HasParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
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

    private void UpdatePhaseState()
    {
        if (!usePhaseTwo || phaseTwoActive || enemyHealth == null || enemyHealth.MaxHealth <= 0)
        {
            return;
        }

        float healthPercent = enemyHealth.CurrentHealth / (float)enemyHealth.MaxHealth;
        if (healthPercent > phaseTwoHealthPercent)
        {
            return;
        }

        phaseTwoActive = true;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = phaseTwoTint;
        }

        nextBarrageTime = Mathf.Min(nextBarrageTime, Time.time + 0.35f);
        nextAreaBurstTime = Mathf.Min(nextAreaBurstTime, Time.time + 0.55f);
        nextSpecialTime = Mathf.Min(nextSpecialTime, Time.time + 0.75f);
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

    private float CurrentMoveSpeed()
    {
        return phaseTwoActive ? moveSpeed * Mathf.Max(phaseTwoMoveSpeedMultiplier, 0.1f) : moveSpeed;
    }

    private float CurrentCooldown(float baseCooldown)
    {
        float multiplier = phaseTwoActive ? phaseTwoCooldownMultiplier : 1f;
        return Mathf.Max(baseCooldown * Mathf.Max(multiplier, 0.1f), 0.05f);
    }

    private float CurrentProjectileSpeed()
    {
        return phaseTwoActive ? projectileSpeed * Mathf.Max(phaseTwoProjectileSpeedMultiplier, 0.1f) : projectileSpeed;
    }

    private static Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos
        ).normalized;
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

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, specialTriggerRange);

        Gizmos.color = new Color(1f, 0.45f, 0f, 1f);
        Gizmos.DrawWireSphere(transform.position, areaBurstRange);
    }
}
