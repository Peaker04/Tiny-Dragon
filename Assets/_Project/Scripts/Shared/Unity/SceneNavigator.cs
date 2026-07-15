using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinyDragon.Shared.Unity
{
    public static class SceneNavigator
    {
        public static string ActiveSceneName => SceneManager.GetActiveScene().name;

        public static bool IsActiveScene(Scene scene)
        {
            return scene == SceneManager.GetActiveScene();
        }

        public static bool LoadSceneIfSet(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("Scene name is empty; load skipped.");
                return false;
            }

            SceneManager.LoadScene(sceneName, mode);
            return true;
        }

        public static void ReloadActiveScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
