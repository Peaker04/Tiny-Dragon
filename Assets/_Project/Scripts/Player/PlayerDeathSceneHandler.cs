using UnityEngine;
using TinyDragon.Data;
using TinyDragon.Shared.Unity;

[RequireComponent(typeof(PlayerHealth))]
public class PlayerDeathSceneHandler : MonoBehaviour
{
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
        Debug.Log($"Player died. Reloading scene '{SceneNavigator.ActiveSceneName}'.");
        TinyDragonSaveManager.Instance.MarkDeathRespawnFullRestore();
        SceneNavigator.ReloadActiveScene();
    }
}
