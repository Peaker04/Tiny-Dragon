using UnityEngine;

/// <summary>
/// Handles melee hit detection for the player using Physics2D.OverlapCircleAll.
/// Extracted from PlayerAttack to separate combat detection from attack orchestration.
/// Configure hitbox radius, offset, and enemy layer mask in the Inspector.
/// </summary>
public class PlayerMeleeHitbox : MonoBehaviour
{
    [SerializeField] private float hitRadius = 0.8f;
    [SerializeField] private Vector2 hitOffset = new Vector2(0.8f, 0.2f);
    [SerializeField] private LayerMask enemyLayerMask = ~0;

    /// <summary>
    /// Queries Physics2D for enemies within the hitbox and applies damage.
    /// Uses the player's facing direction to determine hitbox position.
    /// </summary>
    public void DealDamage(int damage)
    {
        float facingDirection = transform.localScale.x >= 0f ? 1f : -1f;
        Vector2 scaledOffset = new Vector2(hitOffset.x * facingDirection, hitOffset.y);
        Vector2 hitPosition = (Vector2)transform.position + scaledOffset;

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(hitPosition, hitRadius, enemyLayerMask);
        foreach (Collider2D enemy in hitEnemies)
        {
            EnemyHealth enemyHealth = enemy.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        float facingDirection = transform.localScale.x >= 0f ? 1f : -1f;
        Vector2 scaledOffset = new Vector2(hitOffset.x * facingDirection, hitOffset.y);
        Vector2 hitPosition = (Vector2)transform.position + scaledOffset;

        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.5f);
        Gizmos.DrawWireSphere(hitPosition, hitRadius);
    }
}
