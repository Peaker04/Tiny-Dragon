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
            SceneManager.LoadScene("LangAru");
        }

        public void QuitGame()
        {
            Debug.Log("Đã bấm Quit!");
            Application.Quit();
        }
    }
}
