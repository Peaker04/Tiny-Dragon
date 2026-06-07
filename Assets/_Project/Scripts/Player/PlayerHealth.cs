using UnityEngine;
using UnityEngine.SceneManagement;
using TinyDragon.UI;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 5;
    [SerializeField] private string guideSceneName = "Level_01_guide";
    [SerializeField] private bool immortalInGuideScene = true;
    [SerializeField] private Vector3 damagePopupOffset = new Vector3(0f, 1.1f, 0f);
    [SerializeField] private Color damagePopupColor = new Color(1f, 0.15f, 0.05f);

    private int currentHealth;
    private bool isDead;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        // Khi Player duoc tao, mau hien tai bat dau bang mau toi da.
        currentHealth = maxHealth;
        isDead = false;
        PlayerStatusHud.EnsureFor(this);
    }

    private void OnEnable()
    {
        PlayerStatusHud.EnsureFor(this);
    }

    private void Start()
    {
        PlayerStatusHud.EnsureFor(this);
    }

    public void TakeDamage(int damage)
    {
        PlayerStatusHud.EnsureFor(this);

        // Bo qua damage khong hop le, hoac khi Player da chet.
        if (damage <= 0 || isDead)
        {
            return;
        }

        currentHealth -= damage;
        ShowDamagePopup(damage);
        Debug.Log($"Player took {damage} damage. HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            HandleNoHealth();
        }
    }

    private void HandleNoHealth()
    {
        if (immortalInGuideScene && SceneManager.GetActiveScene().name == guideSceneName)
        {
            Debug.Log("Player is out of HP but stays alive in guide level.");
            return;
        }

        Die();
    }

    private void Die()
    {
        isDead = true;
        Debug.Log("Player died.");

        if (!string.IsNullOrWhiteSpace(guideSceneName))
        {
            SceneManager.LoadScene(guideSceneName, LoadSceneMode.Single);
        }
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
