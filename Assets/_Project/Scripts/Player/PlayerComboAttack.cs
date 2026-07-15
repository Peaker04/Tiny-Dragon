using TinyDragon.Data;
using TinyDragon.Combat;
using System;
using UnityEngine;

/// <summary>
/// Manages punch and kick combo attack sequences for the player.
/// Extracted from PlayerAttack so the orchestrator only delegates
/// input to the appropriate attack sub-system.
/// </summary>
public class PlayerComboAttack : MonoBehaviour
{
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private PlayerAnimatorDriver animatorDriver;
    [SerializeField] private PlayerMeleeHitbox meleeHitbox;
    [SerializeField] private float comboWindow = 0.5f;
    [SerializeField] private float comboInputLockout = 0.05f;
    [SerializeField] private int baseDamage = GameplayBalanceDefaults.PlayerBaseAttack;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip attackSound;

    private int punchComboStep;
    private float lastPunchTime;
    private int kickComboStep;
    private float lastKickTime;

    public event Action<int> PunchExecuted;
    public event Action<int> KickExecuted;

    private void Awake()
    {
        if (inputReader == null) inputReader = GetComponent<PlayerInputReader>();
        if (animatorDriver == null) animatorDriver = GetComponent<PlayerAnimatorDriver>();
        if (meleeHitbox == null) meleeHitbox = GetComponent<PlayerMeleeHitbox>();
        if (meleeHitbox == null) meleeHitbox = gameObject.AddComponent<PlayerMeleeHitbox>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    /// <summary>Tries to execute a punch. Returns true if input was consumed.</summary>
    public bool TryExecutePunch()
    {
        if (inputReader == null || !inputReader.ConsumePunchPressed()) return false;
        if (Time.time - lastPunchTime < comboInputLockout) return false;

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
        meleeHitbox?.DealDamage(baseDamage, PlayerDamageSource.Punch);
        PunchExecuted?.Invoke(punchComboStep);
        lastPunchTime = Time.time;
        return true;
    }

    /// <summary>Tries to execute a kick. Returns true if input was consumed.</summary>
    public bool TryExecuteKick()
    {
        if (inputReader == null || !inputReader.ConsumeKickPressed()) return false;
        if (Time.time - lastKickTime < comboInputLockout) return false;

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
        meleeHitbox?.DealDamage(baseDamage, PlayerDamageSource.Kick);
        KickExecuted?.Invoke(kickComboStep);
        lastKickTime = Time.time;
        return true;
    }

    public void ApplyBaseDamage(int damage)
    {
        baseDamage = Mathf.Max(damage, 1);
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, TinyDragon.UI.SettingsManager.GlobalSFXVolume);
        }
    }
}
