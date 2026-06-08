using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinyDragon.UI
{
    public class MainMenu : MonoBehaviour
    {
        public void PlayGame()
        {
            SceneManager.LoadScene("Level_01_guide");
        }

        public void QuitGame()
        {
            Debug.Log("Đã bấm Quit!");
            Application.Quit();
        }
    }
}
