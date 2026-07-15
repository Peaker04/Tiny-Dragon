using UnityEngine;
using TinyDragon.Data;
using TinyDragon.Config;
using TinyDragon.Shared.Unity;

namespace TinyDragon.UI
{
    public class MainMenu : MonoBehaviour
    {
        [Header("Debug / Settings")]
        [SerializeField] private TinyDragonRuntimeConfig runtimeConfig;
        [SerializeField] private string nextSceneName = "LangAru";

        public void PlayGame()
        {
            PlayerAttack.ResetManaForNewRun();
            TinyDragonSaveManager.Instance.ResetCurrentKiToMax();
            string configuredScene = TinyDragonRuntimeConfigProvider.Resolve(runtimeConfig).Scenes.newGameSceneName;
            SceneNavigator.LoadSceneIfSet(string.IsNullOrWhiteSpace(configuredScene) ? nextSceneName : configuredScene);
        }

        public void QuitGame()
        {
            // Thoát game (Chỉ hoạt động khi đã build ra game thật, trên editor sẽ in log)
            Debug.Log("Đã bấm Quit!");
            Application.Quit();
        }
    }
}
