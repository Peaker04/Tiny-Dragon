using TinyDragon.Data;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    private const string DefaultDamagePopupPrefabPath = "Combat/DamagePopup";

    private static Sprite healthBarSprite;
    private static Material healthBarMaterial;

    [SerializeField] private string displayName = "Khủng long";
    [SerializeField] private string balanceEnemyId;
    [SerializeField] private int maxHealth = GameplayBalanceDefaults.NormalEnemyHealth;
    [SerializeField] private FloatingDamageText damagePopupPrefab;
    [SerializeField] private int damagePopupPoolPrewarmCount = 4;
    [SerializeField] private Vector3 damagePopupOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private Color damagePopupColor = new Color(1f, 0.85f, 0.05f);
    [SerializeField] private bool showHealthBar = true;
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 0.95f, 0f);
    [SerializeField] private float healthBarWidth = 0.8f;
    [SerializeField] private float healthBarHeight = 0.08f;
    [SerializeField] private Color healthBarBackColor = new Color(0.12f, 0.02f, 0.02f, 0.9f);
    [SerializeField] private Color healthBarFillColor = new Color(1f, 0.05f, 0.02f, 1f);
    [SerializeField] private int healthBarSortingOrder = 10;

    private int currentHealth;
    private Transform healthBarRoot;
    private SpriteRenderer healthBarBack;
    private SpriteRenderer healthBarFill;
    private ComponentPool<FloatingDamageText> damagePopupPool;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

    /// <summary>Fired when this enemy's HP reaches zero, just before the GameObject is destroyed.</summary>
    public event System.Action<EnemyHealth> Died;

    private void Awake()
    {
        ApplyDatabaseBalanceIfAvailable();
        EnsureDamagePopupPool();
        ResetHealth();
    }

    private void OnEnable()
    {
        ResetHealth();
        SetHealthBarActive(showHealthBar);
    }

    private void LateUpdate()
    {
        if (healthBarRoot == null || !showHealthBar)
        {
            return;
        }

        healthBarRoot.position = transform.position + healthBarOffset;
        UpdateHealthBar();
    }

    private void OnDisable()
    {
        SetHealthBarActive(false);
    }

    private void OnDestroy()
    {
        if (healthBarRoot != null)
        {
            Destroy(healthBarRoot.gameObject);
        }
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        UpdateHealthBar();
    }

    public void ApplyBalance(string newDisplayName, int newMaxHealth, bool refillCurrentHealth = true)
    {
        if (!string.IsNullOrWhiteSpace(newDisplayName))
        {
            displayName = newDisplayName;
        }

        maxHealth = Mathf.Max(newMaxHealth, 1);
        currentHealth = refillCurrentHealth
            ? maxHealth
            : Mathf.Clamp(currentHealth, 0, maxHealth);
        UpdateHealthBar();
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || currentHealth <= 0)
        {
            return;
        }

        int actualDamage = damage;
        IEnemyDamageFilter filter = GetComponent<IEnemyDamageFilter>();
        if (filter != null)
        {
            actualDamage = filter.FilterDamage(damage);
        }

        if (actualDamage <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(currentHealth - actualDamage, 0);
        UpdateHealthBar();
        ShowDamagePopup(actualDamage);
        Debug.Log($"Enemy took {actualDamage} damage. HP: {currentHealth}/{maxHealth}", this);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("Enemy died.", this);
        Died?.Invoke(this);
        Destroy(gameObject);
    }

    /// <summary>Shows a short text popup over this enemy (e.g. "Shielded!") using the damage popup pool.</summary>
    public void ShowStatusPopup(string message, Color color)
    {
        EnsureDamagePopupPool();
        if (damagePopupPool == null)
        {
            return;
        }

        FloatingDamageText floatingText = damagePopupPool.Get(transform.position + damagePopupOffset, Quaternion.identity);
        floatingText.Initialize(message, color, damagePopupPool.Release);
    }

    private void ApplyDatabaseBalanceIfAvailable()
    {
        string enemyId = ResolveBalanceEnemyId();
        if (!TinyDragonSaveManager.Instance.TryLoadEnemyBalance(enemyId, out EnemyBalanceData balance))
        {
            return;
        }

        ApplyBalance(balance.DisplayName, balance.BaseHP);

        EnemyPatrol patrol = GetComponent<EnemyPatrol>();
        if (patrol != null)
        {
            patrol.ApplyCombatStats(
                balance.BaseSpd,
                balance.BaseAtk,
                balance.BaseAtk,
                GameplayBalanceDefaults.NormalEnemyAttackCooldown,
                GameplayBalanceDefaults.NormalEnemyRangeAttackCooldown
            );
        }

        BossAI boss = GetComponent<BossAI>();
        if (boss != null)
        {
            boss.ApplyCombatStats(
                balance.BaseSpd,
                balance.BaseAtk,
                GameplayBalanceDefaults.BossEnergyDamage
            );
        }
    }

    private string ResolveBalanceEnemyId()
    {
        if (!string.IsNullOrWhiteSpace(balanceEnemyId))
        {
            return balanceEnemyId;
        }

        return GetComponent<BossAI>() != null ? "enemy_boss_act_1" : "enemy_monster_1";
    }

    private void ShowDamagePopup(int damage)
    {
        EnsureDamagePopupPool();
        if (damagePopupPool == null)
        {
            return;
        }

        FloatingDamageText floatingText = damagePopupPool.Get(transform.position + damagePopupOffset, Quaternion.identity);
        floatingText.Initialize($"-{damage}", damagePopupColor, damagePopupPool.Release);
    }

    private void UpdateHealthBar()
    {
        if (!showHealthBar)
        {
            return;
        }

        EnsureHealthBar();

        float healthPercent = maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
        healthBarFill.transform.localScale = new Vector3(healthBarWidth * healthPercent, healthBarHeight, 1f);
        healthBarFill.transform.localPosition = new Vector3(
            -healthBarWidth * 0.5f + healthBarWidth * healthPercent * 0.5f,
            0f,
            0f
        );
    }

    private void EnsureHealthBar()
    {
        if (healthBarRoot != null)
        {
            return;
        }

        healthBarRoot = new GameObject($"{name} Health Bar").transform;
        healthBarRoot.SetParent(RuntimeSceneRoot.GetChild("HealthBars"), false);
        healthBarRoot.position = transform.position + healthBarOffset;

        healthBarBack = CreateHealthBarPart("Back", healthBarRoot, healthBarBackColor, healthBarSortingOrder);
        healthBarBack.transform.localScale = new Vector3(healthBarWidth, healthBarHeight, 1f);

        healthBarFill = CreateHealthBarPart("Fill", healthBarRoot, healthBarFillColor, healthBarSortingOrder + 1);
        SetHealthBarActive(isActiveAndEnabled);
    }

    private SpriteRenderer CreateHealthBarPart(string partName, Transform parent, Color color, int sortingOrder)
    {
        GameObject partObject = new GameObject(partName);
        partObject.transform.SetParent(parent, false);

        SpriteRenderer renderer = partObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GetHealthBarSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        renderer.sharedMaterial = GetHealthBarMaterial();
        return renderer;
    }

    private void SetHealthBarActive(bool isActive)
    {
        if (healthBarRoot != null)
        {
            healthBarRoot.gameObject.SetActive(isActive);
        }
    }

    private static Sprite GetHealthBarSprite()
    {
        if (healthBarSprite != null)
        {
            return healthBarSprite;
        }

        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        healthBarSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return healthBarSprite;
    }

    private static Material GetHealthBarMaterial()
    {
        if (healthBarMaterial != null)
        {
            return healthBarMaterial;
        }

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader == null)
        {
            return null;
        }

        healthBarMaterial = new Material(spriteShader);
        return healthBarMaterial;
    }

    private void EnsureDamagePopupPool()
    {
        if (damagePopupPool != null)
        {
            return;
        }

        if (damagePopupPrefab == null)
        {
            GameObject damagePopupPrefabObject = Resources.Load<GameObject>(DefaultDamagePopupPrefabPath);
            if (damagePopupPrefabObject != null)
            {
                damagePopupPrefab = damagePopupPrefabObject.GetComponent<FloatingDamageText>();
            }
        }

        if (damagePopupPrefab != null)
        {
            damagePopupPool = new ComponentPool<FloatingDamageText>(
                damagePopupPrefab,
                RuntimeSceneRoot.GetChild("DamagePopupPool"),
                damagePopupPoolPrewarmCount
            );
        }
    }
}
