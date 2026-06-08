using UnityEngine;
using UnityEngine.SceneManagement;
using TinyDragon.Data;

namespace TinyDragon.UI
{
    public class MainMenu : MonoBehaviour
    {
        public void PlayGame()
        {
            PlayerAttack.ResetManaForNewRun();
            TinyDragonSaveManager.Instance.ResetCurrentKiToMax();
            SceneManager.LoadScene("Level_01_guide");
        }

        public void QuitGame()
        {
            // Thoát game (Chỉ hoạt động khi đã build ra game thật, trên editor sẽ in log)
            Debug.Log("Đã bấm Quit!");
            Application.Quit();
        }
    }
}
