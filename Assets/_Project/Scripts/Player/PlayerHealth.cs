using System;
using TinyDragon.Data;
using TinyDragon.Config;
using TinyDragon.Shared.Unity;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private TinyDragonRuntimeConfig runtimeConfig;
    [SerializeField] private int maxHealth = GameplayBalanceDefaults.PlayerBaseHealth;
    [SerializeField] private FloatingDamageText damagePopupPrefab;
    [SerializeField] private int damagePopupPoolPrewarmCount = 4;
    [SerializeField] private Vector3 damagePopupOffset = new Vector3(0f, 1.1f, 0f);
    [SerializeField] private Color damagePopupColor = new Color(1f, 0.15f, 0.05f);

    // Guide-scene immortality is handled exclusively by PlayerDeathSceneHandler.

    // [Bug#2] i-frames (invincibility frames): khoảng thời gian player "miễn thương" sau khi bị đánh
    // - invincibilityDuration: thời gian i-frames tính bằng giây (mặc định 0.5s)
    // - immuneUntil: thời điểm (Time.time) mà i-frames kết thúc
    // Luồng: TakeDamage() -> kiểm tra Time.time < immuneUntil? -> nếu đang i-frames thì bỏ qua damage
    //     -> nếu không, nhận damage, set immuneUntil = Time.time + invincibilityDuration
    //     -> trong khoảng thời gian này mọi đòn tiếp theo đều bị bỏ qua
    [SerializeField] private float invincibilityDuration = 0.5f;
    private float immuneUntil;

    private int currentHealth;
    private int flatDamageReduction;
    private int damageReductionPercent;
    private bool isDead;
    private ComponentPool<FloatingDamageText> damagePopupPool;
    private TinyDragonRuntimeConfig Config => TinyDragonRuntimeConfigProvider.Resolve(runtimeConfig);

    public static event Action<PlayerHealth> PlayerAvailable;

    public event Action<PlayerHealth> HealthChanged;
    public event Action<PlayerHealth> Died;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        EnsureDeathSceneHandler();
        currentHealth = maxHealth;
        isDead = false;
        immuneUntil = 0f;
        EnsureDamagePopupPool();
    }

    private void EnsureDeathSceneHandler()
    {
        if (GetComponent<PlayerDeathSceneHandler>() == null)
        {
            gameObject.AddComponent<PlayerDeathSceneHandler>();
        }
    }

    private void OnEnable()
    {
        PlayerAvailable?.Invoke(this);
        HealthChanged?.Invoke(this);
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || isDead)
        {
            return;
        }

        // [Bug#2] Kiểm tra i-frames: nếu đang trong thời gian miễn thương (immuneUntil) thì bỏ qua damage
        // - immuneUntil được set sau mỗi lần nhận damage thành công = Time.time + invincibilityDuration
        // - Mục đích: tránh bị stunlock / chết quá nhanh khi trúng nhiều đòn liên tiếp
        // - Đây là cơ chế "grace period" tiêu chuẩn trong game action
        if (Time.time < immuneUntil)
        {
            Debug.Log($"Player ignored damage (i-frame active). Remaining: {immuneUntil - Time.time:F2}s");
            return;
        }

        // [Bug#2] Kích hoạt i-frames ngay: set thời điểm kết thúc miễn thương
        // - Bắt đầu từ ngay frame này, mọi đòn đánh đều bị bỏ qua cho đến khi Time.time >= immuneUntil
        immuneUntil = Time.time + invincibilityDuration;

        int effectiveDamage = CalculateIncomingDamage(damage);
        currentHealth -= effectiveDamage;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
        }

        TinyDragonSaveManager.Instance.SaveCurrentHealth(currentHealth, maxHealth);
        ShowDamagePopup(effectiveDamage);

        HealthChanged?.Invoke(this);
        Debug.Log($"Player took {effectiveDamage} damage. HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            isDead = true;
            Died?.Invoke(this);
        }
    }

    public void Revive()
    {
        currentHealth = maxHealth;
        isDead = false;
        TinyDragonSaveManager.Instance.SaveCurrentHealth(currentHealth, maxHealth);
        HealthChanged?.Invoke(this);
    }

    public void RestoreHealth(int savedCurrentHealth, int savedMaxHealth)
    {
        maxHealth = Mathf.Max(savedMaxHealth, 1);
        currentHealth = Mathf.Clamp(savedCurrentHealth, 0, maxHealth);
        isDead = currentHealth <= 0;
        HealthChanged?.Invoke(this);
    }

    public void ApplyRuntimeStats(int runtimeMaxHealth, int flatDefense, int reductionPercent)
    {
        maxHealth = Mathf.Max(runtimeMaxHealth, 1);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        flatDamageReduction = Mathf.Max(flatDefense, 0);
        damageReductionPercent = Mathf.Clamp(reductionPercent, 0, 95);
        isDead = currentHealth <= 0;
        HealthChanged?.Invoke(this);
    }

    private int CalculateIncomingDamage(int damage)
    {
        int percentReducedDamage = Mathf.RoundToInt(damage * (100 - damageReductionPercent) / 100f);
        return Mathf.Max(percentReducedDamage - flatDamageReduction, 1);
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

    private void EnsureDamagePopupPool()
    {
        if (damagePopupPool != null)
        {
            return;
        }

        if (damagePopupPrefab == null)
        {
            GameObject damagePopupPrefabObject = ResourceLoader.Load<GameObject>(Config.Resources.damagePopupPrefabPath);
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
