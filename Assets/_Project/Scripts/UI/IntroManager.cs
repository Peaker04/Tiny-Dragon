using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

[System.Serializable]
public struct StorySceneData
{
    public Sprite illustration;
    [TextArea(3, 5)]
    public string narrativeText;
}

public class IntroManager : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image displayImage;
    [SerializeField] private TextMeshProUGUI displayText;

    [Header("Story Data")]
    [SerializeField] private StorySceneData[] introScenes;
    [SerializeField] private string nextLevelName = "Level_01_guide";

    [Header("Developer Options")]
    [SerializeField] private bool forceShowIntro = false;

    private int currentIndex = 0;

    private void Start()
    {
        Time.timeScale = 1f;

        if (!forceShowIntro && PlayerPrefs.GetInt("HasSeenIntro", 0) == 1)
        {
            LoadNextLevel();
            return;
        }

        if (introScenes != null && introScenes.Length > 0)
        {
            UpdateUI();
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
        {
            ShowNextScene();
        }
    }

    private void ShowNextScene()
    {
        currentIndex++;

        if (currentIndex >= introScenes.Length)
        {
            PlayerPrefs.SetInt("HasSeenIntro", 1);
            PlayerPrefs.Save();

            LoadNextLevel();
        }
        else
        {
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        displayImage.sprite = introScenes[currentIndex].illustration;
        displayText.text = introScenes[currentIndex].narrativeText;
    }

    private void LoadNextLevel()
    {
        if (!string.IsNullOrWhiteSpace(nextLevelName))
        {
            SceneManager.LoadScene(nextLevelName);
        }
    }
}