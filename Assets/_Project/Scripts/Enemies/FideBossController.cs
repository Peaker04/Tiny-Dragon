using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Runtime combat controller for Fide Dai Ca 3.  It deliberately uses the NRO
/// source-status playback model: every skill advances one pose every 0.05 seconds.
/// Attach this instead of FideBossAI to a Fide object with EnemyHealth.
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]
public sealed class FideBossController : MonoBehaviour
{
    private const float SkillTick = 0.05f;
    private static readonly int[] RunFrames = { 2, 3, 4, 5, 6 };
    private static readonly int[] FlightFrames = { 7, 8 };
    private static readonly int[] MeleeFrames = { 9, 10, 11, 12 };
    private static readonly int[] SuperDashFrames = { 13, 14 };
    private static readonly int[] RapidComboFrames = { 13, 14, 15, 16, 17 };
    private static readonly int[] ChargeFrames = { 18, 19, 20, 21, 22 };
    private const int TeleportFrame = 25;
    private static readonly int[] HurtFrames = { 23, 24 };
    private static readonly int[] BarrageFrames = { 26, 27, 28, 29, 30 };
    private static readonly int[] HeavyFrames = { 30, 31, 32 };

    private enum FideSkill { BasicPunch, Dragon, Antomic, Masenko, Galick, DeathBeam, AfterimageDash, GravityCage, MeteorBarrage, PlanetBreaker, CounterStance, AerialDive, SkyRush, VanishingRush, DeathBeamBarrage, TeleportCross, NovaBurst, DeathSaucerStorm }
    private enum CueAction { None, Warning, HitCircle, Projectile, Beam, DashBehind, Cage, Meteors, UltimateRing, CounterWindow, AerialDive, SkyRush, VanishingRush, BeamBarrage, TeleportCross, NovaBurst, SpiralStorm }

    private readonly struct Cue
    {
        public readonly int Step;
        public readonly int EffectId;
        public readonly Vector2 Offset;
        public readonly CueAction Action;
        public readonly float Radius;
        public readonly int Damage;

        public Cue(int step, int effectId, Vector2 offset, CueAction action = CueAction.None, float radius = 0f, int damage = 0)
        {
            Step = step; EffectId = effectId; Offset = offset; Action = action; Radius = radius; Damage = damage;
        }
    }

    private sealed class SkillDefinition
    {
        public readonly FideSkill Id;
        public readonly string Label;
        public readonly int Phase;
        public readonly float MinRange;
        public readonly float MaxRange;
        public readonly float Cooldown;
        public readonly int[] StatusFrames;
        public readonly Cue[] Cues;

        public SkillDefinition(FideSkill id, string label, int phase, float minRange, float maxRange, float cooldown, int[] statusFrames, Cue[] cues)
        {
            Id = id; Label = label; Phase = phase; MinRange = minRange; MaxRange = maxRange; Cooldown = cooldown; StatusFrames = statusFrames; Cues = cues;
        }
    }

    [Header("Target / Arena")]
    [SerializeField] private Transform player;
    [SerializeField] private float arenaMinX = -9f;
    [SerializeField] private float arenaMaxX = 9f;
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float globalCooldown = 0.3f;
    [SerializeField] private float facingDeadZone = .35f;
    [SerializeField] private float facingChangeLock = .12f;
    [SerializeField] private float repositionDuration = .28f;
    [SerializeField] private bool disableLegacyFideAi = true;
    [SerializeField] private bool disableFrameBridge = true;
    [SerializeField] private bool disableLegacyAnimator = true;

    [Header("Boss Difficulty")]
    [SerializeField] private bool enforceBossHealth = true;
    [SerializeField, Min(1)] private int bossMaxHealth = 1500;
    [SerializeField, Range(.1f, .95f)] private float phaseTwoHealthPercent = .70f;
    [SerializeField, Range(.05f, .8f)] private float phaseThreeHealthPercent = .40f;

    [Header("Aerial Pressure")]
    [SerializeField] private float hoverHeight = 0.45f;
    [SerializeField] private float aerialHeight = 3.1f;
    [SerializeField] private float aerialMoveSpeed = 17f;
    [SerializeField] private float aerialDiveWidth = 1.1f;

    [Header("Combat")]
    [SerializeField] private float playerHitInvulnerability = 0.22f;
    [SerializeField] private float projectileSpeed = 10f;
    [SerializeField] private float projectileLifetime = 3.5f;
    [SerializeField] private float maxHorizontalKnockbackSpeed = 7f;
    [SerializeField] private float knockbackLift = 1.25f;
    [SerializeField] private float maxVerticalSpeedAfterHit = 4f;

    [Header("Debug")]
    [SerializeField] private bool showDebugHud = true;
    [SerializeField] private bool keyboardSkillTesting = true;
    [SerializeField] private bool aiEnabled = true;

    private readonly List<Sprite> rightFrames = new List<Sprite>(33);
    private readonly List<Sprite> leftFrames = new List<Sprite>(33);
    private readonly Dictionary<int, Sprite[]> effectFrames = new Dictionary<int, Sprite[]>();
    private readonly Dictionary<FideSkill, SkillDefinition> skills = new Dictionary<FideSkill, SkillDefinition>();
    private readonly Dictionary<FideSkill, float> cooldownEnds = new Dictionary<FideSkill, float>();
    private readonly Queue<FideSkill> recentSkills = new Queue<FideSkill>();
    private SpriteRenderer spriteRenderer;
    private Animator legacyAnimator;
    private Rigidbody2D bossBody;
    private Collider2D bossCollider;
    private EnemyHealth health;
    private PlayerHealth playerHealth;
    private Coroutine activeRoutine;
    private FideSkill currentSkill;
    private string currentSkillLabel = "Hunting";
    private bool facingRight = true;
    private bool counterWindowActive;
    private bool isCasting;
    private float counterWindowEnd;
    private float nextPlayerDamageTime;
    private float nextAiDecisionTime;
    private float closeTime;
    private float farTime;
    private float groundY;
    private float actionLockUntil;
    private float nextHurtReactionTime;
    private float nextFacingChangeTime;
    private float repositionUntil;
    private int currentFrameIndex;
    private int phase = 1;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
        legacyAnimator = GetComponent<Animator>();
        bossBody = GetComponent<Rigidbody2D>();
        bossCollider = GetComponent<Collider2D>();
        health = GetComponent<EnemyHealth>();
        groundY = transform.position.y;
        bossBody.bodyType = RigidbodyType2D.Kinematic;
        bossBody.gravityScale = 0f;
        bossBody.linearVelocity = Vector2.zero;
        bossBody.freezeRotation = true;
        if (bossCollider != null) bossCollider.isTrigger = true;
        if (disableLegacyFideAi)
        {
            FideBossAI legacy = GetComponent<FideBossAI>();
            if (legacy != null) legacy.enabled = false;
        }
        if (disableFrameBridge)
        {
            FideFrameAnimationBridge bridge = GetComponent<FideFrameAnimationBridge>();
            if (bridge != null) bridge.enabled = false;
        }
        if (disableLegacyAnimator && legacyAnimator != null)
        {
            legacyAnimator.enabled = false;
        }

        LoadFrames("Enemies/Fide/FideDaiCa3", rightFrames);
        LoadFrames("Enemies/Fide/FideDaiCa3/left", leftFrames);
        BuildSkillData();
        health.Damaged += OnBossDamaged;
        ShowFrame(0);
    }

    private void Start()
    {
        if (enforceBossHealth && health != null)
        {
            health.ApplyBalance("Fide Dai Ca 3", bossMaxHealth);
        }
    }

    private void OnDestroy()
    {
        if (health != null) health.Damaged -= OnBossDamaged;
    }

    private void Update()
    {
        ResolvePlayer();
        UpdatePhase();
        HandleKeyboardTest();
        if (player == null || isCasting || Time.time < actionLockUntil) return;

        float distance = Vector2.Distance(transform.position, player.position);
        closeTime = distance < 2.2f ? closeTime + Time.deltaTime : 0f;
        farTime = distance > 8f ? farTime + Time.deltaTime : 0f;
        FacePlayer();

        if (aiEnabled && Time.time < repositionUntil)
        {
            RepositionAroundPlayer(distance);
            return;
        }

        if (aiEnabled && Time.time >= nextAiDecisionTime)
        {
            nextAiDecisionTime = Time.time + (phase == 3 ? .045f : phase == 2 ? .065f : .085f);
            SkillDefinition next = ChooseSkill(distance);
            if (next != null) StartSkill(next);
            else MoveTowardsPlayer(distance);
        }
        else if (!aiEnabled)
        {
            ShowIdle();
        }
    }

    private void ResolvePlayer()
    {
        if (player != null && playerHealth != null) return;
        playerHealth = FindAnyObjectByType<PlayerHealth>();
        if (playerHealth != null) player = playerHealth.transform;
    }

    private void UpdatePhase()
    {
        if (health == null || health.MaxHealth <= 0) return;
        float ratio = (float)health.CurrentHealth / health.MaxHealth;
        int nextPhase = ratio <= phaseThreeHealthPercent ? 3 : ratio <= phaseTwoHealthPercent ? 2 : 1;
        if (nextPhase == phase) return;
        phase = nextPhase;
        currentSkillLabel = $"PHASE {phase}";
        if (spriteRenderer != null) spriteRenderer.color = phase == 3 ? new Color(1f, .55f, .55f) : new Color(1f, .82f, .82f);
        SpawnWarning(transform.position, 2.3f, .6f, phase == 3 ? new Color(1f, .2f, .15f) : new Color(1f, .6f, .15f));
    }

    private SkillDefinition ChooseSkill(float distance)
    {
        // Anti-cheese rules have priority, but every option remains telegraphed.
        if (distance > 2.35f && TryGetAvailable(FideSkill.AfterimageDash, out SkillDefinition teleportAssault)) return teleportAssault;
        if (distance <= 2.35f && TryGetAvailable(FideSkill.BasicPunch, out SkillDefinition basicPunch)) return basicPunch;
        if (farTime > 2f && TryGetAvailable(FideSkill.Galick, out SkillDefinition galick)) return galick;
        if (closeTime > 1.6f && phase >= 2 && TryGetAvailable(FideSkill.CounterStance, out SkillDefinition counter)) return counter;

        List<SkillDefinition> choices = skills.Values
            .Where(skill => skill.Phase <= phase && distance >= skill.MinRange && distance <= skill.MaxRange && IsAvailable(skill))
            .OrderBy(_ => UnityEngine.Random.value)
            .ToList();
        return choices.Count == 0 ? null : choices[0];
    }

    private bool TryGetAvailable(FideSkill id, out SkillDefinition skill)
    {
        return skills.TryGetValue(id, out skill) && skill.Phase <= phase && IsAvailable(skill);
    }

    private bool IsAvailable(SkillDefinition skill)
    {
        if (Time.time < cooldownEnds.GetValueOrDefault(skill.Id)) return false;
        FideSkill[] history = recentSkills.ToArray();
        return history.Length < 2 || history[history.Length - 1] != skill.Id || history[history.Length - 2] != skill.Id;
    }

    private void StartSkill(SkillDefinition skill)
    {
        if (isCasting || !IsAvailable(skill)) return;
        activeRoutine = StartCoroutine(PlaySkill(skill));
    }

    private IEnumerator PlaySkill(SkillDefinition skill)
    {
        isCasting = true;
        currentSkill = skill.Id;
        currentSkillLabel = skill.Label;
        cooldownEnds[skill.Id] = Time.time + skill.Cooldown * PhaseCooldownMultiplier();
        recentSkills.Enqueue(skill.Id);
        while (recentSkills.Count > 3) recentSkills.Dequeue();

        for (int step = 0; step < skill.StatusFrames.Length; step++)
        {
            ShowFrame(skill.StatusFrames[step]);
            foreach (Cue cue in skill.Cues)
            {
                if (cue.Step != step) continue;
                SpawnSourceEffect(cue.EffectId, cue.Offset);
                ExecuteCue(skill, cue);
            }
            yield return new WaitForSeconds(SkillTick);
        }

        counterWindowActive = false;
        isCasting = false;
        nextAiDecisionTime = Time.time + globalCooldown * PhaseCooldownMultiplier();
        repositionUntil = Time.time + repositionDuration * (phase == 3 ? .55f : phase == 2 ? .75f : 1f);
        ShowIdle();
    }

    private void ExecuteCue(SkillDefinition skill, Cue cue)
    {
        Vector2 target = player != null ? player.position : transform.position + Vector3.right * (facingRight ? 1f : -1f);
        switch (cue.Action)
        {
            case CueAction.Warning:
                SpawnWarning(target + FacingOffset(cue.Offset / 32f), cue.Radius, .35f, new Color(1f, .8f, .15f));
                break;
            case CueAction.HitCircle:
                DamagePlayerInCircle((Vector2)transform.position + FacingOffset(cue.Offset / 32f), cue.Radius, cue.Damage, 7f);
                break;
            case CueAction.Projectile:
                SpawnProjectile((target - (Vector2)transform.position).normalized, cue.Damage, cue.Radius, cue.EffectId);
                break;
            case CueAction.Beam:
                StartCoroutine(BeamAfterWarning(target, cue.Radius, cue.Damage, skill.Id == FideSkill.DeathBeam ? .12f : .28f));
                break;
            case CueAction.DashBehind:
                DashBehindPlayer();
                break;
            case CueAction.Cage:
                StartCoroutine(CreateGravityCage(target, cue.Damage));
                break;
            case CueAction.Meteors:
                StartCoroutine(MeteorWaves(target, phase == 3 ? 4 : 3, cue.Damage));
                break;
            case CueAction.UltimateRing:
                StartCoroutine(PlanetBreaker(target, cue.Damage));
                break;
            case CueAction.CounterWindow:
                counterWindowActive = true;
                counterWindowEnd = Time.time + .55f;
                SpawnWarning(transform.position, 1.8f, .55f, new Color(.7f, .25f, 1f));
                break;
            case CueAction.AerialDive:
                StartAerialDive(1, cue.Damage);
                break;
            case CueAction.SkyRush:
                StartAerialDive(phase == 3 ? 4 : 3, cue.Damage);
                break;
            case CueAction.VanishingRush:
                StartAerialDive(5, cue.Damage);
                break;
            case CueAction.BeamBarrage:
                StartCoroutine(DeathBeamBarrage(cue.Damage));
                break;
            case CueAction.TeleportCross:
                StartCoroutine(TeleportCross(cue.Damage));
                break;
            case CueAction.NovaBurst:
                StartCoroutine(NovaBurst(cue.Damage));
                break;
            case CueAction.SpiralStorm:
                StartCoroutine(SpiralStorm(cue.Damage));
                break;
        }
    }

    private IEnumerator BeamAfterWarning(Vector2 target, float width, int damage, float warningDuration)
    {
        Vector2 origin = (Vector2)transform.position + FacingOffset(new Vector2(.65f, .15f));
        Vector2 direction = (target - origin).normalized;
        float length = 11f;
        Vector2 center = origin + direction * length * .5f;
        SpawnLine(center, direction, length, width, warningDuration, new Color(1f, .75f, .15f, .72f));
        yield return new WaitForSeconds(warningDuration);
        SpawnLine(center, direction, length, width * 1.35f, .16f, new Color(1f, .15f, .85f, .9f));
        DamagePlayerInBeam(origin, direction, length, width, damage);
    }

    private IEnumerator CreateGravityCage(Vector2 center, int damage)
    {
        const int zones = 6;
        for (int i = 0; i < zones; i++)
        {
            float angle = i * Mathf.PI * 2f / zones;
            Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 2.1f;
            SpawnWarning(point, .72f, .7f, new Color(.7f, .25f, 1f));
        }
        yield return new WaitForSeconds(.7f);
        for (int i = 0; i < zones; i++)
        {
            float angle = i * Mathf.PI * 2f / zones;
            Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 2.1f;
            DamagePlayerInCircle(point, .72f, damage, 0f);
        }
    }

    private IEnumerator MeteorWaves(Vector2 target, int waves, int damage)
    {
        for (int wave = 0; wave < waves; wave++)
        {
            Vector2 safeGap = new Vector2(UnityEngine.Random.Range(-1.1f, 1.1f), 0f);
            for (int i = -3; i <= 3; i++)
            {
                Vector2 point = target + new Vector2(i * .85f, UnityEngine.Random.Range(-.45f, .45f));
                if (Vector2.Distance(point, target + safeGap) < .7f) continue;
                SpawnWarning(point, .34f, .4f, new Color(1f, .35f, .1f));
                StartCoroutine(MeteorImpact(point, damage));
            }
            yield return new WaitForSeconds(.32f);
        }
    }

    private IEnumerator MeteorImpact(Vector2 point, int damage)
    {
        yield return new WaitForSeconds(.4f);
        SpawnWarning(point, .42f, .12f, new Color(1f, .1f, .05f));
        DamagePlayerInCircle(point, .42f, damage, 0f);
    }

    private IEnumerator PlanetBreaker(Vector2 target, int damage)
    {
        float[] radii = { 1.4f, 2.7f, 4.1f };
        for (int i = 0; i < radii.Length; i++)
        {
            SpawnWarning(target, radii[i], .9f, new Color(1f, .1f, .25f));
            yield return new WaitForSeconds(.28f);
        }
        yield return new WaitForSeconds(.1f);
        for (int i = 0; i < radii.Length; i++) DamagePlayerInRing(target, radii[i], .45f, damage + i * 8);
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.PI * 2f / 12f;
            SpawnProjectile(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), damage / 2, .18f, 35);
        }
    }

    private IEnumerator DeathBeamBarrage(int damage)
    {
        int shots = phase == 3 ? 7 : 5;
        actionLockUntil = Mathf.Max(actionLockUntil, Time.time + shots * .14f + .2f);
        for (int shot = 0; shot < shots; shot++)
        {
            if (player == null) yield break;
            Vector2 origin = (Vector2)transform.position + FacingOffset(new Vector2(.65f, .15f));
            Vector2 direction = ((Vector2)player.position - origin).normalized;
            SpawnLine(origin + direction * 5.5f, direction, 11f, .14f, .07f, new Color(1f, .2f, .8f, .9f));
            DamagePlayerInBeam(origin, direction, 11f, .18f, damage);
            ShowFrame(HeavyFrames[shot % HeavyFrames.Length]);
            yield return new WaitForSeconds(.14f);
        }
    }

    private IEnumerator TeleportCross(int damage)
    {
        const int strikes = 3;
        actionLockUntil = Mathf.Max(actionLockUntil, Time.time + 1.25f);
        for (int strike = 0; strike < strikes; strike++)
        {
            if (player == null) yield break;
            float side = strike % 2 == 0 ? -1f : 1f;
            Vector3 destination = player.position + Vector3.right * side * 1.35f;
            destination.x = Mathf.Clamp(destination.x, arenaMinX, arenaMaxX);
            ShowFrame(TeleportFrame);
            SpawnAfterimage();
            transform.position = destination;
            FacePlayerImmediate();
            SpawnWarning(player.position, 1.35f, .16f, new Color(.85f, .25f, 1f));
            yield return new WaitForSeconds(.12f);
            ShowFrame(SuperDashFrames[strike % SuperDashFrames.Length]);
            DamagePlayerInCircle(player.position, 1.45f, damage + strike * 5, 11f);
            yield return new WaitForSeconds(.22f);
        }
    }

    private IEnumerator NovaBurst(int damage)
    {
        actionLockUntil = Mathf.Max(actionLockUntil, Time.time + .75f);
        Vector2 center = transform.position;
        SpawnWarning(center, 2.5f, .45f, new Color(1f, .2f, .5f));
        yield return new WaitForSeconds(.45f);
        DamagePlayerInCircle(center, 2.5f, damage, 12f);
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.PI * 2f / 12f;
            SpawnProjectile(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), damage / 2, .16f, 35);
        }
    }

    private IEnumerator SpiralStorm(int damage)
    {
        const int waves = 3;
        actionLockUntil = Mathf.Max(actionLockUntil, Time.time + 1.15f);
        for (int wave = 0; wave < waves; wave++)
        {
            float offset = wave * .48f;
            for (int i = 0; i < 10; i++)
            {
                float angle = offset + i * Mathf.PI * 2f / 10f;
                SpawnProjectile(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), damage, .15f, 35);
            }
            ShowFrame(BarrageFrames[wave % BarrageFrames.Length]);
            yield return new WaitForSeconds(.22f);
        }
    }

    private void DashBehindPlayer()
    {
        if (player == null) return;
        float direction = facingRight ? 1f : -1f;
        Vector3 point = player.position + Vector3.right * direction * 1.15f;
        point.x = Mathf.Clamp(point.x, arenaMinX, arenaMaxX);
        transform.position = point;
        FacePlayerImmediate();
        ShowFrame(TeleportFrame);
        SpawnWarning(transform.position, 1.2f, .15f, new Color(.8f, .8f, 1f));
    }

    private void StartAerialDive(int hits, int damage)
    {
        actionLockUntil = Mathf.Max(actionLockUntil, Time.time + .7f + hits * .42f);
        StartCoroutine(AerialDiveRoutine(hits, damage));
    }

    private IEnumerator AerialDiveRoutine(int hits, int damage)
    {
        if (player == null) yield break;
        currentSkillLabel = hits > 1 ? "SKY RUSH" : "AERIAL DIVE";
        Vector2 risePoint = new Vector2(transform.position.x, groundY + aerialHeight);
        SpawnAfterimage();
        yield return MoveTo(risePoint, .18f);

        for (int hit = 0; hit < hits; hit++)
        {
            if (player == null) break;
            Vector2 target = new Vector2(player.position.x, groundY + .25f);
            SpawnWarning(target, aerialDiveWidth, .22f, new Color(1f, .35f, .1f));
            yield return new WaitForSeconds(.16f);
            ShowFrame(TeleportFrame);
            SpawnAfterimage();
            yield return MoveTo(target, .12f, true);
            ShowFrame(MeleeFrames[hit % MeleeFrames.Length]);
            DamagePlayerInCircle(target, aerialDiveWidth, damage + hit * 4, 10f);
            SpawnWarning(target, aerialDiveWidth * 1.2f, .12f, new Color(1f, .1f, .25f));

            if (hit < hits - 1)
            {
                Vector2 nextRise = new Vector2(Mathf.Clamp(target.x + (facingRight ? -1.8f : 1.8f), arenaMinX, arenaMaxX), groundY + aerialHeight);
                yield return MoveTo(nextRise, .1f);
            }
        }

        yield return MoveTo(new Vector2(transform.position.x, groundY + hoverHeight), .16f);
    }

    private IEnumerator MoveTo(Vector2 destination, float duration, bool superDash = false)
    {
        Vector2 start = transform.position;
        duration = Mathf.Max(duration, Vector2.Distance(start, destination) / Mathf.Max(aerialMoveSpeed, 1f));
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            transform.position = Vector2.Lerp(start, destination, progress);
            int[] frames = superDash ? SuperDashFrames : FlightFrames;
            ShowFrame(frames[Mathf.FloorToInt(elapsed / SkillTick) % frames.Length]);
            yield return null;
        }
        transform.position = destination;
    }

    private void SpawnAfterimage()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null) return;
        GameObject image = new GameObject("FideAfterimage");
        image.transform.position = transform.position;
        image.transform.localScale = transform.lossyScale;
        SpriteRenderer renderer = image.AddComponent<SpriteRenderer>();
        renderer.sprite = spriteRenderer.sprite;
        renderer.flipX = spriteRenderer.flipX;
        renderer.color = new Color(.85f, .5f, 1f, .48f);
        renderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        Destroy(image, .18f);
    }

    private void SpawnProjectile(Vector2 direction, int damage, float scale, int effectId)
    {
        GameObject projectile = new GameObject("FideProjectile");
        projectile.transform.position = transform.position + (Vector3)(direction * .65f);
        SpriteRenderer renderer = projectile.AddComponent<SpriteRenderer>();
        renderer.sprite = GetEffectFrames(effectId).FirstOrDefault() ?? RuntimeSprite.White;
        renderer.color = new Color(1f, .3f, .9f);
        renderer.sortingOrder = 40;
        CircleCollider2D collider = projectile.AddComponent<CircleCollider2D>();
        collider.isTrigger = true; collider.radius = Mathf.Max(.14f, scale);
        Rigidbody2D body = projectile.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f; body.freezeRotation = true; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        FideBossProjectile behaviour = projectile.AddComponent<FideBossProjectile>();
        float speedMultiplier = phase == 3 ? 1.55f : phase == 2 ? 1.25f : 1f;
        behaviour.Initialize(direction, projectileSpeed * speedMultiplier, damage, projectileLifetime, playerHitInvulnerability);
        projectile.transform.localScale = Vector3.one * Mathf.Max(.45f, scale * 2.2f);
    }

    private void DamagePlayerInCircle(Vector2 point, float radius, int damage, float knockback)
    {
        if (playerHealth == null || Time.time < nextPlayerDamageTime) return;
        if (Vector2.Distance(playerHealth.transform.position, point) > radius + .45f) return;
        DealDamage(damage, (playerHealth.transform.position - transform.position).normalized * knockback);
    }

    private void DamagePlayerInRing(Vector2 center, float radius, float thickness, int damage)
    {
        if (playerHealth == null || Time.time < nextPlayerDamageTime) return;
        float distance = Vector2.Distance(playerHealth.transform.position, center);
        if (Mathf.Abs(distance - radius) <= thickness) DealDamage(damage, ((Vector2)playerHealth.transform.position - center).normalized * 9f);
    }

    private void DamagePlayerInBeam(Vector2 origin, Vector2 direction, float length, float width, int damage)
    {
        if (playerHealth == null || Time.time < nextPlayerDamageTime) return;
        Vector2 delta = (Vector2)playerHealth.transform.position - origin;
        float along = Vector2.Dot(delta, direction);
        float sideways = Mathf.Abs(Vector2.Perpendicular(direction).x * delta.x + Vector2.Perpendicular(direction).y * delta.y);
        if (along >= 0f && along <= length && sideways <= width) DealDamage(damage, direction * 10f);
    }

    private void DealDamage(int damage, Vector2 knockback)
    {
        if (playerHealth == null) return;
        nextPlayerDamageTime = Time.time + playerHitInvulnerability;
        playerHealth.TakeDamage(damage);
        Rigidbody2D targetBody = playerHealth.GetComponent<Rigidbody2D>();
        if (targetBody == null || knockback.sqrMagnitude <= .001f) return;

        float horizontalDirection = Mathf.Abs(knockback.x) > .01f
            ? Mathf.Sign(knockback.x)
            : Mathf.Sign(playerHealth.transform.position.x - transform.position.x);
        float horizontalSpeed = Mathf.Min(knockback.magnitude, maxHorizontalKnockbackSpeed);
        float verticalSpeed = Mathf.Clamp(Mathf.Max(targetBody.linearVelocity.y, knockbackLift), -maxVerticalSpeedAfterHit, maxVerticalSpeedAfterHit);
        targetBody.linearVelocity = new Vector2(horizontalDirection * horizontalSpeed, verticalSpeed);
    }

    private void OnBossDamaged(EnemyHealth _, int __)
    {
        if (counterWindowActive && Time.time <= counterWindowEnd && playerHealth != null)
        {
            counterWindowActive = false;
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = StartCoroutine(CounterPunish());
            return;
        }

        if (!isCasting && Time.time >= nextHurtReactionTime)
        {
            nextHurtReactionTime = Time.time + .18f;
            StartCoroutine(PlayHurtReaction());
        }
    }

    private IEnumerator PlayHurtReaction()
    {
        ShowFrame(HurtFrames[0]);
        yield return new WaitForSeconds(SkillTick);
        ShowFrame(HurtFrames[1]);
        yield return new WaitForSeconds(SkillTick);
        ShowIdle();
    }

    private IEnumerator CounterPunish()
    {
        currentSkillLabel = "COUNTER!";
        ShowFrame(26);
        SpawnWarning(player.position, 1.6f, .2f, new Color(.8f, .2f, 1f));
        yield return new WaitForSeconds(.2f);
        DashBehindPlayer();
        ShowFrame(30);
        DamagePlayerInCircle(player.position, 1.8f, 38 + phase * 7, 11f);
        yield return new WaitForSeconds(.15f);
        isCasting = false;
        nextAiDecisionTime = Time.time + globalCooldown;
        repositionUntil = Time.time + repositionDuration;
    }

    private void RepositionAroundPlayer(float distance)
    {
        if (player == null) return;
        float horizontalDelta = player.position.x - transform.position.x;
        float direction = Mathf.Sign(horizontalDelta);
        if (distance < 2.15f) direction *= -1f;

        float speed = moveSpeed * (phase == 3 ? 1.45f : phase == 2 ? 1.2f : 1f);
        float floatingY = groundY + hoverHeight + Mathf.Sin(Time.time * 5f) * .1f;
        Vector3 nextPosition = transform.position + Vector3.right * direction * speed * Time.deltaTime;
        nextPosition.x = Mathf.Clamp(nextPosition.x, arenaMinX, arenaMaxX);
        nextPosition.y = Mathf.Lerp(transform.position.y, floatingY, Time.deltaTime * 7f);
        transform.position = nextPosition;
        ShowFrame(RunFrames[Mathf.FloorToInt(Time.time / SkillTick) % RunFrames.Length]);
    }

    private void MoveTowardsPlayer(float distance)
    {
        float floatingY = groundY + hoverHeight + Mathf.Sin(Time.time * (phase == 3 ? 6f : 4f)) * .12f;
        if (distance < 2f)
        {
            transform.position = Vector3.Lerp(transform.position, new Vector3(transform.position.x, floatingY, transform.position.z), Time.deltaTime * 5f);
            ShowIdle();
            return;
        }
        float speed = moveSpeed * (phase == 3 ? 1.35f : phase == 2 ? 1.15f : 1f);
        float direction = Mathf.Sign(player.position.x - transform.position.x);
        transform.position += Vector3.right * direction * speed * Time.deltaTime;
        transform.position = new Vector3(Mathf.Clamp(transform.position.x, arenaMinX, arenaMaxX), floatingY, transform.position.z);
        ShowFrame(RunFrames[Mathf.FloorToInt(Time.time / SkillTick) % RunFrames.Length]);
    }

    private void FacePlayer()
    {
        if (player == null) return;
        float horizontalDelta = player.position.x - transform.position.x;
        if (Mathf.Abs(horizontalDelta) <= facingDeadZone || Time.time < nextFacingChangeTime) return;

        bool shouldFaceRight = horizontalDelta > 0f;
        if (shouldFaceRight == facingRight) return;
        facingRight = shouldFaceRight;
        nextFacingChangeTime = Time.time + facingChangeLock;
    }

    private void FacePlayerImmediate()
    {
        if (player == null) return;
        float horizontalDelta = player.position.x - transform.position.x;
        if (Mathf.Abs(horizontalDelta) > .01f) facingRight = horizontalDelta > 0f;
        nextFacingChangeTime = Time.time + facingChangeLock;
    }

    private void ShowIdle()
    {
        if (!isCasting) ShowFrame(Mathf.FloorToInt(Time.time * 3f) % 2);
    }

    private void ShowFrame(int sourceStatus)
    {
        if (spriteRenderer == null) return;
        List<Sprite> frames = facingRight || leftFrames.Count == 0 ? rightFrames : leftFrames;
        if (frames.Count == 0) return;
        currentFrameIndex = Mathf.Clamp(sourceStatus, 0, frames.Count - 1);
        spriteRenderer.sprite = frames[currentFrameIndex];
        spriteRenderer.flipX = false;
    }

    private Vector2 FacingOffset(Vector2 offset) => new Vector2(offset.x * (facingRight ? 1f : -1f), offset.y);
    private float PhaseCooldownMultiplier() => phase == 3 ? .34f : phase == 2 ? .58f : 1f;

    private void SpawnSourceEffect(int id, Vector2 offset)
    {
        Sprite[] frames = GetEffectFrames(id);
        if (frames.Length == 0) return;
        GameObject effect = new GameObject($"FideEffect_{id}");
        effect.transform.position = transform.position + (Vector3)FacingOffset(offset / 32f);
        SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 35;
        renderer.flipX = !facingRight;
        FideBossEffectStrip strip = effect.AddComponent<FideBossEffectStrip>();
        strip.Initialize(frames, renderer, SkillTick);
    }

    private Sprite[] GetEffectFrames(int id)
    {
        if (effectFrames.TryGetValue(id, out Sprite[] cached)) return cached;
        Sprite[] frames = Resources.LoadAll<Sprite>($"res/x4/e/e_{id}");
        return effectFrames[id] = frames ?? Array.Empty<Sprite>();
    }

    private void SpawnWarning(Vector2 point, float radius, float duration, Color color)
    {
        GameObject warning = new GameObject("FideWarning");
        warning.transform.position = point;
        warning.transform.localScale = Vector3.one * radius * 2f;
        SpriteRenderer renderer = warning.AddComponent<SpriteRenderer>();
        renderer.sprite = RuntimeSprite.Ring;
        renderer.color = color;
        renderer.sortingOrder = 30;
        Destroy(warning, duration);
    }

    private void SpawnLine(Vector2 center, Vector2 direction, float length, float width, float duration, Color color)
    {
        GameObject line = new GameObject("FideBeam");
        line.transform.position = center;
        line.transform.right = direction;
        line.transform.localScale = new Vector3(length, width, 1f);
        SpriteRenderer renderer = line.AddComponent<SpriteRenderer>();
        renderer.sprite = RuntimeSprite.White;
        renderer.color = color;
        renderer.sortingOrder = 31;
        Destroy(line, duration);
    }

    private void LoadFrames(string path, List<Sprite> destination)
    {
        destination.Clear();
        for (int i = 0; i < 33; i++)
        {
            string framePath = $"{path}/pose_{i:00}";
            Sprite sprite = Resources.Load<Sprite>(framePath);
            if (sprite != null)
            {
                destination.Add(sprite);
                continue;
            }

            Texture2D texture = Resources.Load<Texture2D>(framePath);
            if (texture != null) destination.Add(Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, 0f), 100f));
        }
        if (destination.Count != 33) Debug.LogWarning($"Fide expected 33 poses in Resources/{path}, found {destination.Count}.", this);
    }

    private void BuildSkillData()
    {
        Add(new SkillDefinition(FideSkill.BasicPunch, "Basic Punch", 1, 0f, 2.35f, .72f, new[] { 13, 13, 14, 14, 13, 14, 0 }, new[] { new Cue(0, 9, new Vector2(8, -12), CueAction.Warning, 1.15f), new Cue(3, 2, new Vector2(20, 2), CueAction.HitCircle, 1.25f, 24) }));
        Add(new SkillDefinition(FideSkill.Dragon, "Dragon Rush", 1, 0f, 2.5f, 1.0f, new[] { 2, 3, 4, 9, 10, 11, 12, 0 }, new[] { new Cue(2, 2, new Vector2(-2, -16), CueAction.Warning, 1.35f), new Cue(4, 28, new Vector2(9, -13), CueAction.HitCircle, 1.35f, 20), new Cue(6, 9, new Vector2(12, -13), CueAction.HitCircle, 1.65f, 25) }));
        Add(new SkillDefinition(FideSkill.Antomic, "Antomic Flurry", 1, 0f, 3f, 1.6f, new[] { 13, 14, 15, 16, 17, 13, 14, 15, 16, 17, 0 }, new[] { new Cue(1, 2, new Vector2(2, -17), CueAction.Warning, 1.55f), new Cue(3, 30, new Vector2(8, -13), CueAction.HitCircle, 1.35f, 14), new Cue(5, 29, new Vector2(11, -15), CueAction.HitCircle, 1.55f, 16), new Cue(8, 4, new Vector2(12, -15), CueAction.HitCircle, 1.9f, 28) }));
        Add(new SkillDefinition(FideSkill.Masenko, "Masenko", 1, 2f, 8f, 2.2f, new[] { 18, 19, 20, 21, 22, 21, 31, 32, 0 }, new[] { new Cue(2, 32, new Vector2(11, -17), CueAction.Warning, 1.4f), new Cue(6, 33, new Vector2(8, -14), CueAction.Beam, .38f, 28) }));
        Add(new SkillDefinition(FideSkill.Galick, "Galick Cannon", 1, 5f, 14f, 3.2f, new[] { 18, 19, 20, 21, 22, 22, 30, 31, 32, 0 }, new[] { new Cue(2, 33, new Vector2(10, -15), CueAction.Warning, 2.2f), new Cue(8, 35, new Vector2(1, -19), CueAction.Beam, .62f, 43) }));
        Add(new SkillDefinition(FideSkill.DeathBeam, "Death Beam", 2, 3f, 12f, 2.1f, new[] { 18, 19, 20, 21, 31, 0 }, new[] { new Cue(1, 9, new Vector2(14, -17), CueAction.Warning, .5f), new Cue(4, 35, new Vector2(16, -17), CueAction.Beam, .18f, 34) }));
        Add(new SkillDefinition(FideSkill.AfterimageDash, "Teleport Punch", 1, 2.35f, 14f, 2.4f, new[] { 25, 25, 25, 13, 13, 14, 14, 0 }, new[] { new Cue(0, 9, Vector2.zero, CueAction.Warning, .9f), new Cue(2, 28, Vector2.zero, CueAction.DashBehind), new Cue(6, 2, new Vector2(20, 2), CueAction.HitCircle, 1.45f, 34) }));
        Add(new SkillDefinition(FideSkill.GravityCage, "Gravity Cage", 2, 2f, 9f, 5f, new[] { 18, 19, 20, 21, 22, 30, 31, 0 }, new[] { new Cue(3, 32, Vector2.zero, CueAction.Cage, 0f, 30) }));
        Add(new SkillDefinition(FideSkill.MeteorBarrage, "Meteor Barrage", 3, 3f, 13f, 5.5f, new[] { 18, 19, 20, 21, 22, 26, 27, 28, 29, 30, 0 }, new[] { new Cue(5, 34, Vector2.zero, CueAction.Meteors, 0f, 24) }));
        Add(new SkillDefinition(FideSkill.PlanetBreaker, "Planet Breaker", 3, 2f, 12f, 12f, new[] { 18, 19, 20, 21, 22, 22, 30, 31, 32, 31, 0 }, new[] { new Cue(3, 35, Vector2.zero, CueAction.Warning, 3.5f), new Cue(8, 35, Vector2.zero, CueAction.UltimateRing, 0f, 52) }));
        Add(new SkillDefinition(FideSkill.CounterStance, "Counter Stance", 2, 0f, 3.2f, 4.5f, new[] { 15, 16, 17, 16, 17, 13, 14, 0 }, new[] { new Cue(2, 9, Vector2.zero, CueAction.CounterWindow) }));
        Add(new SkillDefinition(FideSkill.AerialDive, "Aerial Dive", 1, 1.2f, 8f, 3.1f, new[] { 7, 8, 7, 18, 19, 25, 13, 14, 9, 10, 7, 8 }, new[] { new Cue(3, 9, Vector2.zero, CueAction.Warning, 1.1f), new Cue(5, 28, Vector2.zero, CueAction.AerialDive, 0f, 29) }));
        Add(new SkillDefinition(FideSkill.SkyRush, "Sky Rush", 2, 1.5f, 9f, 4.3f, new[] { 7, 8, 18, 19, 25, 13, 14, 9, 25, 13, 14, 10, 7 }, new[] { new Cue(2, 32, Vector2.zero, CueAction.Warning, 1.2f), new Cue(4, 30, Vector2.zero, CueAction.SkyRush, 0f, 28) }));
        Add(new SkillDefinition(FideSkill.VanishingRush, "Vanishing Rush", 3, 1.5f, 11f, 5.8f, new[] { 7, 8, 18, 19, 25, 13, 14, 9, 25, 13, 14, 10, 25, 13, 14, 11, 7 }, new[] { new Cue(2, 35, Vector2.zero, CueAction.Warning, 1.4f), new Cue(4, 28, Vector2.zero, CueAction.VanishingRush, 0f, 31) }));
        Add(new SkillDefinition(FideSkill.DeathBeamBarrage, "Death Beam Barrage", 2, 3f, 14f, 4.2f, new[] { 18, 19, 20, 21, 22, 31, 32, 31, 0 }, new[] { new Cue(2, 35, Vector2.zero, CueAction.Warning, 1.8f), new Cue(5, 35, Vector2.zero, CueAction.BeamBarrage, 0f, 20) }));
        Add(new SkillDefinition(FideSkill.TeleportCross, "Teleport Cross", 2, 1f, 14f, 5.2f, new[] { 25, 13, 14, 25, 13, 14, 25, 13, 14, 9, 0 }, new[] { new Cue(0, 9, Vector2.zero, CueAction.Warning, 1.4f), new Cue(2, 28, Vector2.zero, CueAction.TeleportCross, 0f, 26) }));
        Add(new SkillDefinition(FideSkill.NovaBurst, "Emperor Nova", 2, 0f, 3.5f, 5.4f, new[] { 18, 19, 20, 21, 22, 30, 31, 32, 0 }, new[] { new Cue(2, 35, Vector2.zero, CueAction.Warning, 2.5f), new Cue(5, 35, Vector2.zero, CueAction.NovaBurst, 0f, 38) }));
        Add(new SkillDefinition(FideSkill.DeathSaucerStorm, "Death Saucer Storm", 3, 2f, 14f, 4.8f, new[] { 18, 19, 20, 21, 22, 26, 27, 28, 29, 30, 31, 0 }, new[] { new Cue(2, 35, Vector2.zero, CueAction.Warning, 2.6f), new Cue(6, 35, Vector2.zero, CueAction.SpiralStorm, 0f, 18) }));
    }

    private void Add(SkillDefinition skill) => skills[skill.Id] = skill;

    private void HandleKeyboardTest()
    {
        if (!keyboardSkillTesting) return;
        if (Input.GetKeyDown(KeyCode.Alpha1)) StartSkill(skills[FideSkill.Dragon]);
        if (Input.GetKeyDown(KeyCode.Alpha2)) StartSkill(skills[FideSkill.Antomic]);
        if (Input.GetKeyDown(KeyCode.Alpha3)) StartSkill(skills[FideSkill.Masenko]);
        if (Input.GetKeyDown(KeyCode.Alpha4)) StartSkill(skills[FideSkill.Galick]);
        if (Input.GetKeyDown(KeyCode.Alpha5)) StartSkill(skills[FideSkill.DeathBeam]);
        if (Input.GetKeyDown(KeyCode.Alpha6)) StartSkill(skills[FideSkill.AfterimageDash]);
        if (Input.GetKeyDown(KeyCode.Alpha7)) StartSkill(skills[FideSkill.GravityCage]);
        if (Input.GetKeyDown(KeyCode.Alpha8)) StartSkill(skills[FideSkill.MeteorBarrage]);
        if (Input.GetKeyDown(KeyCode.Alpha9)) StartSkill(skills[FideSkill.PlanetBreaker]);
        if (Input.GetKeyDown(KeyCode.Alpha0)) StartSkill(skills[FideSkill.CounterStance]);
        if (Input.GetKeyDown(KeyCode.Minus)) StartSkill(skills[FideSkill.AerialDive]);
        if (Input.GetKeyDown(KeyCode.Equals)) StartSkill(skills[FideSkill.SkyRush]);
        if (Input.GetKeyDown(KeyCode.BackQuote)) StartSkill(skills[FideSkill.VanishingRush]);
        if (Input.GetKeyDown(KeyCode.F2)) StartSkill(skills[FideSkill.DeathBeamBarrage]);
        if (Input.GetKeyDown(KeyCode.F3)) StartSkill(skills[FideSkill.TeleportCross]);
        if (Input.GetKeyDown(KeyCode.F4)) StartSkill(skills[FideSkill.NovaBurst]);
        if (Input.GetKeyDown(KeyCode.F5)) StartSkill(skills[FideSkill.DeathSaucerStorm]);
        if (Input.GetKeyDown(KeyCode.F6)) StartSkill(skills[FideSkill.BasicPunch]);
        if (Input.GetKeyDown(KeyCode.F1)) aiEnabled = !aiEnabled;
    }

    private void OnGUI()
    {
        if (!showDebugHud || !Application.isPlaying || health == null) return;
        GUI.Box(new Rect(16, 16, 430, 118), "Fide Dai Ca 3 - Combat Runtime");
        GUI.Label(new Rect(28, 42, 330, 20), $"HP {health.CurrentHealth}/{health.MaxHealth}    Phase {phase}    AI {(aiEnabled ? "ON" : "OFF")}");
        GUI.Label(new Rect(28, 64, 330, 20), $"Casting: {currentSkillLabel}    Frame: {currentFrameIndex:00}");
        GUI.Label(new Rect(28, 86, 410, 20), "F2 Beam Barrage | F3 Teleport Cross | F4 Nova | F5 Storm | F6 Punch | F1 AI");
    }
}

public sealed class FideBossProjectile : MonoBehaviour
{
    private static readonly Dictionary<PlayerHealth, float> nextDamageTimes = new Dictionary<PlayerHealth, float>();
    private int damage;
    private float lifetime;
    private float invulnerability;
    private float age;
    private Rigidbody2D body;

    public void Initialize(Vector2 direction, float speed, int newDamage, float newLifetime, float newInvulnerability)
    {
        damage = newDamage; lifetime = newLifetime; invulnerability = newInvulnerability;
        body = GetComponent<Rigidbody2D>();
        body.linearVelocity = direction.normalized * speed;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime) Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerHealth target = other.GetComponentInParent<PlayerHealth>();
        if (target == null) return;
        if (nextDamageTimes.TryGetValue(target, out float nextDamageTime) && Time.time < nextDamageTime)
        {
            Destroy(gameObject);
            return;
        }

        nextDamageTimes[target] = Time.time + invulnerability;
        target.TakeDamage(damage);
        Rigidbody2D targetBody = target.GetComponent<Rigidbody2D>();
        if (targetBody != null && body != null)
        {
            float horizontalDirection = Mathf.Abs(body.linearVelocity.x) > .01f ? Mathf.Sign(body.linearVelocity.x) : 1f;
            targetBody.linearVelocity = new Vector2(horizontalDirection * 4.5f, Mathf.Clamp(Mathf.Max(targetBody.linearVelocity.y, 1f), -4f, 4f));
        }
        Destroy(gameObject);
    }
}

public sealed class FideBossEffectStrip : MonoBehaviour
{
    private Sprite[] frames;
    private SpriteRenderer effectRenderer;
    private float tick;
    private float elapsed;

    public void Initialize(Sprite[] sourceFrames, SpriteRenderer targetRenderer, float frameTick)
    {
        frames = sourceFrames; effectRenderer = targetRenderer; tick = frameTick;
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0 || effectRenderer == null) { Destroy(gameObject); return; }
        int index = Mathf.Min(Mathf.FloorToInt(elapsed / tick), frames.Length - 1);
        effectRenderer.sprite = frames[index];
        elapsed += Time.deltaTime;
        if (elapsed >= frames.Length * tick) Destroy(gameObject);
    }
}

internal static class RuntimeSprite
{
    private static Sprite white;
    private static Sprite ring;
    public static Sprite White => white ??= CreateWhite();
    public static Sprite Ring => ring ??= CreateRing();

    private static Sprite CreateWhite()
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        texture.SetPixel(0, 0, Color.white); texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
    }

    private static Sprite CreateRing()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        Vector2 center = Vector2.one * (size - 1) * .5f;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float normalized = Vector2.Distance(new Vector2(x, y), center) / (size * .5f);
            texture.SetPixel(x, y, normalized > .78f && normalized < .98f ? Color.white : Color.clear);
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
    }
}
