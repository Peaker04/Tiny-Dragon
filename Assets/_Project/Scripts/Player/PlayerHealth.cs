using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 5;
    [SerializeField] private Vector3 damagePopupOffset = new Vector3(0f, 1.1f, 0f);
    [SerializeField] private Color damagePopupColor = new Color(1f, 0.15f, 0.05f);

    private int currentHealth;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        // Khi Player duoc tao, mau hien tai bat dau bang mau toi da.
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        // Bo qua damage khong hop le, hoac khi Player da chet.
        if (damage <= 0 || currentHealth <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(currentHealth - damage, 0);
        ShowDamagePopup(damage);
        Debug.Log($"Player took {damage} damage. HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        // Cho nay hien chi log, sau nay co the them restart, game over, hoac animation chet.
        Debug.Log("Player died.");
    }

    private void ShowDamagePopup(int damage)
    {
        // Tao text damage tai vi tri Player cong offset de hien len tren dau.
        GameObject popupObject = new GameObject("Damage Popup");
        popupObject.transform.position = transform.position + damagePopupOffset;

        FloatingDamageText floatingText = popupObject.AddComponent<FloatingDamageText>();
        floatingText.Initialize($"-{damage}", damagePopupColor);
    }
}
