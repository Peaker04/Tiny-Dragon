using System;
using TinyDragon.Data;
using UnityEngine;

/// <summary>
/// Manages the player's mana/Ki resource: current value, max value,
/// regeneration, spending, and cross-scene persistence via static sync.
/// Extracted from PlayerAttack to follow single-responsibility principle.
/// </summary>
public class PlayerMana : MonoBehaviour
{
    [SerializeField] private float maxMana = GameplayBalanceDefaults.PlayerBaseKi;
    [SerializeField] private float manaRegenRate = 10f;

    private float currentMana;

    private static bool hasSyncedMana;
    private static float syncedMana;

    public float CurrentMana => currentMana;
    public float MaxMana => maxMana;

    /// <summary>Fired whenever current or max mana changes.</summary>
    public event Action<PlayerMana> ManaChanged;

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
        InitializeMana();
    }

    /// <summary>Call this from PlayerAttack.Update() to tick mana regen each frame.</summary>
    public void Regenerate(float deltaTime)
    {
        if (maxMana <= 0f)
        {
            SyncMana(0f);
            return;
        }

        SetCurrentMana(currentMana + manaRegenRate * deltaTime);
    }

    /// <summary>Returns true if the player has at least <paramref name="amount"/> mana.</summary>
    public bool CanSpend(float amount)
    {
        return maxMana > 0f && currentMana >= amount;
    }

    /// <summary>
    /// Attempts to spend <paramref name="amount"/> mana.
    /// Returns true if the spend succeeded.
    /// </summary>
    public bool TrySpend(float amount)
    {
        if (!CanSpend(amount))
        {
            return false;
        }

        SetCurrentMana(currentMana - amount);
        return true;
    }

    /// <summary>Returns true when the mana bar is completely full.</summary>
    public bool IsFull()
    {
        return maxMana > 0f && currentMana >= maxMana;
    }

    /// <summary>Returns the mana cost for a power shot based on the ratio of max mana.</summary>
    public float GetPowerShotManaCost(float manaCostRatio)
    {
        return maxMana * Mathf.Clamp01(manaCostRatio);
    }

    /// <summary>Restore mana from a saved state.</summary>
    public void RestoreMana(float savedCurrentMana, float savedMaxMana)
    {
        maxMana = Mathf.Max(savedMaxMana, 0f);
        SetCurrentMana(savedCurrentMana);
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
        ManaChanged?.Invoke(this);
    }

    private static void SyncMana(float value)
    {
        syncedMana = value;
        hasSyncedMana = true;
    }
}
