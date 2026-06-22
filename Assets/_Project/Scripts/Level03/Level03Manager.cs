using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Level03State
{
    BossShielded,
    CollectingFragments,
    DragonGemMerging,
    BossVulnerable,
    BossDefeated
}

[DisallowMultipleComponent]
public class Level03Manager : MonoBehaviour
{
    private sealed class PlatformGroupRuntime
    {
        public readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        public readonly List<Collider2D> colliders = new List<Collider2D>();
    }

    [Header("Level 02 Platform Visual")]
    [SerializeField] private Sprite platformBlockSprite;
    [SerializeField] private Color platformColor = Color.white;

    [Header("Dragon Gem Assets")]
    [SerializeField] private Texture2D fragmentTexture;
    [SerializeField] private Texture2D completeGemTexture;
    [SerializeField] private Texture2D mergeEffectTexture;
    [SerializeField] private TextAsset mergeEffectData;

    [Header("Encounter Tuning")]
    [SerializeField] private int requiredFragments = 2;
    [SerializeField] private float platformFadeDelay = 0.15f;
    [SerializeField] private float platformTransitionDuration = 0.25f;
    [SerializeField] private float fragmentPixelsPerUnit = 512f;
    [SerializeField] private Vector3 mergePoint = new Vector3(0f, 15.0f, 0f);

    private readonly List<Level03DragonFragment> fragments = new List<Level03DragonFragment>();
    private PlatformGroupRuntime platformGroupA;
    private PlatformGroupRuntime platformGroupB;
    private PlayerMovement playerMovement;
    private PlayerInputReader playerInput;
    private EnemyHealth bossHealth;
    private EnemyPatrol bossPatrol;
    private Level03BossShield bossShield;
    private DragonGemEffectPlayer mergeEffect;
    private SpriteRenderer completeGemRenderer;
    private Coroutine platformTransition;
    private bool isGroupAActive = true;
    private int collectedFragments;
    private string announcement;
    private float announcementUntil;
    private GUIStyle counterStyle;
    private GUIStyle announcementStyle;
    private Material spriteUnlitMaterial;

    public Level03State CurrentState { get; private set; } = Level03State.BossShielded;
    public bool CanCollectFragments => CurrentState == Level03State.BossShielded
        || CurrentState == Level03State.CollectingFragments;

    private IEnumerator Start()
    {
        yield return null;

        if (!ResolveSceneActors())
        {
            enabled = false;
            yield break;
        }

        BuildEncounterHierarchy();
        bossShield.ActivateShield(this);
        bossHealth.Died += HandleBossDied;
        playerMovement.JumpPerformed += HandlePlayerJumpPerformed;

        CurrentState = Level03State.BossShielded;
        ShowAnnouncement("Boss đang được bảo vệ - hãy tìm 2 mảnh Ngọc Rồng!", 2f);
        yield return new WaitForSeconds(0.8f);

        if (CurrentState == Level03State.BossShielded)
        {
            CurrentState = Level03State.CollectingFragments;
        }
    }

    private void OnDestroy()
    {
        if (playerMovement != null)
        {
            playerMovement.JumpPerformed -= HandlePlayerJumpPerformed;
        }

        if (bossHealth != null)
        {
            bossHealth.Died -= HandleBossDied;
        }

        if (spriteUnlitMaterial != null)
        {
            Destroy(spriteUnlitMaterial);
        }
    }

    public void NotifyShieldHit()
    {
        if (CanCollectFragments)
        {
            ShowAnnouncement("Shielded! Thu thập đủ 2 mảnh Ngọc Rồng.", 0.75f);
        }
    }

    public void CollectFragment(Level03DragonFragment fragment)
    {
        if (!CanCollectFragments || fragment == null)
        {
            return;
        }

        collectedFragments = Mathf.Min(collectedFragments + 1, requiredFragments);
        ShowAnnouncement($"Dragon Fragment: {collectedFragments}/{requiredFragments}", 1f);

        if (collectedFragments >= requiredFragments)
        {
            StartCoroutine(MergeDragonGem());
        }
    }

    private bool ResolveSceneActors()
    {
        playerMovement = FindAnyObjectByType<PlayerMovement>();
        if (playerMovement != null)
        {
            playerInput = playerMovement.GetComponent<PlayerInputReader>();
        }

        Mob77JsonAnimationBridge mob77 = FindAnyObjectByType<Mob77JsonAnimationBridge>();
        if (mob77 != null)
        {
            bossHealth = mob77.GetComponent<EnemyHealth>();
            bossPatrol = mob77.GetComponent<EnemyPatrol>();
            bossShield = mob77.GetComponent<Level03BossShield>();
            if (bossShield == null)
            {
                bossShield = mob77.gameObject.AddComponent<Level03BossShield>();
            }
        }

        if (playerMovement == null || bossHealth == null || bossShield == null)
        {
            Debug.LogError("Level03Manager requires PlayerMovement and the Mob77 boss in Level_03.", this);
            return false;
        }

        return true;
    }

    private void BuildEncounterHierarchy()
    {
        Transform flyingPlatforms = CreateRoot("FlyingPlatforms", transform);
        Transform groupARoot = CreateRoot("PlatformGroup_A", flyingPlatforms);
        Transform groupBRoot = CreateRoot("PlatformGroup_B", flyingPlatforms);

        platformGroupA = new PlatformGroupRuntime();
        platformGroupB = new PlatformGroupRuntime();

        Vector3 platformA1 = new Vector3(-3.5f, -2.5f, 0f);
        Vector3 platformA2 = new Vector3(3.0f, 1.0f, 0f);
        Vector3 platformA3 = new Vector3(-4.0f, 5.0f, 0f);
        Vector3 platformA4 = new Vector3(3.5f, 9.0f, 0f);
        Vector3 platformA5 = new Vector3(-2.5f, 13.0f, 0f);

        Vector3 platformB1 = new Vector3(0.0f, -1.0f, 0f);
        Vector3 platformB2 = new Vector3(-1.0f, 3.0f, 0f);
        Vector3 platformB3 = new Vector3(0.0f, 7.0f, 0f);
        Vector3 platformB4 = new Vector3(1.0f, 11.0f, 0f);
        Vector3 platformB5 = new Vector3(1.0f, 15.0f, 0f);

        CreatePlatform("Platform_A1", platformA1, groupARoot, platformGroupA);
        CreatePlatform("Platform_A2", platformA2, groupARoot, platformGroupA);
        CreatePlatform("Platform_A3", platformA3, groupARoot, platformGroupA);
        CreatePlatform("Platform_A4", platformA4, groupARoot, platformGroupA);
        CreatePlatform("Platform_A5", platformA5, groupARoot, platformGroupA);

        CreatePlatform("Platform_B1", platformB1, groupBRoot, platformGroupB);
        CreatePlatform("Platform_B2", platformB2, groupBRoot, platformGroupB);
        CreatePlatform("Platform_B3", platformB3, groupBRoot, platformGroupB);
        CreatePlatform("Platform_B4", platformB4, groupBRoot, platformGroupB);
        CreatePlatform("Platform_B5", platformB5, groupBRoot, platformGroupB);

        SetPlatformGroupState(platformGroupA, 1f, true);
        SetPlatformGroupState(platformGroupB, 0f, false);

        Transform fragmentRoot = CreateRoot("DragonFragments", transform);
        CreateFragments(
            fragmentRoot,
            new Vector3[] { platformA1, platformA2, platformA3, platformA4, platformA5 },
            new Vector3[] { platformB1, platformB2, platformB3, platformB4, platformB5 });

        Transform completeRoot = CreateRoot("DragonGemComplete", transform);
        completeRoot.position = mergePoint;
        completeGemRenderer = completeRoot.gameObject.AddComponent<SpriteRenderer>();
        completeGemRenderer.sprite = CreateFullTextureSprite(completeGemTexture, fragmentPixelsPerUnit);
        completeGemRenderer.sharedMaterial = GetSpriteUnlitMaterial();
        completeGemRenderer.sortingOrder = 25;
        completeGemRenderer.transform.localScale = Vector3.one * 0.65f;
        completeGemRenderer.enabled = false;

        Transform effectsRoot = CreateRoot("Effects", transform);
        Transform mergeEffectRoot = CreateRoot("MergeEffect", effectsRoot);
        mergeEffectRoot.position = mergePoint;
        mergeEffect = mergeEffectRoot.gameObject.AddComponent<DragonGemEffectPlayer>();
        mergeEffect.Configure(mergeEffectTexture, mergeEffectData);
    }

    private void CreateFragments(
        Transform parent,
        Vector3[] platformsA,
        Vector3[] platformsB)
    {
        if (fragmentTexture == null)
        {
            Debug.LogError("Level03Manager is missing dragon_fragments.png.", this);
            return;
        }

        int halfWidth = fragmentTexture.width / 2;
        Color32[] pixels = fragmentTexture.GetPixels32();
        Sprite leftFragment = CreateTrimmedSprite(
            fragmentTexture,
            pixels,
            0,
            halfWidth,
            fragmentPixelsPerUnit);
        Sprite rightFragment = CreateTrimmedSprite(
            fragmentTexture,
            pixels,
            halfWidth,
            fragmentTexture.width,
            fragmentPixelsPerUnit);

        Vector3 fragmentStandOffset = new Vector3(0f, 0.06f, 0f);

        Vector3[] pathOne =
        {
            platformsA[1] + fragmentStandOffset, // A2
            platformsB[2] + fragmentStandOffset, // B3
            platformsA[3] + fragmentStandOffset, // A4
            platformsB[4] + fragmentStandOffset  // B5
        };
        
        Vector3[] pathTwo =
        {
            platformsB[1] + fragmentStandOffset, // B2
            platformsA[2] + fragmentStandOffset, // A3
            platformsB[3] + fragmentStandOffset, // B4
            platformsA[4] + fragmentStandOffset  // A5
        };

        fragments.Add(CreateFragment("Fragment_01", 1, leftFragment, pathOne, parent));
        fragments.Add(CreateFragment("Fragment_02", 2, rightFragment, pathTwo, parent));
    }

    private Level03DragonFragment CreateFragment(
        string objectName,
        int index,
        Sprite sprite,
        Vector3[] path,
        Transform parent)
    {
        GameObject fragmentObject = new GameObject(objectName);
        fragmentObject.transform.SetParent(parent, true);
        fragmentObject.transform.localScale = Vector3.one * 0.8f;
        Level03DragonFragment fragment = fragmentObject.AddComponent<Level03DragonFragment>();
        fragment.Initialize(this, index, sprite, path, GetSpriteUnlitMaterial());
        return fragment;
    }

    private void CreatePlatform(
        string objectName,
        Vector3 position,
        Transform parent,
        PlatformGroupRuntime group)
    {
        GameObject platform = new GameObject(objectName);
        platform.layer = LayerMask.NameToLayer("Ground");
        platform.transform.SetParent(parent, true);
        platform.transform.position = position;

        BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(2.6f, 0.15f);
        collider.offset = new Vector2(0f, -0.075f);
        group.colliders.Add(collider);

        GameObject visual = new GameObject("BlockVisual");
        visual.transform.SetParent(platform.transform, false);

        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = platformBlockSprite;
        renderer.sharedMaterial = GetSpriteUnlitMaterial();
        renderer.color = platformColor;
        renderer.sortingOrder = 2;

        Vector3 visualScale = new Vector3(1.35f, 0.22f, 1f);
        visual.transform.localScale = visualScale;
        if (platformBlockSprite != null)
        {
            Bounds bounds = platformBlockSprite.bounds;
            visual.transform.localPosition = new Vector3(
                -bounds.center.x * visualScale.x,
                -bounds.max.y * visualScale.y,
                0f);
        }

        group.renderers.Add(renderer);
    }

    private void HandlePlayerJumpPerformed()
    {
        if (!CanCollectFragments)
        {
            return;
        }

        isGroupAActive = !isGroupAActive;
        if (platformTransition != null)
        {
            StopCoroutine(platformTransition);
        }

        platformTransition = StartCoroutine(TransitionPlatforms(
            isGroupAActive ? platformGroupB : platformGroupA,
            isGroupAActive ? platformGroupA : platformGroupB));

        for (int i = 0; i < fragments.Count; i++)
        {
            fragments[i]?.AdvanceToNextPoint();
        }
    }

    private IEnumerator TransitionPlatforms(PlatformGroupRuntime outgoing, PlatformGroupRuntime incoming)
    {
        SetColliderState(incoming, true);
        float elapsed = 0f;
        float duration = Mathf.Max(platformTransitionDuration, 0.01f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetRendererAlpha(incoming, t);

            float outgoingAlpha = t <= platformFadeDelay / duration
                ? 1f
                : 1f - Mathf.InverseLerp(platformFadeDelay / duration, 1f, t);
            SetRendererAlpha(outgoing, outgoingAlpha);
            yield return null;
        }

        SetRendererAlpha(incoming, 1f);
        SetRendererAlpha(outgoing, 0f);
        SetColliderState(outgoing, false);
        platformTransition = null;
    }

    private IEnumerator MergeDragonGem()
    {
        CurrentState = Level03State.DragonGemMerging;
        ShowAnnouncement("Đang ghép Ngọc Rồng...", 1.2f);
        SetPlayerControls(false);

        if (bossPatrol != null)
        {
            bossPatrol.enabled = false;
        }

        yield return new WaitForSeconds(0.25f);

        for (int i = 0; i < fragments.Count; i++)
        {
            fragments[i]?.PrepareForMerge();
        }

        float mergeDuration = 0.75f;
        Vector3[] starts = new Vector3[fragments.Count];
        for (int i = 0; i < fragments.Count; i++)
        {
            starts[i] = fragments[i].transform.position;
        }

        float elapsed = 0f;
        while (elapsed < mergeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / mergeDuration));
            for (int i = 0; i < fragments.Count; i++)
            {
                Level03DragonFragment fragment = fragments[i];
                if (fragment == null)
                {
                    continue;
                }

                float orbit = (i == 0 ? -1f : 1f) * Mathf.Sin(t * Mathf.PI) * 0.65f;
                Vector3 position = Vector3.Lerp(starts[i], mergePoint, t);
                position.x += orbit;
                fragment.transform.position = position;
                fragment.transform.Rotate(0f, 0f, (i == 0 ? 360f : -360f) * Time.deltaTime);
            }

            yield return null;
        }

        for (int i = 0; i < fragments.Count; i++)
        {
            fragments[i]?.Hide();
        }

        if (completeGemRenderer != null)
        {
            completeGemRenderer.enabled = true;
        }

        mergeEffect?.PlayOnce();
        yield return new WaitForSeconds(0.75f);

        bossShield.BreakShield();
        if (bossPatrol != null)
        {
            bossPatrol.enabled = true;
        }

        CurrentState = Level03State.BossVulnerable;
        SetPlayerControls(true);
        ShowAnnouncement("Boss Shield Broken! Phase 2 bắt đầu!", 2f);

        yield return new WaitForSeconds(1.5f);
        if (completeGemRenderer != null)
        {
            completeGemRenderer.enabled = false;
        }
    }

    private void HandleBossDied(EnemyHealth defeatedBoss)
    {
        CurrentState = Level03State.BossDefeated;
        ShowAnnouncement("Boss Defeated!", 3f);
    }

    private void SetPlayerControls(bool enabledState)
    {
        if (playerInput == null)
        {
            return;
        }

        if (enabledState)
        {
            playerInput.EnableAll();
        }
        else
        {
            playerInput.EnableMovement(false);
            playerInput.EnableJump(false);
            playerInput.EnableAttack(false);
            playerInput.EnablePowerShot(false);
            playerMovement.Stop();
        }
    }

    private void ShowAnnouncement(string message, float duration)
    {
        announcement = message;
        announcementUntil = Time.time + duration;
    }

    private void OnGUI()
    {
        counterStyle ??= CreateGuiStyle(18, new Color(1f, 0.85f, 0.1f), FontStyle.Bold);
        announcementStyle ??= CreateGuiStyle(24, Color.white, FontStyle.Bold);

        GUI.Label(
            new Rect(Screen.width * 0.5f - 170f, 12f, 340f, 32f),
            $"Dragon Fragment: {collectedFragments}/{requiredFragments}",
            counterStyle);

        if (!string.IsNullOrEmpty(announcement) && Time.time <= announcementUntil)
        {
            GUI.Label(
                new Rect(Screen.width * 0.5f - 300f, 50f, 600f, 45f),
                announcement,
                announcementStyle);
        }
    }

    private static GUIStyle CreateGuiStyle(int fontSize, Color color, FontStyle fontStyle)
    {
        return new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = fontSize,
            fontStyle = fontStyle,
            normal = { textColor = color }
        };
    }

    private Material GetSpriteUnlitMaterial()
    {
        if (spriteUnlitMaterial != null)
        {
            return spriteUnlitMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader != null)
        {
            spriteUnlitMaterial = new Material(shader);
        }

        return spriteUnlitMaterial;
    }

    private static Transform CreateRoot(string rootName, Transform parent)
    {
        GameObject root = new GameObject(rootName);
        root.transform.SetParent(parent, false);
        return root.transform;
    }

    private static Sprite CreateFullTextureSprite(Texture2D texture, float pixelsPerUnit)
    {
        return texture == null
            ? null
            : Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit);
    }

    private static Sprite CreateTrimmedSprite(
        Texture2D texture,
        Color32[] pixels,
        int startX,
        int endX,
        float pixelsPerUnit)
    {
        int minX = endX;
        int maxX = startX - 1;
        int minY = texture.height;
        int maxY = -1;

        for (int y = 0; y < texture.height; y++)
        {
            int rowStart = y * texture.width;
            for (int x = startX; x < endX; x++)
            {
                if (pixels[rowStart + x].a <= 8)
                {
                    continue;
                }

                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return Sprite.Create(
                texture,
                new Rect(startX, 0f, endX - startX, texture.height),
                new Vector2(0.5f, 0f),
                pixelsPerUnit);
        }

        const int padding = 2;
        minX = Mathf.Max(startX, minX - padding);
        maxX = Mathf.Min(endX - 1, maxX + padding);
        minY = Mathf.Max(0, minY - padding);
        maxY = Mathf.Min(texture.height - 1, maxY + padding);

        return Sprite.Create(
            texture,
            new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1),
            new Vector2(0.5f, 0f),
            pixelsPerUnit);
    }

    private static void SetPlatformGroupState(PlatformGroupRuntime group, float alpha, bool collidersEnabled)
    {
        SetRendererAlpha(group, alpha);
        SetColliderState(group, collidersEnabled);
    }

    private static void SetRendererAlpha(PlatformGroupRuntime group, float alpha)
    {
        if (group == null)
        {
            return;
        }

        for (int i = 0; i < group.renderers.Count; i++)
        {
            SpriteRenderer renderer = group.renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Color color = renderer.color;
            color.a = Mathf.Clamp01(alpha);
            renderer.color = color;
        }
    }

    private static void SetColliderState(PlatformGroupRuntime group, bool enabledState)
    {
        if (group == null)
        {
            return;
        }

        for (int i = 0; i < group.colliders.Count; i++)
        {
            if (group.colliders[i] != null)
            {
                group.colliders[i].enabled = enabledState;
            }
        }
    }
}
