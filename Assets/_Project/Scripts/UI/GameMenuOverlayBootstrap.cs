using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinyDragon.UI
{
    public static class GameMenuOverlayBootstrap
    {
        private const string OverlayName = "GameMenuOverlay";
        private const string OverlayPrefabPath = "UI/GameMenuOverlay";

        private static bool isSubscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            isSubscribed = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureOverlay()
        {
            if (!isSubscribed)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
                isSubscribed = true;
            }

            CreateIfMissing();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CreateIfMissing();
        }

        private static void CreateIfMissing()
        {
            if (GameObject.Find(OverlayName) != null)
            {
                return;
            }

            GameObject overlayPrefab = Resources.Load<GameObject>(OverlayPrefabPath);
            if (overlayPrefab == null)
            {
                Debug.LogWarning($"Game menu overlay prefab not found at Resources/{OverlayPrefabPath}.");
                return;
            }

            GameObject overlay = Object.Instantiate(overlayPrefab);
            Object.DontDestroyOnLoad(overlay);
        }
    }
}
