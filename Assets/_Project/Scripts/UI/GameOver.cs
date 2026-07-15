using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TinyDragon.Config;
using TinyDragon.Shared.Unity;

namespace TinyDragon.UI
{
    public class GameOver : MonoBehaviour
    {
        private static GameOver instance;

        [SerializeField] private TinyDragonRuntimeConfig runtimeConfig;
        [SerializeField] private string menuSceneName = "Level_01_Origin";
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button menuButton;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(transform.root.gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
            EnsureEventSystem();
            BindPanel();
            BindButtons();

            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            BindPanel();
            BindButtons();
        }

        private void BindPanel()
        {
            if (gameOverPanel != null)
            {
                return;
            }

            Transform panel = transform.Find("Panel");
            if (panel != null)
            {
                gameOverPanel = panel.gameObject;
            }
        }

        private void BindButtons()
        {
            if (playAgainButton == null || menuButton == null)
            {
                foreach (Button button in GetComponentsInChildren<Button>(true))
                {
                    if (button.name == "PlayAgain")
                    {
                        playAgainButton = button;
                    }
                    else if (button.name == "Menu")
                    {
                        menuButton = button;
                    }
                }
            }

            if (playAgainButton != null)
            {
                playAgainButton.onClick.RemoveListener(PlayAgain);
                playAgainButton.onClick.AddListener(PlayAgain);
            }

            if (menuButton != null)
            {
                menuButton.onClick.RemoveListener(Menu);
                menuButton.onClick.AddListener(Menu);
            }
        }

        public void GameOverActive()
        {
            EnsureEventSystem();
            BindPanel();
            BindButtons();

            if (gameOverPanel != null)
                gameOverPanel.SetActive(true);
            else
                gameObject.SetActive(true);

            Time.timeScale = 0f;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(eventSystemObject);
        }

        public void PlayAgain()
        {
            Time.timeScale = 1f;

            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
            else
                gameObject.SetActive(false);

            PlayerHealth playerHealth = ObjectLookup.Any<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.Revive();
            }

            SceneNavigator.ReloadActiveScene();
        }

        public void Menu()
        {
            Time.timeScale = 1f;

            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
            else
                gameObject.SetActive(false);

            PlayerHealth playerHealth = ObjectLookup.Any<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.Revive();
            }

            string configuredScene = TinyDragonRuntimeConfigProvider.Resolve(runtimeConfig).Scenes.gameOverMenuSceneName;
            SceneNavigator.LoadSceneIfSet(string.IsNullOrWhiteSpace(configuredScene) ? menuSceneName : configuredScene);
        }
    }
}
