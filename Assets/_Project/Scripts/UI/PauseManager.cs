using UnityEngine;
using TinyDragon.Config;
using TinyDragon.Shared.Unity;

namespace TinyDragon.UI
{
    public class PauseManager : MonoBehaviour
    {
        [Header("UI Canvases")]
        [SerializeField] private Canvas pauseCanvas;
        [SerializeField] private Canvas settingsCanvas; // Reference to settings canvas if opening from pause menu

        [Header("Navigation")]
        [SerializeField] private TinyDragonRuntimeConfig runtimeConfig;
        [SerializeField] private string mainMenuSceneName = "MainMenu"; // Name of your Main Menu scene

        private bool isPaused = false;

        public bool IsPaused => isPaused;

        private static PauseManager instance;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(transform.root.gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
        }

        private void Start()
        {
            if (pauseCanvas == null) pauseCanvas = GetComponent<Canvas>();
            if (settingsCanvas == null)
            {
                SettingsManager sm = ObjectLookup.Any<SettingsManager>();
                if (sm != null) settingsCanvas = sm.GetComponent<Canvas>();
            }

            // Ensure UI is in correct state on game start
            if (pauseCanvas != null) pauseCanvas.enabled = false;
            if (settingsCanvas != null) settingsCanvas.enabled = false;
            Time.timeScale = 1f;
        }


        private void Update()
        {
            PlayerInputReader inputReader = ObjectLookup.Any<PlayerInputReader>();
            
            if (inputReader != null && inputReader.ConsumePausePressed())
            {
                if (settingsCanvas != null && settingsCanvas.enabled)
                {
                    PauseGame();
                }
                else
                {
                    TogglePause();
                }
            }
        }

        public void TogglePause()
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        public void PauseGame()
        {
            isPaused = true;
            Time.timeScale = 0f; // Freeze game physics and animations
            
            if (settingsCanvas != null)
            {
                settingsCanvas.enabled = false; // Close settings canvas automatically
            }

            if (pauseCanvas != null)
            {
                pauseCanvas.enabled = true;
            }
        }

        public void ResumeGame()
        {
            isPaused = false;
            Time.timeScale = 1f; // Unfreeze game
            
            if (pauseCanvas != null)
            {
                pauseCanvas.enabled = false;
            }
            
            if (settingsCanvas != null)
            {
                settingsCanvas.enabled = false;
            }
        }

        public void LoadMainMenu()
        {
            // Reset Time.timeScale to normal before loading a new scene
            Time.timeScale = 1f;
            
            string configuredScene = TinyDragonRuntimeConfigProvider.Resolve(runtimeConfig).Scenes.mainMenuSceneName;
            SceneNavigator.LoadSceneIfSet(string.IsNullOrWhiteSpace(configuredScene) ? mainMenuSceneName : configuredScene);
        }
    }
}
