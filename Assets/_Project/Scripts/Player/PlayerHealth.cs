using System;
using TinyDragon.Config;
using TinyDragon.Data;
using TinyDragon.Audio;
using TinyDragon.Shared.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private TinyDragonRuntimeConfig runtimeConfig;
    [SerializeField] private int maxHealth = GameplayBalanceDefaults.PlayerBaseHealth;
    [SerializeField] private FloatingDamageText damagePopupPrefab;
    [SerializeField] private int damagePopupPoolPrewarmCount = 4;
    [SerializeField] private Vector3 damagePopupOffset = new Vector3(0f, 1.1f, 0f);
    [SerializeField] private Color damagePopupColor = new Color(1f, 0.15f, 0.05f);
    [SerializeField] private float invincibilityDuration = 0.5f;

    [Header("Guide Scene Logic")]
    [SerializeField] private string guideSceneName = "LangAru";
    [SerializeField] private bool immortalInGuideScene = true;

    private int currentHealth;
    private int flatDamageReduction;
    private int damageReductionPercent;
    private bool isDead;
    private float immuneUntil;
    private ComponentPool<FloatingDamageText> damagePopupPool;
    private TinyDragonRuntimeConfig Config => TinyDragonRuntimeConfigProvider.Resolve(runtimeConfig);

    public static event Action<PlayerHealth> PlayerAvailable;

    public event Action<int, bool> Damaged;
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
        EnsurePlayerAudioComponents();
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

        if (Time.time < immuneUntil)
        {
            return;
        }

        immuneUntil = Time.time + invincibilityDuration;
        int effectiveDamage = CalculateIncomingDamage(damage);
        currentHealth -= effectiveDamage;
        if (currentHealth <= 0)
        {
            if (immortalInGuideScene && SceneManager.GetActiveScene().name == guideSceneName)
            {
                currentHealth = 1;
            }
            else
            {
                currentHealth = 0;
            }
        }

        TinyDragonSaveManager.Instance.SaveCurrentHealth(currentHealth, maxHealth);
        ShowDamagePopup(effectiveDamage);
        Damaged?.Invoke(effectiveDamage, currentHealth <= 0);
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

    private void EnsureDeathSceneHandler()
    {
        if (GetComponent<PlayerDeathSceneHandler>() == null)
        {
            gameObject.AddComponent<PlayerDeathSceneHandler>();
        }
    }

    private void EnsurePlayerAudioComponents()
    {
        if (GetComponent<PlayerActionAudio>() == null)
        {
            gameObject.AddComponent<PlayerActionAudio>();
        }

        if (GetComponent<CharacterFeedbackAudio>() == null)
        {
            gameObject.AddComponent<CharacterFeedbackAudio>();
        }

        if (GetComponent<PlayerAuraAudio>() == null)
        {
            gameObject.AddComponent<PlayerAuraAudio>();
        }
    }
}
