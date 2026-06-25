using UnityEngine;
using TinyDragon.Shared.Unity;

[RequireComponent(typeof(Collider2D))]
public sealed class SceneExitOnPlayerContact : MonoBehaviour
{
    [SerializeField] private string targetSceneName;
    [SerializeField] private bool useTargetSpawnPosition;
    [SerializeField] private Vector3 targetSpawnPosition;
    [SerializeField] private float targetFacingDirection = 1f;
    [SerializeField] private bool healOnExit;

    private bool isLoadingScene;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryLoadTargetScene(collision.collider);
        TryLoadTargetScene(collision.otherCollider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryLoadTargetScene(other);
    }

    private void TryLoadTargetScene(Collider2D playerCollider)
    {
        if (isLoadingScene || string.IsNullOrWhiteSpace(targetSceneName))
        {
            return;
        }

        if (playerCollider.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        isLoadingScene = true;

        if (healOnExit)
        {
            PlayerHealth playerHealth = playerCollider.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.Revive();
            }
        }

        if (useTargetSpawnPosition)
        {
            PlayerSceneTransition.LoadSceneWithPlayerSpawn(
                targetSceneName,
                targetSpawnPosition,
                targetFacingDirection
            );
            return;
        }

        SceneNavigator.LoadSceneIfSet(targetSceneName);
    }
}
