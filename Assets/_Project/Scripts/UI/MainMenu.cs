using UnityEngine;
using UnityEngine.SceneManagement;
using TinyDragon.Data;

namespace TinyDragon.UI
{
    public class MainMenu : MonoBehaviour
    {
        [Header("Debug / Settings")]
        [SerializeField] private string nextSceneName = "LangAru";

        public void PlayGame()
        {
            PlayerAttack.ResetManaForNewRun();
            TinyDragonSaveManager.Instance.ResetCurrentKiToMax();
            SceneManager.LoadScene(nextSceneName);
        }

        public void QuitGame()
        {
            // Thoát game (Chỉ hoạt động khi đã build ra game thật, trên editor sẽ in log)
            Debug.Log("Đã bấm Quit!");
            Application.Quit();
        }
    }
}
