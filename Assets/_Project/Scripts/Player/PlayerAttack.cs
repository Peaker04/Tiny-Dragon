using TinyDragon.Data;
using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private PlayerAnimatorDriver animatorDriver;
    [SerializeField] private ProjectileShooter projectileShooter;
    [SerializeField] private float attackCooldown = 0.2f;
    [Header("Power Shot")]
    [SerializeField] private float powerShotCooldown = GameplayBalanceDefaults.PowerShotCooldown;
    [SerializeField] private float powerShotManaCostRatio = GameplayBalanceDefaults.PowerShotManaCostRatio;
    [SerializeField] private float maxMana = GameplayBalanceDefaults.PlayerBaseKi;
    [SerializeField] private float manaRegenRate = 10f;
    [SerializeField] private GameObject auraEffect;
    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private AudioClip powerShotSound;

    private float nextAttackTime;
    private float nextPowerShotTime;
    private float currentMana;
    
    private int punchComboStep = 0;
    private float lastPunchTime = 0f;
    private int kickComboStep = 0;
    private float lastKickTime = 0f;
    [SerializeField] private float comboWindow = 0.5f;

    private static bool hasSyncedMana;
    private static float syncedMana;

    public float CurrentMana => currentMana;
    public float MaxMana => maxMana;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSyncedManaState()
    {
        hasSyncedMana = false;
        syncedMana = 0f;
    }

    public static void ResetManaForNewRun()
    {
        hasSyncedMana = false;
        syncedMana = 0f;
    }

    private void Awake()
    {
        if (inputReader == null)
        {
            inputReader = GetComponent<PlayerInputReader>();
        }

        if (animatorDriver == null)
        {
            animatorDriver = GetComponent<PlayerAnimatorDriver>();
        }

        if (projectileShooter == null)
        {
            projectileShooter = GetComponent<ProjectileShooter>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        CacheAuraEffect();
        InitializeMana();
    }

    private void Update()
    {
        RegenerateMana();

        if (inputReader == null)
        {
            return;
        }

        if (!TryHandlePowerShot())
        {
            if (!TryHandlePunch() && !TryHandleKick())
            {
                HandleNormalAttack();
            }
        }
    }

    private bool TryHandlePunch()
    {
        if (!inputReader.ConsumePunchPressed()) return false;
        if (Time.time < nextAttackTime) return false;

        if (Time.time - lastPunchTime > comboWindow)
        {
            punchComboStep = 1;
        }
        else
        {
            punchComboStep = punchComboStep == 1 ? 2 : 1;
        }

        animatorDriver?.TriggerPunch(punchComboStep);
        PlaySound(attackSound);
        nextAttackTime = Time.time + attackCooldown;
        lastPunchTime = Time.time;
        return true;
    }

    private bool TryHandleKick()
    {
        if (!inputReader.ConsumeKickPressed()) return false;
        if (Time.time < nextAttackTime) return false;

        if (Time.time - lastKickTime > comboWindow)
        {
            kickComboStep = 1;
        }
        else
        {
            kickComboStep = kickComboStep == 1 ? 2 : 1;
        }

        animatorDriver?.TriggerKick(kickComboStep);
        PlaySound(attackSound);
        nextAttackTime = Time.time + attackCooldown;
        lastKickTime = Time.time;
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

    public void ApplyAttackCooldown(float cooldown)
    {
        attackCooldown = Mathf.Max(cooldown, 0.01f);
    }

    public void RestoreMana(float savedCurrentMana, float savedMaxMana)
    {
        maxMana = Mathf.Max(savedMaxMana, 0f);
        SetCurrentMana(savedCurrentMana);
    }

    public void ApplyPowerShotTuning(float cooldown, float manaCostRatio)
    {
        powerShotCooldown = Mathf.Max(cooldown, 0.01f);
        powerShotManaCostRatio = Mathf.Clamp01(manaCostRatio);
    }

    private bool TryHandlePowerShot()
    {
        if (!inputReader.ConsumePowerShotPressed())
        {
            return false;
        }

        float manaCost = GetPowerShotManaCost();
        if (maxMana <= 0f || currentMana < manaCost || Time.time < nextPowerShotTime)
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
        SetCurrentMana(currentMana - manaCost);
        nextPowerShotTime = Time.time + powerShotCooldown;
        return true;
    }

    private void RegenerateMana()
    {
        if (maxMana <= 0f)
        {
            SyncMana(0f);
            SetAuraActive(false);
            return;
        }

        SetCurrentMana(currentMana + manaRegenRate * Time.deltaTime);
    }

    private void InitializeMana()
    {
        if (maxMana <= 0f)
        {
            SetCurrentMana(0f);
            return;
        }

        SetCurrentMana(hasSyncedMana ? syncedMana : maxMana);
    }

    private void SetCurrentMana(float value)
    {
        currentMana = Mathf.Clamp(value, 0f, maxMana);
        SyncMana(currentMana);
        SetAuraActive(currentMana >= maxMana);
    }

    private float GetPowerShotManaCost()
    {
        return maxMana * Mathf.Clamp01(powerShotManaCostRatio);
    }

    private static void SyncMana(float value)
    {
        syncedMana = value;
        hasSyncedMana = true;
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

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
