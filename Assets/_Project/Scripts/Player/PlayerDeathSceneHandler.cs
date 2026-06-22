using UnityEngine;
using UnityEngine.SceneManagement;
using TinyDragon.UI;

[RequireComponent(typeof(PlayerHealth))]
public class PlayerDeathSceneHandler : MonoBehaviour
{
    [SerializeField] private string guideSceneName = "LangAru";
    [SerializeField] private bool immortalInGuideScene = true;
    [SerializeField] private bool showGameOverOutsideGuide = true;
    [SerializeField] private GameOver gameOverUI;

    private PlayerHealth playerHealth;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        if (playerHealth != null)
        {
            playerHealth.Died += HandlePlayerDied;
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.Died -= HandlePlayerDied;
        }
    }

    private void HandlePlayerDied(PlayerHealth health)
    {
        if (immortalInGuideScene && SceneManager.GetActiveScene().name == guideSceneName)
        {
            Debug.Log("Player is out of HP but stays alive in guide level.");
            health.Revive();
            return;
        }

        Debug.Log("Player died.");

        health.Revive();
        if (showGameOverOutsideGuide)
        {
            GameOver resolvedGameOver = gameOverUI != null ? gameOverUI : FindAnyObjectByType<GameOver>(FindObjectsInactive.Include);
            if (resolvedGameOver != null)
            {
                resolvedGameOver.GameOverActive();
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(guideSceneName))
        {
            SceneManager.LoadScene(guideSceneName, LoadSceneMode.Single);
        }
    }
}
