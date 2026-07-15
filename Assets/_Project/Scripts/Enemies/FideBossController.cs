using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TinyDragon.UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

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

    private enum FideSkill { BasicPunch, Dragon, Antomic, Masenko, Galick, DeathBeam, AfterimageDash, GravityCage, MeteorBarrage, PlanetBreaker, CounterStance, AerialDive, SkyRush, VanishingRush, DeathBeamBarrage, TeleportCross, NovaBurst, DeathSaucerStorm, SolarBomb, Kamehameha }
    private enum CueAction { None, Warning, HitCircle, Projectile, Beam, DashBehind, Cage, Meteors, UltimateRing, CounterWindow, AerialDive, SkyRush, VanishingRush, BeamBarrage, TeleportCross, NovaBurst, SpiralStorm, KamehamehaCharge, KamehamehaBeam }
    private static readonly KeyCode[] NumberRowKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0 };
    private static readonly KeyCode[] NumberPadKeys = { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5, KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9, KeyCode.Keypad0 };
    private static readonly FideSkill[] NumberSkills = { FideSkill.BasicPunch, FideSkill.Dragon, FideSkill.Antomic, FideSkill.Masenko, FideSkill.Galick, FideSkill.DeathBeam, FideSkill.AfterimageDash, FideSkill.GravityCage, FideSkill.MeteorBarrage, FideSkill.PlanetBreaker };
    private static readonly FideSkill[] ShiftNumberSkills = { FideSkill.CounterStance, FideSkill.AerialDive, FideSkill.SkyRush, FideSkill.VanishingRush, FideSkill.DeathBeamBarrage, FideSkill.TeleportCross, FideSkill.NovaBurst, FideSkill.DeathSaucerStorm, FideSkill.SolarBomb, FideSkill.Kamehameha };

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
        public readonly float MinRange;
        public readonly float MaxRange;
        public readonly float Cooldown;
        public readonly int[] StatusFrames;
        public readonly Cue[] Cues;

        public SkillDefinition(FideSkill id, string label, int _, float minRange, float maxRange, float cooldown, int[] statusFrames, Cue[] cues)
        {
            Id = id; Label = label; MinRange = minRange; MaxRange = maxRange; Cooldown = cooldown; StatusFrames = statusFrames; Cues = cues;
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
    [SerializeField] private bool useWholeMapAttackRange = true;
    [SerializeField] private bool requirePlayerProximityToActivate = true;
    [SerializeField, Min(0f)] private float activationRange = 10f;
    [SerializeField] private string introDialogueText = "Ngươi cũng gan lắm mới dám bước tới đây.|Nhưng từ khoảnh khắc này...|thành phố Vegeta sẽ là nơi ngươi gục xuống.";
    [SerializeField] private string playerResponseText = "Ta không đến đây để lùi bước.|Nếu ngươi muốn một trận chiến,|ta sẽ kết thúc nó ngay tại đây.";
    [SerializeField, Min(1f)] private float introDialogueCharsPerSecond = 34f;
    [SerializeField, Min(0f)] private float introDialogueHoldAfterTyping = .9f;
    [SerializeField] private Vector3 introDialogueOffset = new Vector3(0f, 4.75f, 0f);
    [SerializeField] private Vector3 playerDialogueOffset = new Vector3(0f, 2.65f, 0f);
    [SerializeField, Range(.02f, .25f)] private float dialogueViewportMargin = .03f;
    [SerializeField] private bool disableLegacyFideAi = true;
    [SerializeField] private bool disableFrameBridge = true;
    [SerializeField] private bool disableLegacyAnimator = true;

    [Header("Attack Position Randomizer")]
    [SerializeField] private bool randomizeAttackStartPosition = true;
    [SerializeField] private bool alternateAttackSides = true;
    [SerializeField, Range(0f, 1f)] private float rightSideAttackChance = .5f;
    [SerializeField] private float randomAttackEdgePadding = .65f;
    [SerializeField] private float randomCloseAttackMinDistance = .85f;
    [SerializeField] private float randomRangedAttackMinDistance = 2.6f;
    [SerializeField] private float randomAttackMaxDistance = 8f;
    [SerializeField] private float randomAttackHeightJitter = .18f;

    [Header("Boss Difficulty")]
    [SerializeField] private bool enforceBossHealth = true;
    [SerializeField, Min(1)] private int bossMaxHealth = 1500;

    [Header("Aerial Pressure")]
    [SerializeField] private float hoverHeight = 0.45f;
    [SerializeField] private float aerialHeight = 3.1f;
    [SerializeField] private float aerialMoveSpeed = 17f;
    [SerializeField] private float aerialDiveWidth = 1.1f;

    [Header("Solar Bomb")]
    [SerializeField] private float solarBombHeight = 4.2f;
    [SerializeField] private float solarBombChargeDuration = .85f;
    [SerializeField] private float solarBombThrowSpeed = 8.5f;
    [SerializeField] private float solarBombScale = 1.15f;
    [SerializeField] private int solarBombDamage = 48;
    [SerializeField, Range(.05f, .8f)] private float solarBombAmbientLight = .22f;
    [SerializeField, Range(.03f, .2f)] private float solarBombEnergyStreamInterval = .055f;

    [Header("Kamehameha Placement")]
    [SerializeField] private Vector2 kamehamehaChargeOffsetPixels = new Vector2(13f, 10f);
    [SerializeField] private Vector2 kamehamehaBeamOffset = new Vector2(.4f, .31f);
    [SerializeField] private float kamehamehaBeamScreenPadding = 2.5f;
    [SerializeField, Min(.03f)] private float kamehamehaDamageInterval = .18f;

    [Header("Combat")]
    [SerializeField] private float playerHitInvulnerability = 0.22f;
    [SerializeField] private float projectileSpeed = 10f;
    [SerializeField] private float projectileLifetime = 3.5f;
    [SerializeField] private float maxHorizontalKnockbackSpeed = 7f;
    [SerializeField] private float knockbackLift = 1.25f;
    [SerializeField] private float maxVerticalSpeedAfterHit = 4f;

    [Header("Debug")]
    [SerializeField] private bool keyboardSkillTesting = true;
    [SerializeField] private bool aiEnabled = true;

    private readonly List<Sprite> rightFrames = new List<Sprite>(33);
    private readonly List<Sprite> leftFrames = new List<Sprite>(33);
    private readonly Dictionary<int, Sprite[]> effectFrames = new Dictionary<int, Sprite[]>();
    private readonly Dictionary<FideSkill, SkillDefinition> skills = new Dictionary<FideSkill, SkillDefinition>();
    private readonly Dictionary<FideSkill, float> cooldownEnds = new Dictionary<FideSkill, float>();
    private readonly Queue<FideSkill> recentSkills = new Queue<FideSkill>();
    private FideBossAnimationLibrary animationLibrary;
    private SpriteRenderer spriteRenderer;
    private Animator legacyAnimator;
    private Rigidbody2D bossBody;
    private Collider2D bossCollider;
    private EnemyHealth health;
    private PlayerHealth playerHealth;
    private Coroutine activeRoutine;
    private Coroutine introDialogueRoutine;
    private GameObject introDialogueBubble;
    private CanvasGroup introDialogueGroup;
    private Text introDialogueTextComponent;
    private GameObject playerDialogueBubble;
    private CanvasGroup playerDialogueGroup;
    private Text playerDialogueTextComponent;
    private PlayerInputReader lockedPlayerInput;
    private bool restoreMovementInput;
    private bool restoreJumpInput;
    private bool restoreAttackInput;
    private bool restorePowerShotInput;
    private bool restorePunchInput;
    private bool restoreKickInput;
    private bool playerInputLocked;
    private FideSkill currentSkill;
    private string currentSkillLabel = "Hunting";
    private bool facingRight = true;
    private bool combatActivated;
    private bool introDialogueStarted;
    private bool counterWindowActive;
    private bool isCasting;
    private float counterWindowEnd;
    private float nextPlayerDamageTime;
    private float nextKamehamehaDamageTime;
    private float nextAiDecisionTime;
    private float closeTime;
    private float farTime;
    private float groundY;
    private float actionLockUntil;
    private float nextHurtReactionTime;
    private float nextFacingChangeTime;
    private float repositionUntil;
    private int nextAttackSide = 1;
    private int currentFrameIndex;
    private Light2D[] solarBombLights = Array.Empty<Light2D>();
    private float[] solarBombLightIntensities = Array.Empty<float>();

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
        animationLibrary = Resources.Load<FideBossAnimationLibrary>("FideBossSkillAnimationLibrary");
        BuildSkillData();
        health.HealthDamaged += OnBossDamaged;
        health.Died += OnBossDied;
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
        RestoreSolarBombLighting();
        UnlockPlayerForDialogue();
        DestroyDialogueBubbles();
        if (health != null)
        {
            health.HealthDamaged -= OnBossDamaged;
            health.Died -= OnBossDied;
        }
    }

    private void Update()
    {
        ResolvePlayer();
        HandleKeyboardTest();
        if (player == null || isCasting || Time.time < actionLockUntil) return;

        float distance = Vector2.Distance(transform.position, player.position);
        FacePlayer();

        if (IsWaitingForPlayerActivation(distance))
        {
            ShowIdle();
            return;
        }

        closeTime = distance < 2.2f ? closeTime + Time.deltaTime : 0f;
        farTime = distance > 8f ? farTime + Time.deltaTime : 0f;

        if (aiEnabled && Time.time < repositionUntil)
        {
            RepositionAroundPlayer(distance);
            return;
        }

        if (aiEnabled && Time.time >= nextAiDecisionTime)
        {
            nextAiDecisionTime = Time.time + .085f;
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

    private bool IsWaitingForPlayerActivation(float distance)
    {
        if (!requirePlayerProximityToActivate || combatActivated)
        {
            return false;
        }

        if (distance > activationRange)
        {
            currentSkillLabel = "Waiting";
            return true;
        }

        if (!introDialogueStarted)
        {
            introDialogueRoutine = StartCoroutine(PlayIntroDialogue());
        }

        return true;
    }

    private IEnumerator PlayIntroDialogue()
    {
        introDialogueStarted = true;
        currentSkillLabel = "Talking";
        LockPlayerForDialogue();
        ShowIntroDialogue();
        yield return PlayDialoguePages(introDialogueTextComponent, introDialogueText, "Fide sẽ kết thúc trận này!");
        HideIntroDialogue();

        ShowPlayerDialogue();
        yield return PlayDialoguePages(playerDialogueTextComponent, playerResponseText, "Ta sẽ đánh bại ngươi!");
        HidePlayerDialogue();

        UnlockPlayerForDialogue();
        combatActivated = true;
        currentSkillLabel = "Hunting";
        introDialogueRoutine = null;
    }

    private void LockPlayerForDialogue()
    {
        if (player == null || playerInputLocked)
        {
            return;
        }

        lockedPlayerInput = player.GetComponent<PlayerInputReader>();
        if (lockedPlayerInput == null)
        {
            return;
        }

        restoreMovementInput = lockedPlayerInput.IsFeatureEnabled("Movement");
        restoreJumpInput = lockedPlayerInput.IsFeatureEnabled("Jump");
        restoreAttackInput = lockedPlayerInput.IsFeatureEnabled("Attack");
        restorePowerShotInput = lockedPlayerInput.IsFeatureEnabled("PowerShot");
        restorePunchInput = lockedPlayerInput.IsFeatureEnabled("Punch");
        restoreKickInput = lockedPlayerInput.IsFeatureEnabled("Kick");
        lockedPlayerInput.SetInputEnabled(false, false, false, false, false, false);

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.Stop();
        }

        playerInputLocked = true;
    }

    private void UnlockPlayerForDialogue()
    {
        if (!playerInputLocked)
        {
            return;
        }

        if (lockedPlayerInput != null)
        {
            lockedPlayerInput.SetInputEnabled(
                restoreMovementInput,
                restoreJumpInput,
                restoreAttackInput,
                restorePowerShotInput,
                restorePunchInput,
                restoreKickInput
            );
        }

        lockedPlayerInput = null;
        playerInputLocked = false;
    }

    private void ShowIntroDialogue()
    {
        EnsureIntroDialogueBubble();
        if (introDialogueBubble == null) return;

        if (introDialogueTextComponent != null)
        {
            introDialogueTextComponent.text = string.Empty;
        }

        introDialogueBubble.SetActive(true);
        if (introDialogueGroup != null)
        {
            introDialogueGroup.alpha = 1f;
        }
    }

    private void LateUpdate()
    {
        UpdateDialogueBubblePositions();
    }

    private void ShowPlayerDialogue()
    {
        EnsurePlayerDialogueBubble();
        if (playerDialogueBubble == null) return;

        if (playerDialogueTextComponent != null)
        {
            playerDialogueTextComponent.text = string.Empty;
        }

        playerDialogueBubble.SetActive(true);
        if (playerDialogueGroup != null)
        {
            playerDialogueGroup.alpha = 1f;
        }
    }

    private IEnumerator PlayDialoguePages(Text targetText, string dialogue, string fallback)
    {
        string[] pages = SplitDialoguePages(dialogue, fallback);
        for (int i = 0; i < pages.Length; i++)
        {
            yield return TypeDialogue(targetText, pages[i], fallback);
            yield return new WaitForSeconds(introDialogueHoldAfterTyping);
        }
    }

    private IEnumerator TypeDialogue(Text targetText, string dialogue, string fallback)
    {
        if (targetText == null)
        {
            yield break;
        }

        string fullText = string.IsNullOrWhiteSpace(dialogue) ? fallback : dialogue;
        float secondsPerCharacter = 1f / Mathf.Max(1f, introDialogueCharsPerSecond);

        targetText.text = string.Empty;
        for (int i = 0; i < fullText.Length; i++)
        {
            targetText.text = fullText.Substring(0, i + 1);
            yield return new WaitForSeconds(secondsPerCharacter);
        }
    }

    private static string[] SplitDialoguePages(string dialogue, string fallback)
    {
        string source = string.IsNullOrWhiteSpace(dialogue) ? fallback : dialogue;
        string[] rawPages = source.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        List<string> pages = new List<string>();
        foreach (string rawPage in rawPages)
        {
            string page = rawPage.Trim();
            if (!string.IsNullOrWhiteSpace(page))
            {
                pages.Add(page);
            }
        }

        if (pages.Count == 0)
        {
            pages.Add(fallback);
        }

        return pages.ToArray();
    }

    private void HideIntroDialogue()
    {
        if (introDialogueBubble != null)
        {
            introDialogueBubble.SetActive(false);
        }
    }

    private void HidePlayerDialogue()
    {
        if (playerDialogueBubble != null)
        {
            playerDialogueBubble.SetActive(false);
        }
    }

    private void DestroyDialogueBubbles()
    {
        if (introDialogueBubble != null)
        {
            Destroy(introDialogueBubble);
            introDialogueBubble = null;
        }

        if (playerDialogueBubble != null)
        {
            Destroy(playerDialogueBubble);
            playerDialogueBubble = null;
        }
    }

    private void UpdateDialogueBubblePositions()
    {
        PositionDialogueBubble(introDialogueBubble, transform, introDialogueOffset);
        PositionDialogueBubble(playerDialogueBubble, player, playerDialogueOffset);
    }

    private void PositionDialogueBubble(GameObject bubble, Transform anchor, Vector3 offset)
    {
        if (bubble == null || anchor == null || !bubble.activeSelf)
        {
            return;
        }

        Vector3 targetPosition = anchor.position + offset;
        bubble.transform.position = ClampDialoguePositionToCamera(targetPosition, bubble);
        AimDialogueTailAtAnchor(bubble, anchor.position);
    }

    private static void AimDialogueTailAtAnchor(GameObject bubble, Vector3 anchorPosition)
    {
        RectTransform bubbleRect = bubble.GetComponent<RectTransform>();
        Transform tailTransform = bubble.transform.Find("Tail");
        RectTransform tailRect = tailTransform != null ? tailTransform.GetComponent<RectTransform>() : null;
        if (bubbleRect == null || tailRect == null)
        {
            return;
        }

        float localAnchorX = bubble.transform.InverseTransformPoint(anchorPosition).x;
        float tailLimit = Mathf.Max(0f, bubbleRect.rect.width * .5f - 54f);
        Vector2 anchoredPosition = tailRect.anchoredPosition;
        anchoredPosition.x = Mathf.Clamp(localAnchorX, -tailLimit, tailLimit);
        tailRect.anchoredPosition = anchoredPosition;
    }

    private Vector3 ClampDialoguePositionToCamera(Vector3 targetPosition, GameObject bubble = null)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return targetPosition;
        }

        if (camera.orthographic)
        {
            Vector2 halfSize = GetDialogueHalfSizeWorld(bubble);
            float cameraHalfHeight = camera.orthographicSize;
            float cameraHalfWidth = cameraHalfHeight * camera.aspect;
            float marginX = cameraHalfWidth * 2f * dialogueViewportMargin;
            float marginY = cameraHalfHeight * 2f * dialogueViewportMargin;
            float minX = camera.transform.position.x - cameraHalfWidth + halfSize.x + marginX;
            float maxX = camera.transform.position.x + cameraHalfWidth - halfSize.x - marginX;
            float minY = camera.transform.position.y - cameraHalfHeight + halfSize.y + marginY;
            float maxY = camera.transform.position.y + cameraHalfHeight - halfSize.y - marginY;

            targetPosition.x = minX <= maxX ? Mathf.Clamp(targetPosition.x, minX, maxX) : camera.transform.position.x;
            targetPosition.y = minY <= maxY ? Mathf.Clamp(targetPosition.y, minY, maxY) : camera.transform.position.y;
            return targetPosition;
        }

        Vector3 viewportPoint = camera.WorldToViewportPoint(targetPosition);
        float zDistance = Mathf.Max(.01f, targetPosition.z - camera.transform.position.z);
        viewportPoint.x = Mathf.Clamp(viewportPoint.x, dialogueViewportMargin, 1f - dialogueViewportMargin);
        viewportPoint.y = Mathf.Clamp(viewportPoint.y, dialogueViewportMargin, 1f - dialogueViewportMargin);
        viewportPoint.z = zDistance;

        Vector3 clampedPosition = camera.ViewportToWorldPoint(viewportPoint);
        clampedPosition.z = targetPosition.z;
        return clampedPosition;
    }

    private static Vector2 GetDialogueHalfSizeWorld(GameObject bubble)
    {
        if (bubble == null)
        {
            return Vector2.zero;
        }

        RectTransform rect = bubble.GetComponent<RectTransform>();
        if (rect == null)
        {
            return Vector2.zero;
        }

        Vector3 scale = rect.lossyScale;
        return new Vector2(Mathf.Abs(rect.rect.width * scale.x) * .5f, Mathf.Abs(rect.rect.height * scale.y) * .5f);
    }

    private void EnsureIntroDialogueBubble()
    {
        if (introDialogueBubble != null)
        {
            if (introDialogueTextComponent == null)
            {
                introDialogueTextComponent = introDialogueBubble.GetComponentInChildren<Text>(true);
            }

            return;
        }

        GameObject bubble = new GameObject("FideIntroDialogue");
        bubble.transform.position = ClampDialoguePositionToCamera(transform.position + introDialogueOffset);
        bubble.transform.localScale = Vector3.one * .01f;

        Canvas canvas = bubble.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 6000;

        RectTransform bubbleRect = bubble.GetComponent<RectTransform>();
        bubbleRect.sizeDelta = new Vector2(460f, 162f);

        introDialogueGroup = bubble.AddComponent<CanvasGroup>();
        introDialogueGroup.blocksRaycasts = false;
        introDialogueGroup.interactable = false;

        Color bossPanel = new Color(.08f, .075f, .09f, .94f);
        Color bossAccent = new Color(1f, .68f, .24f, .95f);
        CreateDialoguePanel(bubble.transform, new Vector2(460f, 138f), bossPanel, bossAccent);
        CreateDialogueTail(bubble.transform, -76f, bossPanel);
        CreateSpeakerTag(bubble.transform, "FIDE", bossAccent, new Color(.14f, .06f, .04f, .96f), new Vector2(-148f, 67f));
        introDialogueTextComponent = CreateDialogueText(bubble.transform, new Vector2(396f, 94f), new Vector2(0f, -2f), new Color(.98f, .94f, .84f, 1f));

        introDialogueBubble = bubble;
        bubble.transform.position = ClampDialoguePositionToCamera(transform.position + introDialogueOffset, bubble);
        introDialogueBubble.SetActive(false);
    }

    private void EnsurePlayerDialogueBubble()
    {
        if (player == null)
        {
            return;
        }

        if (playerDialogueBubble != null)
        {
            if (playerDialogueTextComponent == null)
            {
                playerDialogueTextComponent = playerDialogueBubble.GetComponentInChildren<Text>(true);
            }

            return;
        }

        GameObject bubble = new GameObject("PlayerResponseDialogue");
        bubble.transform.position = ClampDialoguePositionToCamera(player.position + playerDialogueOffset);
        bubble.transform.localScale = Vector3.one * .01f;

        Canvas canvas = bubble.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 6000;

        RectTransform bubbleRect = bubble.GetComponent<RectTransform>();
        bubbleRect.sizeDelta = new Vector2(430f, 154f);

        playerDialogueGroup = bubble.AddComponent<CanvasGroup>();
        playerDialogueGroup.blocksRaycasts = false;
        playerDialogueGroup.interactable = false;

        Color playerPanel = new Color(.055f, .085f, .1f, .94f);
        Color playerAccent = new Color(.48f, .84f, 1f, .95f);
        CreateDialoguePanel(bubble.transform, new Vector2(430f, 130f), playerPanel, playerAccent);
        CreateDialogueTail(bubble.transform, -72f, playerPanel);
        CreateSpeakerTag(bubble.transform, "PLAYER", playerAccent, new Color(.025f, .105f, .14f, .96f), new Vector2(-128f, 63f));
        playerDialogueTextComponent = CreateDialogueText(bubble.transform, new Vector2(368f, 88f), new Vector2(0f, -3f), new Color(.9f, .97f, 1f, 1f));

        playerDialogueBubble = bubble;
        bubble.transform.position = ClampDialoguePositionToCamera(player.position + playerDialogueOffset, bubble);
        playerDialogueBubble.SetActive(false);
    }

    private static void CreateDialoguePanel(Transform parent)
    {
        CreateDialoguePanel(parent, new Vector2(560f, 164f), new Color(.08f, .075f, .09f, .94f), new Color(1f, .68f, .24f, .95f));
    }

    private static void CreateDialoguePanel(Transform parent, Vector2 size, Color panelColor, Color outlineColor)
    {
        GameObject shadow = new GameObject("Shadow");
        shadow.transform.SetParent(parent, false);
        RectTransform shadowRect = shadow.AddComponent<RectTransform>();
        shadowRect.anchorMin = new Vector2(.5f, .5f);
        shadowRect.anchorMax = new Vector2(.5f, .5f);
        shadowRect.anchoredPosition = new Vector2(8f, -8f);
        shadowRect.sizeDelta = size + new Vector2(10f, 10f);
        Image shadowImage = shadow.AddComponent<Image>();
        shadowImage.color = new Color(0f, 0f, 0f, .34f);

        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(.5f, .5f);
        rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        Image image = panel.AddComponent<Image>();
        image.color = panelColor;

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(3f, -3f);

        GameObject highlight = new GameObject("AccentLine");
        highlight.transform.SetParent(parent, false);
        RectTransform highlightRect = highlight.AddComponent<RectTransform>();
        highlightRect.anchorMin = new Vector2(.5f, .5f);
        highlightRect.anchorMax = new Vector2(.5f, .5f);
        highlightRect.anchoredPosition = new Vector2(0f, size.y * .5f - 18f);
        highlightRect.sizeDelta = new Vector2(size.x - 40f, 4f);
        Image highlightImage = highlight.AddComponent<Image>();
        highlightImage.color = outlineColor;
    }

    private static void CreateSpeakerTag(Transform parent, string speakerName, Color accentColor, Color backgroundColor, Vector2 position)
    {
        GameObject tag = new GameObject("SpeakerTag");
        tag.transform.SetParent(parent, false);
        RectTransform rect = tag.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(.5f, .5f);
        rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(132f, 38f);

        Image image = tag.AddComponent<Image>();
        image.color = backgroundColor;
        Outline outline = tag.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject label = new GameObject("Label");
        label.transform.SetParent(tag.transform, false);
        RectTransform labelRect = label.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text text = label.AddComponent<Text>();
        text.alignment = TextAnchor.MiddleCenter;
        text.font = LoadRuntimeFont();
        text.fontSize = 20;
        text.fontStyle = FontStyle.Bold;
        text.color = accentColor;
        text.text = speakerName;
    }

    private static void CreateDialogueTail(Transform parent)
    {
        CreateDialogueTail(parent, -90f, new Color(.05f, .06f, .07f, .92f));
    }

    private static void CreateDialogueTail(Transform parent, float yPosition, Color tailColor)
    {
        GameObject tail = new GameObject("Tail");
        tail.transform.SetParent(parent, false);
        RectTransform rect = tail.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(.5f, .5f);
        rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(0f, yPosition);
        rect.sizeDelta = new Vector2(38f, 38f);
        rect.localRotation = Quaternion.Euler(0f, 0f, 45f);

        Image image = tail.AddComponent<Image>();
        image.color = tailColor;
    }

    private static Text CreateDialogueText(Transform parent)
    {
        return CreateDialogueText(parent, new Vector2(488f, 116f), Vector2.zero, new Color(.98f, .94f, .84f, 1f));
    }

    private static Text CreateDialogueText(Transform parent, Vector2 size)
    {
        return CreateDialogueText(parent, size, Vector2.zero, new Color(.98f, .94f, .84f, 1f));
    }

    private static Text CreateDialogueText(Transform parent, Vector2 size, Vector2 position, Color textColor)
    {
        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(.5f, .5f);
        rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = textObject.AddComponent<Text>();
        text.alignment = TextAnchor.MiddleCenter;
        text.font = LoadRuntimeFont();
        text.fontSize = 25;
        text.fontStyle = FontStyle.Normal;
        text.color = textColor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 17;
        text.resizeTextMaxSize = 25;

        Shadow shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, .62f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    private static Font LoadRuntimeFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            return font;
        }

        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font != null ? font : Font.CreateDynamicFontFromOSFont("Arial", 28);
    }

    private SkillDefinition ChooseSkill(float distance)
    {
        // Anti-cheese rules have priority, but every option remains telegraphed.
        if (distance > 2.35f && TryGetAvailable(FideSkill.AfterimageDash, out SkillDefinition teleportAssault)) return teleportAssault;
        if (distance <= 2.35f && TryGetAvailable(FideSkill.BasicPunch, out SkillDefinition basicPunch)) return basicPunch;
        if (farTime > 2f && TryGetAvailable(FideSkill.Galick, out SkillDefinition galick)) return galick;
        if (closeTime > 1.6f && TryGetAvailable(FideSkill.CounterStance, out SkillDefinition counter)) return counter;

        List<SkillDefinition> choices = skills.Values
            .Where(skill => IsWithinAttackRange(skill, distance) && IsAvailable(skill))
            .OrderBy(_ => UnityEngine.Random.value)
            .ToList();
        return choices.Count == 0 ? null : choices[0];
    }

    private bool IsWithinAttackRange(SkillDefinition skill, float distance)
    {
        if (distance < skill.MinRange) return false;
        return useWholeMapAttackRange || distance <= skill.MaxRange;
    }

    private bool TryGetAvailable(FideSkill id, out SkillDefinition skill)
    {
        return skills.TryGetValue(id, out skill) && IsAvailable(skill);
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

    private void RandomizeAttackStartPosition(SkillDefinition skill)
    {
        if (!randomizeAttackStartPosition || player == null) return;

        float minX = Mathf.Min(arenaMinX, arenaMaxX) + randomAttackEdgePadding;
        float maxX = Mathf.Max(arenaMinX, arenaMaxX) - randomAttackEdgePadding;
        if (minX > maxX)
        {
            minX = Mathf.Min(arenaMinX, arenaMaxX);
            maxX = Mathf.Max(arenaMinX, arenaMaxX);
        }

        float minDistance = skill.MaxRange <= 3.5f
            ? Mathf.Max(skill.MinRange, randomCloseAttackMinDistance)
            : Mathf.Max(skill.MinRange, randomRangedAttackMinDistance);
        float maxDistance = Mathf.Min(skill.MaxRange, randomAttackMaxDistance);
        if (skill.MaxRange <= 3.5f)
        {
            maxDistance = Mathf.Min(maxDistance, Mathf.Max(minDistance, skill.MaxRange * .72f));
        }

        if (maxDistance < minDistance)
        {
            maxDistance = minDistance;
        }

        int preferredSide = ChoosePreferredAttackSide();
        if (!TryGetRandomAttackX(preferredSide, minDistance, maxDistance, minX, maxX, out float x, out int chosenSide)
            && !TryGetRandomAttackX(-preferredSide, minDistance, maxDistance, minX, maxX, out x, out chosenSide))
        {
            chosenSide = preferredSide;
            x = Mathf.Clamp(player.position.x + chosenSide * minDistance, minX, maxX);
        }

        nextAttackSide = -chosenSide;
        float y = groundY + hoverHeight + UnityEngine.Random.Range(-randomAttackHeightJitter, randomAttackHeightJitter);
        Vector3 nextPosition = new Vector3(x, y, transform.position.z);

        SpawnAfterimage();
        transform.position = nextPosition;
        if (bossBody != null) bossBody.position = nextPosition;
        FacePlayerImmediate();
        ShowFrame(TeleportFrame);
    }

    private int ChoosePreferredAttackSide()
    {
        if (alternateAttackSides)
        {
            return nextAttackSide >= 0 ? 1 : -1;
        }

        return UnityEngine.Random.value <= rightSideAttackChance ? 1 : -1;
    }

    private bool TryGetRandomAttackX(int side, float minDistance, float maxDistance, float minX, float maxX, out float x, out int chosenSide)
    {
        chosenSide = side >= 0 ? 1 : -1;
        float playerX = player.position.x;
        float sideMinX = chosenSide > 0 ? playerX + minDistance : playerX - maxDistance;
        float sideMaxX = chosenSide > 0 ? playerX + maxDistance : playerX - minDistance;
        float validMinX = Mathf.Max(minX, sideMinX);
        float validMaxX = Mathf.Min(maxX, sideMaxX);

        if (validMinX > validMaxX)
        {
            x = playerX;
            return false;
        }

        x = UnityEngine.Random.Range(validMinX, validMaxX);
        return true;
    }

    private IEnumerator PlaySkill(SkillDefinition skill)
    {
        isCasting = true;
        currentSkill = skill.Id;
        currentSkillLabel = skill.Label;
        RandomizeAttackStartPosition(skill);
        cooldownEnds[skill.Id] = Time.time + skill.Cooldown;
        recentSkills.Enqueue(skill.Id);
        while (recentSkills.Count > 3) recentSkills.Dequeue();

        if (skill.Id == FideSkill.SolarBomb)
        {
            yield return SolarBombRoutine();
        }
        else
        {
            AnimationClip editableClip = animationLibrary != null ? animationLibrary.GetClip((int)skill.Id) : null;
            int editableSteps = editableClip != null ? Mathf.CeilToInt(editableClip.length / SkillTick) + 1 : 0;
            int stepCount = Mathf.Max(skill.StatusFrames.Length, editableSteps);
            for (int step = 0; step < stepCount; step++)
            {
                int fallbackFrame = skill.StatusFrames[Mathf.Min(step, skill.StatusFrames.Length - 1)];
                SampleSkillAnimation(skill.Id, step * SkillTick, fallbackFrame);
                foreach (Cue cue in skill.Cues)
                {
                    if (cue.Step != step) continue;
                    SpawnSourceEffect(cue.EffectId, cue.Offset);
                    ExecuteCue(skill, cue);
                }
                yield return new WaitForSeconds(SkillTick);
            }
        }

        counterWindowActive = false;
        isCasting = false;
        nextAiDecisionTime = Time.time + globalCooldown;
        repositionUntil = Time.time + repositionDuration;
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
                StartCoroutine(MeteorWaves(target, 3, cue.Damage));
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
                StartAerialDive(3, cue.Damage);
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
            case CueAction.KamehamehaCharge:
                SpawnKamehamehaCharge(cue.Radius);
                break;
            case CueAction.KamehamehaBeam:
                SpawnKamehamehaBeam(target, cue.Radius, cue.Damage);
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

    private void SpawnKamehamehaCharge(float duration)
    {
        Sprite charge = RuntimeSprite.KamehamehaCharge;
        if (charge == null) return;

        GameObject effect = new GameObject("FideKamehamehaCharge");
        effect.transform.SetParent(transform, false);
        effect.transform.localPosition = FacingOffset(kamehamehaChargeOffsetPixels / 32f);
        effect.transform.localScale = Vector3.one * 1.55f;

        GameObject glow = new GameObject("FideKamehamehaChargeGlow");
        glow.transform.SetParent(effect.transform, false);
        glow.transform.localScale = Vector3.one * 1.42f;
        SpriteRenderer glowRenderer = glow.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = charge;
        glowRenderer.color = new Color(.25f, .95f, 1f, .48f);
        glowRenderer.sortingOrder = KamehamehaSortingOrder(7);
        RuntimeSprite.ApplyUnlit(glowRenderer);

        SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
        renderer.sprite = charge;
        renderer.color = new Color(1f, 1f, 1f, 1f);
        renderer.sortingOrder = KamehamehaSortingOrder(8);
        RuntimeSprite.ApplyUnlit(renderer);

        FideBossChargeEffect pulse = effect.AddComponent<FideBossChargeEffect>();
        pulse.Initialize(renderer, Mathf.Max(SkillTick, duration));
    }

    private void SpawnKamehamehaBeam(Vector2 target, float width, int damage)
    {
        Sprite[] frames = RuntimeSprite.KamehamehaBeamFrames;
        if (frames.Length == 0) return;

        Vector2 origin = (Vector2)transform.position + FacingOffset(kamehamehaBeamOffset);
        Vector2 direction = facingRight ? Vector2.right : Vector2.left;
        float length = GetKamehamehaScreenLength(origin);
        float duration = frames.Length * .38f;
        GameObject beam = new GameObject("FideKamehamehaBeam");
        beam.transform.position = origin;
        beam.transform.right = direction;
        beam.transform.localScale = Vector3.one * 2.35f;

        SpriteRenderer renderer = beam.AddComponent<SpriteRenderer>();
        renderer.sprite = frames[0];
        renderer.color = new Color(.82f, 1f, 1f, .98f);
        renderer.sortingOrder = KamehamehaSortingOrder(6);
        RuntimeSprite.ApplyUnlit(renderer);

        FideBossEffectStrip strip = beam.AddComponent<FideBossEffectStrip>();
        strip.Initialize(frames, renderer, duration / frames.Length);
        SpawnKamehamehaBeamTail(origin, direction, length, duration);
        StartCoroutine(DamagePlayerInKamehamehaBeam(origin, direction, length, width, damage, duration));
    }

    private IEnumerator DamagePlayerInKamehamehaBeam(Vector2 origin, Vector2 direction, float length, float width, int damage, float duration)
    {
        float endTime = Time.time + Mathf.Max(SkillTick, duration);
        nextKamehamehaDamageTime = Mathf.Min(nextKamehamehaDamageTime, Time.time);

        while (Time.time < endTime)
        {
            DamagePlayerInKamehamehaBeam(origin, direction, length, width, damage);
            yield return null;
        }
    }

    private float GetKamehamehaScreenLength(Vector2 origin)
    {
        float edgeX = facingRight ? arenaMaxX : arenaMinX;
        Camera camera = Camera.main;
        if (camera != null)
        {
            float depth = Mathf.Abs(camera.transform.position.z - transform.position.z);
            Vector3 viewportPoint = new Vector3(facingRight ? 1f : 0f, .5f, depth);
            float cameraEdgeX = camera.ViewportToWorldPoint(viewportPoint).x;
            edgeX = facingRight ? Mathf.Max(edgeX, cameraEdgeX) : Mathf.Min(edgeX, cameraEdgeX);
        }

        return Mathf.Max(1f, Mathf.Abs(edgeX - origin.x) + kamehamehaBeamScreenPadding);
    }

    private void SpawnKamehamehaBeamTail(Vector2 origin, Vector2 direction, float totalLength, float duration)
    {
        Sprite[] frames = RuntimeSprite.KamehamehaBeamTailFrames;
        if (frames.Length == 0) return;

        const float headLength = 4.2f;
        float tailLength = Mathf.Max(0f, totalLength - headLength);
        if (tailLength <= .05f) return;

        GameObject tail = new GameObject("FideKamehamehaBeamTail");
        tail.transform.position = origin + direction * headLength;
        tail.transform.right = direction;
        tail.transform.localScale = new Vector3(tailLength, 2.35f, 1f);

        SpriteRenderer renderer = tail.AddComponent<SpriteRenderer>();
        renderer.sprite = frames[0];
        renderer.color = new Color(.82f, 1f, 1f, .96f);
        renderer.sortingOrder = KamehamehaSortingOrder(5);
        RuntimeSprite.ApplyUnlit(renderer);

        FideBossEffectStrip strip = tail.AddComponent<FideBossEffectStrip>();
        strip.Initialize(frames, renderer, duration / frames.Length);
    }

    private int KamehamehaSortingOrder(int offset)
    {
        return (spriteRenderer != null ? spriteRenderer.sortingOrder : 80) + offset;
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
        const int shots = 5;
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

    private IEnumerator SolarBombRoutine()
    {
        if (player == null) yield break;

        currentSkillLabel = "SOLAR BOMB";
        FacePlayerImmediate();
        SpawnAfterimage();
        Vector2 risePoint = new Vector2(transform.position.x, groundY + solarBombHeight);
        yield return MoveTo(risePoint, .28f);

        ShowFrame(21);
        Sprite[] orbFrames = RuntimeSprite.SolarBombFrames;
        if (orbFrames.Length == 0) yield break;

        GameObject orb = new GameObject("FideSolarBomb");
        SpriteRenderer orbRenderer = orb.AddComponent<SpriteRenderer>();
        orbRenderer.sprite = orbFrames[0];
        orbRenderer.color = Color.white;
        orbRenderer.sortingOrder = 90;
        RuntimeSprite.ApplyUnlit(orbRenderer);
        BeginSolarBombDarkening();

        for (int i = 0; i < 7; i++) SpawnSolarBombEnergyStream(orb.transform, 0f);

        float elapsed = 0f;
        float nextEnergyStreamTime = 0f;
        bool warned = false;
        while (elapsed < solarBombChargeDuration)
        {
            if (player == null)
            {
                Destroy(orb);
                RestoreSolarBombLighting();
                yield break;
            }

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(.01f, solarBombChargeDuration));
            float pulse = 1f + Mathf.Sin(elapsed * 22f) * .055f;
            float size = Mathf.Lerp(.28f, solarBombScale, Mathf.SmoothStep(0f, 1f, progress)) * pulse;
            orb.transform.position = transform.position + (Vector3)FacingOffset(new Vector2(.78f, 1.62f));
            orb.transform.localScale = Vector3.one * size;
            orbRenderer.sprite = orbFrames[Mathf.FloorToInt(elapsed / .09f) % orbFrames.Length];
            SampleSkillAnimation(FideSkill.SolarBomb, elapsed, 21);
            UpdateSolarBombLighting(progress);

            if (elapsed >= nextEnergyStreamTime)
            {
                nextEnergyStreamTime = elapsed + solarBombEnergyStreamInterval;
                SpawnSolarBombEnergyStream(orb.transform, progress);
                if (progress > .7f) SpawnSolarBombEnergyStream(orb.transform, progress);
            }

            if (!warned && progress >= .62f)
            {
                warned = true;
                SpawnWarning(player.position, .85f, solarBombChargeDuration * .38f + .18f, new Color(1f, .72f, .08f, .9f));
            }
            yield return null;
        }

        Vector2 aimPoint = player != null ? player.position : (Vector2)transform.position + Vector2.down * 4f;
        Vector2 direction = (aimPoint - (Vector2)orb.transform.position).normalized;
        CircleCollider2D collider = orb.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = .46f;
        Rigidbody2D body = orb.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        FideBossProjectile projectile = orb.AddComponent<FideBossProjectile>();
        projectile.InitializeHoming(playerHealth, direction, solarBombThrowSpeed, solarBombDamage, playerHitInvulnerability);
        FideBossOrbAnimator animator = orb.AddComponent<FideBossOrbAnimator>();
        animator.Initialize(orbFrames, solarBombScale);

        RestoreSolarBombLighting();
        SpawnSourceEffect(33, new Vector2(25f, 52f));
        yield return new WaitForSeconds(.2f);
        yield return MoveTo(new Vector2(transform.position.x, groundY + hoverHeight), .22f);
    }

    private void BeginSolarBombDarkening()
    {
        RestoreSolarBombLighting();
        solarBombLights = FindObjectsByType<Light2D>()
            .Where(light => light != null && light.isActiveAndEnabled)
            .ToArray();
        solarBombLightIntensities = solarBombLights.Select(light => light.intensity).ToArray();
    }

    private void UpdateSolarBombLighting(float chargeProgress)
    {
        float fadeProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(chargeProgress / .58f));
        float lightMultiplier = Mathf.Lerp(1f, solarBombAmbientLight, fadeProgress);
        for (int i = 0; i < solarBombLights.Length; i++)
        {
            if (solarBombLights[i] != null) solarBombLights[i].intensity = solarBombLightIntensities[i] * lightMultiplier;
        }
    }

    private void RestoreSolarBombLighting()
    {
        int count = Mathf.Min(solarBombLights.Length, solarBombLightIntensities.Length);
        for (int i = 0; i < count; i++)
        {
            if (solarBombLights[i] != null) solarBombLights[i].intensity = solarBombLightIntensities[i];
        }
        solarBombLights = Array.Empty<Light2D>();
        solarBombLightIntensities = Array.Empty<float>();
    }

    private void SpawnSolarBombEnergyStream(Transform target, float chargeProgress)
    {
        if (target == null) return;
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float radius = UnityEngine.Random.Range(Mathf.Lerp(4.6f, 2.5f, chargeProgress), Mathf.Lerp(6.2f, 3.5f, chargeProgress));
        Vector2 start = (Vector2)target.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * .65f) * radius;

        GameObject stream = new GameObject("SolarBombEnergyStream");
        stream.transform.position = start;
        SpriteRenderer renderer = stream.AddComponent<SpriteRenderer>();
        renderer.sprite = RuntimeSprite.Beam;
        renderer.color = Color.Lerp(new Color(.35f, .9f, 1f, .9f), new Color(1f, .82f, .18f, .95f), UnityEngine.Random.value);
        renderer.sortingOrder = 89;
        RuntimeSprite.ApplyUnlit(renderer);
        FideBossEnergyStream behaviour = stream.AddComponent<FideBossEnergyStream>();
        behaviour.Initialize(target, UnityEngine.Random.Range(.24f, .42f), UnityEngine.Random.Range(-.55f, .55f), UnityEngine.Random.Range(.55f, .95f));
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
        RuntimeSprite.ApplyUnlit(renderer);
        renderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        Destroy(image, .18f);
    }

    private void SpawnProjectile(Vector2 direction, int damage, float scale, int effectId)
    {
        GameObject projectile = new GameObject("FideProjectile");
        projectile.transform.position = transform.position + (Vector3)(direction * .65f);
        projectile.transform.right = direction.sqrMagnitude > .001f ? direction : Vector2.right;
        SpriteRenderer renderer = projectile.AddComponent<SpriteRenderer>();
        renderer.sprite = RuntimeSprite.EnergyOrb;
        renderer.color = EffectColor(effectId);
        RuntimeSprite.ApplyUnlit(renderer);
        renderer.sortingOrder = 40;
        CircleCollider2D collider = projectile.AddComponent<CircleCollider2D>();
        collider.isTrigger = true; collider.radius = Mathf.Max(.14f, scale);
        Rigidbody2D body = projectile.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f; body.freezeRotation = true; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        FideBossProjectile behaviour = projectile.AddComponent<FideBossProjectile>();
        behaviour.Initialize(direction, projectileSpeed, damage, projectileLifetime, playerHitInvulnerability);
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
        if (IsPlayerInBeam(origin, direction, length, width)) DealDamage(damage, direction * 10f);
    }

    private void DamagePlayerInKamehamehaBeam(Vector2 origin, Vector2 direction, float length, float width, int damage)
    {
        if (playerHealth == null || Time.time < nextKamehamehaDamageTime) return;
        if (!IsPlayerInBeam(origin, direction, length, width)) return;
        nextKamehamehaDamageTime = Time.time + kamehamehaDamageInterval;
        DealDamage(damage, direction * 10f);
    }

    private bool IsPlayerInBeam(Vector2 origin, Vector2 direction, float length, float width)
    {
        if (playerHealth == null) return false;
        Vector2 delta = (Vector2)playerHealth.transform.position - origin;
        float along = Vector2.Dot(delta, direction);
        float sideways = Mathf.Abs(Vector2.Perpendicular(direction).x * delta.x + Vector2.Perpendicular(direction).y * delta.y);
        return along >= 0f && along <= length && sideways <= width;
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

    private void OnBossDied(EnemyHealth _)
    {
        BossKOOverlay.Show();
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
        DamagePlayerInCircle(player.position, 1.8f, 45, 11f);
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

        float speed = moveSpeed;
        float floatingY = groundY + hoverHeight + Mathf.Sin(Time.time * 5f) * .1f;
        Vector3 nextPosition = transform.position + Vector3.right * direction * speed * Time.deltaTime;
        nextPosition.x = Mathf.Clamp(nextPosition.x, arenaMinX, arenaMaxX);
        nextPosition.y = Mathf.Lerp(transform.position.y, floatingY, Time.deltaTime * 7f);
        transform.position = nextPosition;
        ShowFrame(RunFrames[Mathf.FloorToInt(Time.time / SkillTick) % RunFrames.Length]);
    }

    private void MoveTowardsPlayer(float distance)
    {
        float floatingY = groundY + hoverHeight + Mathf.Sin(Time.time * 4f) * .12f;
        if (distance < 2f)
        {
            transform.position = Vector3.Lerp(transform.position, new Vector3(transform.position.x, floatingY, transform.position.z), Time.deltaTime * 5f);
            ShowIdle();
            return;
        }
        float speed = moveSpeed;
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

    private void SampleSkillAnimation(FideSkill skill, float time, int fallbackFrame)
    {
        if (skill == FideSkill.Kamehameha)
        {
            ShowFrame(fallbackFrame);
            return;
        }

        AnimationClip clip = animationLibrary != null ? animationLibrary.GetClip((int)skill) : null;
        if (clip == null)
        {
            ShowFrame(fallbackFrame);
            return;
        }

        clip.SampleAnimation(gameObject, Mathf.Clamp(time, 0f, clip.length));
        currentFrameIndex = fallbackFrame;
        if (spriteRenderer != null) spriteRenderer.flipX = !facingRight;
    }

    private Vector2 FacingOffset(Vector2 offset) => new Vector2(offset.x * (facingRight ? 1f : -1f), offset.y);
    private void SpawnSourceEffect(int id, Vector2 offset)
    {
        Sprite[] frames = GetEffectFrames(id);
        if (frames.Length == 0) return;
        GameObject effect = new GameObject($"FideEffect_{id}");
        effect.transform.position = transform.position + (Vector3)FacingOffset(offset / 32f);
        SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 35;
        renderer.flipX = !facingRight;
        renderer.color = EffectColor(id);
        RuntimeSprite.ApplyUnlit(renderer);
        FideBossEffectStrip strip = effect.AddComponent<FideBossEffectStrip>();
        strip.Initialize(frames, renderer, SkillTick);
    }

    private Sprite[] GetEffectFrames(int id)
    {
        if (effectFrames.TryGetValue(id, out Sprite[] cached)) return cached;
        Sprite[] frames = RuntimeSprite.CreateFideEffectFrames(id);
        if (frames.Length == 0) frames = RuntimeSprite.CreateFideEffectFrames(35);
        return effectFrames[id] = frames ?? Array.Empty<Sprite>();
    }

    private static Color EffectColor(int id)
    {
        switch (id)
        {
            case 2: return new Color(1f, .82f, .18f, .95f);
            case 4: return new Color(1f, .42f, .08f, .95f);
            case 9: return new Color(.38f, .9f, 1f, .9f);
            case 28:
            case 29:
            case 30: return new Color(.95f, .35f, 1f, .95f);
            case 32: return new Color(.62f, .35f, 1f, .92f);
            case 33: return new Color(1f, .88f, .18f, .95f);
            case 34: return new Color(1f, .38f, .08f, .95f);
            case 35: return new Color(1f, .22f, .9f, .95f);
            default: return Color.white;
        }
    }

    private void SpawnWarning(Vector2 point, float radius, float duration, Color color)
    {
        GameObject warning = new GameObject("FideWarning");
        warning.transform.position = point;
        warning.transform.localScale = Vector3.one * radius * 2f;
        SpriteRenderer renderer = warning.AddComponent<SpriteRenderer>();
        renderer.sprite = RuntimeSprite.Ring;
        renderer.color = color;
        RuntimeSprite.ApplyUnlit(renderer);
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
        renderer.sprite = RuntimeSprite.Beam;
        renderer.color = color;
        RuntimeSprite.ApplyUnlit(renderer);
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
        Add(new SkillDefinition(FideSkill.SolarBomb, "Solar Bomb", 2, 2f, 14f, 6.8f, Array.Empty<int>(), Array.Empty<Cue>()));
        Add(new SkillDefinition(FideSkill.Kamehameha, "Kamehameha", 2, 3f, 14f, 4.6f, Enumerable.Repeat(19, 24).Concat(Enumerable.Repeat(20, 38)).ToArray(), new[] { new Cue(0, 0, Vector2.zero, CueAction.KamehamehaCharge, 1.15f), new Cue(24, 0, Vector2.zero, CueAction.KamehamehaBeam, .62f, 46) }));
    }

    private void Add(SkillDefinition skill) => skills[skill.Id] = skill;

    private void HandleKeyboardTest()
    {
        if (!keyboardSkillTesting) return;
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        FideSkill[] selectedSkills = shiftHeld ? ShiftNumberSkills : NumberSkills;
        for (int i = 0; i < selectedSkills.Length; i++)
        {
            if (!Input.GetKeyDown(NumberRowKeys[i]) && !Input.GetKeyDown(NumberPadKeys[i])) continue;
            StartSkill(skills[selectedSkills[i]]);
            break;
        }
        if (Input.GetKeyDown(KeyCode.F1)) aiEnabled = !aiEnabled;
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
    private PlayerHealth homingTarget;
    private Collider2D homingTargetCollider;
    private float homingSpeed;
    private bool guaranteedHoming;
    private bool solarBombImpact;
    private bool consumed;

    public void Initialize(Vector2 direction, float speed, int newDamage, float newLifetime, float newInvulnerability)
    {
        damage = newDamage; lifetime = newLifetime; invulnerability = newInvulnerability;
        body = GetComponent<Rigidbody2D>();
        body.linearVelocity = direction.normalized * speed;
    }

    public void InitializeHoming(PlayerHealth target, Vector2 initialDirection, float speed, int newDamage, float newInvulnerability)
    {
        damage = newDamage;
        invulnerability = newInvulnerability;
        homingTarget = target;
        homingTargetCollider = target != null ? target.GetComponentInChildren<Collider2D>() : null;
        homingSpeed = Mathf.Max(1f, speed);
        guaranteedHoming = target != null;
        solarBombImpact = true;
        body = GetComponent<Rigidbody2D>();
        if (body != null) body.linearVelocity = initialDirection.normalized * homingSpeed;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (!guaranteedHoming && age >= lifetime) Destroy(gameObject);
    }

    private void FixedUpdate()
    {
        if (!guaranteedHoming || consumed) return;
        if (homingTarget == null || !homingTarget.isActiveAndEnabled)
        {
            Destroy(gameObject);
            return;
        }

        Vector2 targetPoint = homingTargetCollider != null ? homingTargetCollider.bounds.center : homingTarget.transform.position;
        Vector2 delta = targetPoint - (Vector2)transform.position;
        float catchDistance = Mathf.Max(.6f, homingSpeed * Time.fixedDeltaTime * 1.2f);
        if (delta.sqrMagnitude <= catchDistance * catchDistance)
        {
            HitTarget(homingTarget, true);
            return;
        }

        if (body != null) body.linearVelocity = delta.normalized * homingSpeed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerHealth target = other.GetComponentInParent<PlayerHealth>();
        if (target == null) return;
        HitTarget(target, guaranteedHoming && target == homingTarget);
    }

    private void HitTarget(PlayerHealth target, bool forceHit)
    {
        if (consumed || target == null) return;
        if (!forceHit && nextDamageTimes.TryGetValue(target, out float nextDamageTime) && Time.time < nextDamageTime)
        {
            consumed = true;
            Destroy(gameObject);
            return;
        }

        consumed = true;
        nextDamageTimes[target] = Time.time + invulnerability;
        if (solarBombImpact) SpawnSolarBombImpact(transform.position);
        target.TakeDamage(damage);
        Rigidbody2D targetBody = target.GetComponent<Rigidbody2D>();
        if (targetBody != null && body != null)
        {
            float horizontalDirection = Mathf.Abs(body.linearVelocity.x) > .01f ? Mathf.Sign(body.linearVelocity.x) : 1f;
            targetBody.linearVelocity = new Vector2(horizontalDirection * 4.5f, Mathf.Clamp(Mathf.Max(targetBody.linearVelocity.y, 1f), -4f, 4f));
        }
        Destroy(gameObject);
    }

    private static void SpawnSolarBombImpact(Vector2 point)
    {
        GameObject explosion = new GameObject("SolarBombExplosion");
        explosion.transform.position = point;
        SpriteRenderer renderer = explosion.AddComponent<SpriteRenderer>();
        Sprite[] frames = RuntimeSprite.SolarBombFrames;
        renderer.sprite = frames.Length > 0 ? frames[0] : RuntimeSprite.EnergyOrb;
        renderer.sortingOrder = 200;
        RuntimeSprite.ApplyUnlit(renderer);
        explosion.AddComponent<FideBossSolarBombExplosion>().Initialize(renderer);

        GameObject flash = new GameObject("SolarBombScreenFlash");
        flash.AddComponent<FideBossScreenFlash>().Initialize(.28f);
    }
}

public sealed class FideBossSolarBombExplosion : MonoBehaviour
{
    private const float Duration = .48f;
    private SpriteRenderer core;
    private SpriteRenderer firstRing;
    private SpriteRenderer secondRing;
    private float elapsed;

    public void Initialize(SpriteRenderer coreRenderer)
    {
        core = coreRenderer;
        firstRing = CreateRing("SolarBombShockwaveA", 199);
        secondRing = CreateRing("SolarBombShockwaveB", 198);
        transform.localScale = Vector3.one * .45f;
    }

    private SpriteRenderer CreateRing(string objectName, int sortingOrder)
    {
        GameObject ring = new GameObject(objectName);
        ring.transform.SetParent(transform, false);
        SpriteRenderer renderer = ring.AddComponent<SpriteRenderer>();
        renderer.sprite = RuntimeSprite.Ring;
        renderer.sortingOrder = sortingOrder;
        renderer.color = new Color(1f, .82f, .18f, .95f);
        RuntimeSprite.ApplyUnlit(renderer);
        return renderer;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / Duration);
        float burst = 1f - Mathf.Pow(1f - progress, 3f);
        transform.localScale = Vector3.one * Mathf.Lerp(.45f, 4.8f, burst);

        if (core != null)
        {
            Color color = Color.Lerp(Color.white, new Color(1f, .48f, .05f), progress);
            color.a = 1f - Mathf.SmoothStep(.28f, 1f, progress);
            core.color = color;
        }

        UpdateRing(firstRing, progress, 1f, .95f);
        float delayedProgress = Mathf.Clamp01((progress - .14f) / .86f);
        UpdateRing(secondRing, delayedProgress, .72f, .7f);
        if (progress >= 1f) Destroy(gameObject);
    }

    private static void UpdateRing(SpriteRenderer ring, float progress, float scaleMultiplier, float alpha)
    {
        if (ring == null) return;
        ring.transform.localScale = Vector3.one * Mathf.Lerp(.65f, 1.75f * scaleMultiplier, Mathf.SmoothStep(0f, 1f, progress));
        Color color = ring.color;
        color.a = (1f - progress) * alpha;
        ring.color = color;
    }
}

public sealed class FideBossScreenFlash : MonoBehaviour
{
    private float duration;
    private float elapsed;

    public void Initialize(float flashDuration)
    {
        duration = Mathf.Max(.08f, flashDuration);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= duration) Destroy(gameObject);
    }

    private void OnGUI()
    {
        float progress = Mathf.Clamp01(elapsed / duration);
        float alpha = (1f - Mathf.SmoothStep(0f, 1f, progress)) * .92f;
        Color previousColor = GUI.color;
        GUI.depth = -1000;
        GUI.color = new Color(1f, .94f, .68f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;
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

public sealed class FideBossChargeEffect : MonoBehaviour
{
    private SpriteRenderer targetRenderer;
    private float duration;
    private float elapsed;
    private float baseAlpha;
    private float baseScale = 1f;

    public void Initialize(SpriteRenderer renderer, float lifeTime)
    {
        targetRenderer = renderer;
        duration = Mathf.Max(.05f, lifeTime);
        baseAlpha = renderer != null ? renderer.color.a : 1f;
        baseScale = Mathf.Max(.01f, transform.localScale.x);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / duration);
        float pulse = 1f + Mathf.Sin(elapsed * 36f) * .12f;
        transform.localScale = Vector3.one * (baseScale * pulse);
        transform.Rotate(0f, 0f, 180f * Time.deltaTime);

        if (targetRenderer != null)
        {
            Color color = targetRenderer.color;
            color.a = baseAlpha * (1f - Mathf.SmoothStep(.65f, 1f, progress));
            targetRenderer.color = color;
        }

        if (progress >= 1f) Destroy(gameObject);
    }
}

public sealed class FideBossEnergyStream : MonoBehaviour
{
    private Transform target;
    private SpriteRenderer targetRenderer;
    private Vector2 start;
    private float duration;
    private float curve;
    private float streamLength;
    private float elapsed;
    private float baseAlpha;

    public void Initialize(Transform destination, float travelDuration, float curveAmount, float length)
    {
        target = destination;
        targetRenderer = GetComponent<SpriteRenderer>();
        start = transform.position;
        duration = Mathf.Max(.08f, travelDuration);
        curve = curveAmount;
        streamLength = length;
        baseAlpha = targetRenderer != null ? targetRenderer.color.a : 1f;
    }

    private void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / duration);
        Vector2 end = target.position;
        Vector2 travel = end - start;
        Vector2 perpendicular = travel.sqrMagnitude > .001f ? Vector2.Perpendicular(travel.normalized) : Vector2.up;
        float eased = Mathf.SmoothStep(0f, 1f, progress);
        Vector2 position = Vector2.Lerp(start, end, eased) + perpendicular * (Mathf.Sin(progress * Mathf.PI) * curve * (1f - progress));
        transform.position = position;

        Vector2 direction = end - position;
        if (direction.sqrMagnitude > .001f) transform.right = direction;
        transform.localScale = new Vector3(Mathf.Lerp(streamLength, .08f, progress), Mathf.Lerp(.1f, .025f, progress), 1f);
        if (targetRenderer != null)
        {
            Color color = targetRenderer.color;
            color.a = baseAlpha * Mathf.Sin(progress * Mathf.PI);
            targetRenderer.color = color;
        }

        if (progress >= 1f) Destroy(gameObject);
    }
}

public sealed class FideBossOrbAnimator : MonoBehaviour
{
    private Sprite[] frames;
    private SpriteRenderer targetRenderer;
    private float baseScale;
    private float elapsed;

    public void Initialize(Sprite[] sourceFrames, float scale)
    {
        frames = sourceFrames;
        baseScale = scale;
        targetRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0 || targetRenderer == null) return;
        elapsed += Time.deltaTime;
        targetRenderer.sprite = frames[Mathf.FloorToInt(elapsed / .09f) % frames.Length];
        transform.localScale = Vector3.one * baseScale * (1f + Mathf.Sin(elapsed * 18f) * .045f);
        transform.Rotate(0f, 0f, 75f * Time.deltaTime);
    }
}

internal static class RuntimeSprite
{
    private static Sprite white;
    private static Sprite ring;
    private static Sprite energyOrb;
    private static Sprite beam;
    private static Sprite kamehamehaCharge;
    private static Sprite[] kamehamehaBeamFrames;
    private static Sprite[] kamehamehaBeamTailFrames;
    private static Sprite[] solarBombFrames;
    private static Material unlitMaterial;
    private const string KenneyPath = "Effects/KenneyParticlePack/";
    public static Sprite White => white ??= CreateWhite();
    public static Sprite Ring => ring ??= CreateRing();
    public static Sprite EnergyOrb => energyOrb ??= LoadKenneySprite("magic_03", 512f) ?? CreateEnergyOrb();
    public static Sprite Beam => beam ??= LoadKenneySprite("trace_03", 512f) ?? CreateBeam();
    public static Sprite KamehamehaCharge => kamehamehaCharge ??= LoadFideSprite("51", 96f, new Vector2(.5f, .5f));
    public static Sprite[] KamehamehaBeamFrames => kamehamehaBeamFrames ??= LoadKamehamehaBeamFrames();
    public static Sprite[] KamehamehaBeamTailFrames => kamehamehaBeamTailFrames ??= LoadKamehamehaBeamTailFrames();
    public static Sprite[] SolarBombFrames => solarBombFrames ??= LoadSolarBombFrames();

    public static void ApplyUnlit(SpriteRenderer renderer)
    {
        if (renderer == null) return;
        unlitMaterial ??= CreateUnlitMaterial();
        if (unlitMaterial != null) renderer.sharedMaterial = unlitMaterial;
    }

    private static Material CreateUnlitMaterial()
    {
        Shader shader =
            Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ??
            Shader.Find("Sprites/Default");
        return shader != null ? new Material(shader) : null;
    }

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

    public static Sprite[] CreateFideEffectFrames(int id)
    {
        switch (id)
        {
            case 2:
            case 4:
                return LoadKenneyFrames(new[] { "muzzle_01", "muzzle_02", "muzzle_03", "muzzle_04" }, 512f, CreateImpactFrames(id == 4 ? 1.15f : .95f));
            case 9:
                return LoadKenneyFrames(new[] { "twirl_01", "twirl_02", "twirl_03", "circle_03" }, 512f, CreateArcFrames());
            case 28:
            case 29:
            case 30:
                return LoadKenneyFrames(new[] { "slash_01", "slash_02", "slash_03", "slash_04" }, 512f, CreateSlashFrames());
            case 32:
            case 33:
            case 34:
            case 35:
                return LoadKenneyFrames(new[] { "magic_01", "magic_02", "magic_03", "magic_04", "flare_01", "light_02" }, 512f, CreateChargeFrames(id));
            default:
                return Array.Empty<Sprite>();
        }
    }

    private static Sprite[] LoadKenneyFrames(string[] names, float pixelsPerUnit, Sprite[] fallback)
    {
        Sprite[] frames = names
            .Select(name => LoadKenneySprite(name, pixelsPerUnit))
            .Where(sprite => sprite != null)
            .ToArray();
        return frames.Length > 0 ? frames : fallback;
    }

    private static Sprite LoadKenneySprite(string name, float pixelsPerUnit)
    {
        Texture2D texture = Resources.Load<Texture2D>(KenneyPath + name);
        if (texture == null) return null;
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), pixelsPerUnit);
    }

    private static Sprite LoadFideSprite(string name, float pixelsPerUnit, Vector2 pivot)
    {
        Texture2D texture = Resources.Load<Texture2D>("Effects/Fide/" + name);
        if (texture == null) return null;
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, pixelsPerUnit);
    }

    private static Sprite[] LoadKamehamehaBeamFrames()
    {
        Texture2D texture = Resources.Load<Texture2D>("Effects/Fide/kamehameha");
        if (texture == null) return Array.Empty<Sprite>();

        texture.filterMode = FilterMode.Bilinear;
        const float pixelsPerUnit = 360f;
        return new[]
        {
            Sprite.Create(texture, RectFromTop(texture, 1850, 2025), new Vector2(0f, .5f), pixelsPerUnit),
            Sprite.Create(texture, RectFromTop(texture, 1510, 1680), new Vector2(0f, .5f), pixelsPerUnit),
            Sprite.Create(texture, RectFromTop(texture, 1090, 1345), new Vector2(0f, .5f), pixelsPerUnit),
            Sprite.Create(texture, RectFromTop(texture, 555, 940), new Vector2(0f, .5f), pixelsPerUnit),
            Sprite.Create(texture, RectFromTop(texture, 60, 455), new Vector2(0f, .5f), pixelsPerUnit)
        };
    }

    private static Sprite[] LoadKamehamehaBeamTailFrames()
    {
        Texture2D texture = Resources.Load<Texture2D>("Effects/Fide/kamehameha");
        if (texture == null) return Array.Empty<Sprite>();

        texture.filterMode = FilterMode.Bilinear;
        const float pixelsPerUnit = 360f;
        const int tailX = 360;
        return new[]
        {
            Sprite.Create(texture, RectFromTop(texture, 1850, 2025, tailX, texture.width - tailX), new Vector2(0f, .5f), pixelsPerUnit),
            Sprite.Create(texture, RectFromTop(texture, 1510, 1680, tailX, texture.width - tailX), new Vector2(0f, .5f), pixelsPerUnit),
            Sprite.Create(texture, RectFromTop(texture, 1090, 1345, tailX, texture.width - tailX), new Vector2(0f, .5f), pixelsPerUnit),
            Sprite.Create(texture, RectFromTop(texture, 555, 940, tailX, texture.width - tailX), new Vector2(0f, .5f), pixelsPerUnit),
            Sprite.Create(texture, RectFromTop(texture, 60, 455, tailX, texture.width - tailX), new Vector2(0f, .5f), pixelsPerUnit)
        };
    }

    private static Rect RectFromTop(Texture2D texture, int topInclusive, int bottomInclusive)
    {
        int height = bottomInclusive - topInclusive + 1;
        return new Rect(0, texture.height - bottomInclusive - 1, texture.width, height);
    }

    private static Rect RectFromTop(Texture2D texture, int topInclusive, int bottomInclusive, int x, int width)
    {
        int height = bottomInclusive - topInclusive + 1;
        return new Rect(x, texture.height - bottomInclusive - 1, width, height);
    }

    private static Sprite[] LoadSolarBombFrames()
    {
        Texture2D texture = Resources.Load<Texture2D>("Effects/Fide/Skills_25_1_4");
        if (texture == null) return Array.Empty<Sprite>();

        texture.filterMode = FilterMode.Bilinear;
        int frameHeight = texture.height / 2;
        float pixelsPerUnit = texture.width * .5f;
        return new[]
        {
            Sprite.Create(texture, new Rect(0, frameHeight, texture.width, frameHeight), new Vector2(.5f, .5f), pixelsPerUnit),
            Sprite.Create(texture, new Rect(0, 0, texture.width, frameHeight), new Vector2(.5f, .5f), pixelsPerUnit)
        };
    }

    private static Sprite CreateEnergyOrb()
    {
        const int size = 48;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Vector2 center = Vector2.one * (size - 1) * .5f;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            Vector2 point = new Vector2(x, y);
            float distance = Vector2.Distance(point, center) / (size * .5f);
            float glow = Mathf.Clamp01(1f - distance);
            float core = Mathf.SmoothStep(.72f, 1f, glow);
            float alpha = Mathf.Clamp01(glow * glow * 1.25f);
            Color color = Color.Lerp(new Color(.75f, .05f, 1f, 0f), Color.white, core);
            color.a = alpha;
            texture.SetPixel(x, y, color);
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
    }

    private static Sprite CreateBeam()
    {
        const int width = 64;
        const int height = 16;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float centerY = (height - 1) * .5f;
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            float vertical = Mathf.Abs(y - centerY) / centerY;
            float cap = Mathf.Min(x, width - 1 - x) / 8f;
            float alpha = Mathf.Clamp01((1f - vertical) * Mathf.Clamp01(cap));
            float core = Mathf.SmoothStep(.55f, 1f, 1f - vertical);
            Color color = Color.Lerp(new Color(1f, .15f, .9f, 0f), Color.white, core);
            color.a = alpha;
            texture.SetPixel(x, y, color);
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 16f);
    }

    private static Sprite[] CreateImpactFrames(float scale)
    {
        Sprite[] frames = new Sprite[5];
        for (int i = 0; i < frames.Length; i++) frames[i] = CreateRadialSprite(40, .18f + i * .12f * scale, .92f - i * .12f, true);
        return frames;
    }

    private static Sprite[] CreateChargeFrames(int id)
    {
        Sprite[] frames = new Sprite[6];
        for (int i = 0; i < frames.Length; i++)
        {
            float radius = id == 35 ? .3f + i * .085f : .2f + i * .06f;
            frames[i] = CreateRadialSprite(56, radius, .75f, i % 2 == 0);
        }
        return frames;
    }

    private static Sprite[] CreateSlashFrames()
    {
        Sprite[] frames = new Sprite[5];
        for (int i = 0; i < frames.Length; i++) frames[i] = CreateSlashSprite(56, -35f + i * 13f, .9f - i * .08f);
        return frames;
    }

    private static Sprite[] CreateArcFrames()
    {
        Sprite[] frames = new Sprite[4];
        for (int i = 0; i < frames.Length; i++) frames[i] = CreateRingArcSprite(52, .46f + i * .08f, .85f - i * .12f);
        return frames;
    }

    private static Sprite CreateRadialSprite(int size, float radius, float opacity, bool ringCore)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Vector2 center = Vector2.one * (size - 1) * .5f;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), center) / (size * .5f);
            float glow = Mathf.Clamp01(1f - distance / Mathf.Max(radius, .01f));
            float rim = ringCore ? Mathf.Clamp01(1f - Mathf.Abs(distance - radius) * 12f) : 0f;
            float alpha = Mathf.Clamp01(glow * .75f + rim) * opacity;
            float core = Mathf.SmoothStep(.6f, 1f, glow);
            Color color = Color.Lerp(new Color(.95f, .08f, 1f, 0f), Color.white, core);
            color.a = alpha;
            texture.SetPixel(x, y, color);
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
    }

    private static Sprite CreateSlashSprite(int size, float angleDegrees, float opacity)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Vector2 center = Vector2.one * (size - 1) * .5f;
        float angle = angleDegrees * Mathf.Deg2Rad;
        Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 normal = new Vector2(-axis.y, axis.x);
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            Vector2 p = (new Vector2(x, y) - center) / (size * .5f);
            float along = Vector2.Dot(p, axis);
            float across = Mathf.Abs(Vector2.Dot(p, normal));
            float blade = Mathf.Clamp01(1f - across * 7f) * Mathf.Clamp01(1f - Mathf.Abs(along) * .9f);
            blade *= Mathf.SmoothStep(-.85f, -.35f, along) * (1f - Mathf.SmoothStep(.45f, .95f, along));
            Color color = Color.Lerp(new Color(.75f, .05f, 1f, 0f), Color.white, Mathf.SmoothStep(.45f, 1f, blade));
            color.a = blade * opacity;
            texture.SetPixel(x, y, color);
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
    }

    private static Sprite CreateRingArcSprite(int size, float radius, float opacity)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Vector2 center = Vector2.one * (size - 1) * .5f;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            Vector2 p = (new Vector2(x, y) - center) / (size * .5f);
            float distance = p.magnitude;
            float angle = Mathf.Atan2(p.y, p.x);
            float arcMask = angle > -2.6f && angle < .8f ? 1f : 0f;
            float ring = Mathf.Clamp01(1f - Mathf.Abs(distance - radius) * 11f) * arcMask;
            Color color = Color.Lerp(new Color(.1f, .8f, 1f, 0f), Color.white, Mathf.SmoothStep(.5f, 1f, ring));
            color.a = ring * opacity;
            texture.SetPixel(x, y, color);
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
    }
}
