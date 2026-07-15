using TinyDragon.Combat;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RequiredPlayerDamageSourceFilter : MonoBehaviour, IEnemyDamageFilter
{
    [SerializeField] private PlayerDamageSource requiredSource = PlayerDamageSource.Generic;
    [SerializeField] private bool damageEnabled = true;
    [SerializeField] private Color blockedHitColor = new Color(0.55f, 0.8f, 1f, 1f);
    [SerializeField] private float blockedPopupCooldown = 0.25f;

    private EnemyHealth health;
    private float nextBlockedPopupTime;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
    }

    public int FilterDamage(int incomingDamage, PlayerDamageSource source)
    {
        if (!damageEnabled)
        {
            return 0;
        }

        if (requiredSource == PlayerDamageSource.Generic || source == requiredSource)
        {
            return incomingDamage;
        }

        if (Time.time >= nextBlockedPopupTime)
        {
            nextBlockedPopupTime = Time.time + blockedPopupCooldown;
            health?.ShowStatusPopup(RequiredSourceLabel(), blockedHitColor);
        }

        return 0;
    }

    public void SetDamageEnabled(bool isEnabled)
    {
        damageEnabled = isEnabled;
    }

    private string RequiredSourceLabel()
    {
        switch (requiredSource)
        {
            case PlayerDamageSource.NormalShot:
                return "Chuong!";
            case PlayerDamageSource.PowerShot:
                return "Ki!";
            case PlayerDamageSource.Punch:
                return "Dam!";
            case PlayerDamageSource.Kick:
                return "Da!";
            default:
                return "Hit!";
        }
    }
}
