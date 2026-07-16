using System;
using TinyDragon.Data;
using UnityEngine;

/// <summary>
/// Routes player attack input to mana, combo, melee, and projectile subsystems
/// while preserving the public events used by the audio/HUD systems.
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

    private float nextAttackTime;
    private float nextPowerShotTime;
    private bool isAuraActive;

    public event Action<int> PunchExecuted;
    public event Action<int> KickExecuted;
    public event Action AttackExecuted;
    public event Action PowerShotExecuted;
    public event Action<bool> AuraStateChanged;

    public float CurrentMana => mana != null ? mana.CurrentMana : 0f;
    public float MaxMana => mana != null ? mana.MaxMana : 0f;
    public bool IsAuraActive => isAuraActive;

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
        if (mana == null) mana = gameObject.AddComponent<PlayerMana>();
        if (comboAttack == null) comboAttack = GetComponent<PlayerComboAttack>();
        if (comboAttack == null) comboAttack = gameObject.AddComponent<PlayerComboAttack>();

        CacheAuraEffect();
    }

    private void OnEnable()
    {
        if (mana != null)
        {
            mana.ManaChanged += HandleManaChanged;
            HandleManaChanged(mana);
        }

        if (comboAttack != null)
        {
            comboAttack.PunchExecuted += HandlePunchExecuted;
            comboAttack.KickExecuted += HandleKickExecuted;
        }
    }

    private void OnDisable()
    {
        if (mana != null)
        {
            mana.ManaChanged -= HandleManaChanged;
        }

        if (comboAttack != null)
        {
            comboAttack.PunchExecuted -= HandlePunchExecuted;
            comboAttack.KickExecuted -= HandleKickExecuted;
        }
    }

    private void Update()
    {
        mana?.Regenerate(Time.deltaTime);

        if (inputReader == null)
        {
            return;
        }

        TryExecuteAttackCommand();
    }

    private void TryExecuteAttackCommand()
    {
        if (TryHandlePowerShot()) return;
        if (comboAttack != null && comboAttack.TryExecutePunch()) return;
        if (comboAttack != null && comboAttack.TryExecuteKick()) return;
        HandleNormalAttack();
    }

    private bool TryHandlePowerShot()
    {
        if (!inputReader.ConsumePowerShotPressed() || mana == null)
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
        if (!mana.TrySpend(manaCost))
        {
            return false;
        }

        PowerShotExecuted?.Invoke();
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
        bool projectileSpawned = projectileShooter != null && projectileShooter.Shoot();
        if (projectileSpawned)
        {
            // Legacy attack clips also invoke Shoot/ShootProjectile via AnimationEvent.
            projectileShooter.SuppressNextShot(attackCooldown + 0.25f);
        }

        AttackExecuted?.Invoke();
        nextAttackTime = Time.time + attackCooldown;
    }

    public void ApplyAttackCooldown(float cooldown)
    {
        attackCooldown = Mathf.Max(cooldown, 0.01f);
    }

    public void RestoreMana(float savedCurrentMana, float savedMaxMana)
    {
        if (mana == null)
        {
            mana = GetComponent<PlayerMana>();
        }

        mana?.RestoreMana(savedCurrentMana, savedMaxMana);
    }

    public void ApplyPowerShotTuning(float cooldown, float manaCostRatio)
    {
        powerShotCooldown = Mathf.Max(cooldown, 0.01f);
        powerShotManaCostRatio = Mathf.Clamp01(manaCostRatio);
    }

    private void HandlePunchExecuted(int comboStep)
    {
        PunchExecuted?.Invoke(comboStep);
    }

    private void HandleKickExecuted(int comboStep)
    {
        KickExecuted?.Invoke(comboStep);
    }

    private void HandleManaChanged(PlayerMana playerMana)
    {
        SetAuraActive(playerMana != null && playerMana.IsFull());
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

        if (isAuraActive == active)
        {
            return;
        }

        isAuraActive = active;
        if (auraEffect != null && auraEffect.activeSelf != active)
        {
            auraEffect.SetActive(active);
        }

        AuraStateChanged?.Invoke(active);
    }
}
