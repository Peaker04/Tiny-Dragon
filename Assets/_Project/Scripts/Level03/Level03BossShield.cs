using System.Collections;
using TinyDragon.Combat;
using UnityEngine;

[DisallowMultipleComponent]
public class Level03BossShield : MonoBehaviour, IEnemyDamageFilter
{
    [SerializeField] private float phaseTwoMovementMultiplier = 1.15f;
    [SerializeField] private float phaseTwoAttackIntervalMultiplier = 0.85f;
    [SerializeField] private Color shieldFlashColor = new Color(0.35f, 0.9f, 1f, 1f);
    [SerializeField] private Color phaseTwoTint = new Color(1f, 0.72f, 0.72f, 1f);
    [SerializeField] private float shieldPopupCooldown = 0.2f;

    private EnemyHealth health;
    private EnemyPatrol patrol;
    private SpriteRenderer rootRenderer;
    private Level03Manager manager;
    private Coroutine colorRoutine;
    private float nextShieldPopupTime;

    public bool IsShielded { get; private set; }

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        patrol = GetComponent<EnemyPatrol>();
        rootRenderer = GetComponent<SpriteRenderer>();
    }

    public void ActivateShield(Level03Manager encounterManager)
    {
        manager = encounterManager;
        IsShielded = true;
        SetRootColor(Color.white);
    }

    public int FilterDamage(int incomingDamage, PlayerDamageSource source)
    {
        if (!IsShielded)
        {
            return incomingDamage;
        }

        if (Time.time >= nextShieldPopupTime)
        {
            nextShieldPopupTime = Time.time + shieldPopupCooldown;
            health?.ShowStatusPopup("Shielded!", shieldFlashColor);
            manager?.NotifyShieldHit();
        }

        StartColorRoutine(FlashShield());
        return 0;
    }

    public void BreakShield()
    {
        if (!IsShielded)
        {
            return;
        }

        IsShielded = false;
        patrol?.ApplyPhaseMultipliers(phaseTwoMovementMultiplier, phaseTwoAttackIntervalMultiplier);
        StartColorRoutine(PlayPhaseTwoRage());
    }

    private void StartColorRoutine(IEnumerator routine)
    {
        if (colorRoutine != null)
        {
            StopCoroutine(colorRoutine);
        }

        colorRoutine = StartCoroutine(routine);
    }

    private IEnumerator FlashShield()
    {
        SetRootColor(shieldFlashColor);
        yield return new WaitForSeconds(0.1f);
        SetRootColor(Color.white);
        colorRoutine = null;
    }

    private IEnumerator PlayPhaseTwoRage()
    {
        for (int i = 0; i < 4; i++)
        {
            SetRootColor(i % 2 == 0 ? Color.white : phaseTwoTint);
            yield return new WaitForSeconds(0.12f);
        }

        SetRootColor(phaseTwoTint);
        colorRoutine = null;
    }

    private void SetRootColor(Color color)
    {
        if (rootRenderer == null)
        {
            rootRenderer = GetComponent<SpriteRenderer>();
        }

        if (rootRenderer != null)
        {
            rootRenderer.color = color;
        }
    }
}
