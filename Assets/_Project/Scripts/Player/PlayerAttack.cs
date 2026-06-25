using TinyDragon.Data;
using UnityEngine;

/// <summary>
/// Thin orchestrator that routes player input to the correct attack sub-system.
/// Mana management → PlayerMana
/// Melee hit detection → PlayerMeleeHitbox
/// Punch/Kick combos → PlayerComboAttack
/// Projectile shooting → ProjectileShooter
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private PlayerAnimatorDriver animatorDriver;
    [SerializeField] private ProjectileShooter projectileShooter;
    [SerializeField] private PlayerMana mana;
    [SerializeField] private PlayerComboAttack comboAttack;
    [SerializeField] private float attackCooldown = 0.2f;

    [Header("Power Shot")]
    [SerializeField] private float powerShotCooldown = GameplayBalanceDefaults.PowerShotCooldown;
    [SerializeField] private float powerShotManaCostRatio = GameplayBalanceDefaults.PowerShotManaCostRatio;

    [Header("Aura")]
    [SerializeField] private GameObject auraEffect;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private AudioClip powerShotSound;

    private float nextAttackTime;
    private float nextPowerShotTime;

    // --- Public API (backward-compatible) ---

    public float CurrentMana => mana != null ? mana.CurrentMana : 0f;
    public float MaxMana => mana != null ? mana.MaxMana : 0f;

    public static void ResetManaForNewRun()
    {
        PlayerMana.ResetManaForNewRun();
    }

    private void Awake()
    {
        if (inputReader == null) inputReader = GetComponent<PlayerInputReader>();
        if (animatorDriver == null) animatorDriver = GetComponent<PlayerAnimatorDriver>();
        if (projectileShooter == null) projectileShooter = GetComponent<ProjectileShooter>();
        if (mana == null) mana = GetComponent<PlayerMana>();
        if (comboAttack == null) comboAttack = GetComponent<PlayerComboAttack>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        CacheAuraEffect();
    }

    private void OnEnable()
    {
        if (mana != null)
        {
            mana.ManaChanged += HandleManaChanged;
        }
    }

    private void OnDisable()
    {
        if (mana != null)
        {
            mana.ManaChanged -= HandleManaChanged;
        }
    }

    private void Update()
    {
        if (mana != null)
        {
            mana.Regenerate(Time.deltaTime);
        }

        if (inputReader == null)
        {
            return;
        }

        TryExecuteAttackCommand();
    }

    // --- Attack routing ---

    private void TryExecuteAttackCommand()
    {
        if (TryHandlePowerShot()) return;
        if (comboAttack != null && comboAttack.TryExecutePunch(nextAttackTime))
        {
            nextAttackTime = Time.time + attackCooldown;
            return;
        }
        if (comboAttack != null && comboAttack.TryExecuteKick(nextAttackTime))
        {
            nextAttackTime = Time.time + attackCooldown;
            return;
        }
        HandleNormalAttack();
    }

    private bool TryHandlePowerShot()
    {
        if (!inputReader.ConsumePowerShotPressed())
        {
            return false;
        }

        if (mana == null)
        {
            return false;
        }

        float manaCost = mana.GetPowerShotManaCost(powerShotManaCostRatio);
        if (!mana.CanSpend(manaCost) || Time.time < nextPowerShotTime)
        {
            return false;
        }

        bool usedAttackFallback = animatorDriver != null && animatorDriver.TriggerPowerAttackOrFallback();
        if (usedAttackFallback)
        {
            projectileShooter?.SuppressNextShot(attackCooldown + 0.25f);
        }

        projectileShooter?.ShootPower();
        PlaySound(powerShotSound);
        mana.TrySpend(manaCost);
        nextPowerShotTime = Time.time + powerShotCooldown;
        return true;
    }

    private void HandleNormalAttack()
    {
        if (!inputReader.ConsumeAttackPressed())
        {
            return;
        }

        if (Time.time < nextAttackTime)
        {
            return;
        }

        animatorDriver?.TriggerAttack();
        PlaySound(attackSound);
        nextAttackTime = Time.time + attackCooldown;
    }

    // --- Public tuning API (used by save/load) ---

    public void ApplyAttackCooldown(float cooldown)
    {
        attackCooldown = Mathf.Max(cooldown, 0.01f);
    }

    public void RestoreMana(float savedCurrentMana, float savedMaxMana)
    {
        if (mana != null)
        {
            mana.RestoreMana(savedCurrentMana, savedMaxMana);
        }
    }

    public void ApplyPowerShotTuning(float cooldown, float manaCostRatio)
    {
        powerShotCooldown = Mathf.Max(cooldown, 0.01f);
        powerShotManaCostRatio = Mathf.Clamp01(manaCostRatio);
    }

    // --- Aura visual ---

    private void HandleManaChanged(PlayerMana playerMana)
    {
        SetAuraActive(playerMana.IsFull());
    }

    private void CacheAuraEffect()
    {
        if (auraEffect != null)
        {
            return;
        }

        Transform aura = transform.Find("Aura");
        if (aura != null)
        {
            auraEffect = aura.gameObject;
        }
    }

    private void SetAuraActive(bool active)
    {
        if (auraEffect == null)
        {
            CacheAuraEffect();
        }

        if (auraEffect != null && auraEffect.activeSelf != active)
        {
            auraEffect.SetActive(active);
        }
    }

    // --- Audio ---

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, TinyDragon.UI.SettingsManager.GlobalSFXVolume);
        }
    }
}
