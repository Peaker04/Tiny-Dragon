using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinyDragon.UI
{
    public class GameOver : MonoBehaviour
    {
        private static GameOver instance;

        [SerializeField] private string menuSceneName = "Level_01_Origin";
        [SerializeField] private GameObject gameOverPanel;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(transform.root.gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(transform.root.gameObject);

            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
            }
        }

        public void GameOverActive()
        {
            if (gameOverPanel != null)
                gameOverPanel.SetActive(true);
            else
                gameObject.SetActive(true);

            Time.timeScale = 0f;
        }

        public void PlayAgain()
        {
            Time.timeScale = 1f;

            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
            else
                gameObject.SetActive(false);

            PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.Revive();
            }

            int currentScene = SceneManager.GetActiveScene().buildIndex;
            SceneManager.LoadScene(currentScene);
        }

        public void Menu()
        {
            Time.timeScale = 1f;

            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
            else
                gameObject.SetActive(false);

            PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.Revive();
            }

            SceneManager.LoadScene(menuSceneName);
        }
    }
}
