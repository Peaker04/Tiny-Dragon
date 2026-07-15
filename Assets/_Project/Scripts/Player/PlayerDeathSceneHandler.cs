using UnityEngine;
using UnityEngine.SceneManagement;
using TinyDragon.Config;
using TinyDragon.Shared.Unity;
using TinyDragon.UI;

[RequireComponent(typeof(PlayerHealth))]
public class PlayerDeathSceneHandler : MonoBehaviour
{
    [SerializeField] private TinyDragonRuntimeConfig runtimeConfig;
    [SerializeField] private string guideSceneName = "Level_01_guide";
    [SerializeField] private bool immortalInGuideScene = true;
    [SerializeField] private bool showGameOverOutsideGuide = true;
    [SerializeField] private GameOver gameOverUI;

    private PlayerHealth playerHealth;
    private TinyDragonRuntimeConfig Config => TinyDragonRuntimeConfigProvider.Resolve(runtimeConfig);

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
        // Check immortal scene first — player stays alive at 1 HP
        string configuredImmortalScene = Config.Scenes.playerImmortalSceneName;
        string resolvedImmortalScene = string.IsNullOrWhiteSpace(configuredImmortalScene)
            ? guideSceneName
            : configuredImmortalScene;

        if (immortalInGuideScene && SceneNavigator.ActiveSceneName == resolvedImmortalScene)
        {
            Debug.Log("Player is out of HP but stays alive in immortal scene.");
            health.Revive();
            return;
        }

        // Check guide scene — full revive
        string configuredGuideScene = Config.Scenes.guideSceneName;
        string resolvedGuideScene = string.IsNullOrWhiteSpace(configuredGuideScene) ? guideSceneName : configuredGuideScene;
        if (immortalInGuideScene && SceneNavigator.ActiveSceneName == resolvedGuideScene)
        {
            Debug.Log("Player is out of HP but stays alive in guide level.");
            health.Revive();
            return;
        }

        Debug.Log("Player died.");

        if (showGameOverOutsideGuide)
        {
            GameOver resolvedGameOver = gameOverUI != null ? gameOverUI : ObjectLookup.InactiveAny<GameOver>();
            if (resolvedGameOver != null)
            {
                resolvedGameOver.GameOverActive();
                return;
            }
        }

        SceneNavigator.LoadSceneIfSet(resolvedGuideScene, LoadSceneMode.Single);
    }
}
