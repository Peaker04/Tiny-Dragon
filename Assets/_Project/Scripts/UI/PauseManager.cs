using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinyDragon.UI
{
    public class PauseManager : MonoBehaviour
    {
        [Header("UI Canvases")]
        [SerializeField] private Canvas pauseCanvas;
        [SerializeField] private Canvas settingsCanvas; // Reference to settings canvas if opening from pause menu

        [Header("Key Controls")]
        [SerializeField] private KeyCode pauseKey = KeyCode.P;

        [Header("Navigation")]
        [SerializeField] private string mainMenuSceneName = "MainMenu"; // Name of your Main Menu scene

        private bool isPaused = false;

        public bool IsPaused => isPaused;

        private void Start()
        {
            // Ensure UI is in correct state on game start (disable canvas rendering, but keep GameObject active)
            if (pauseCanvas != null) pauseCanvas.enabled = false;
            if (settingsCanvas != null) settingsCanvas.enabled = false;
            
            Time.timeScale = 1f;
        }

        private void Update()
        {
            // Restrict pause input during tutorial steps
            TutorialManager tutorial = FindFirstObjectByType<TutorialManager>();
            if (tutorial != null && tutorial.enabled)
            {
                var inputReader = FindFirstObjectByType<PlayerInputReader>();
                if (inputReader != null && !inputReader.IsFeatureEnabled("Pause"))
                {
                    return; // Ignore pause input during this tutorial step
                }
            }

            if (Input.GetKeyDown(pauseKey))
            {
                // If settings canvas is open, close it first instead of resuming the game directly
                if (settingsCanvas != null && settingsCanvas.enabled)
                {
                    settingsCanvas.enabled = false;
                    if (pauseCanvas != null) pauseCanvas.enabled = true;
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
            
            if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
            else
            {
                Debug.LogWarning("Main Menu Scene Name is not set in PauseManager!");
            }
        }
    }
}
