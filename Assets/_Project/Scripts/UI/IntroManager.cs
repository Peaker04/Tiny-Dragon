using System.Collections;
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
    [SerializeField] private GameObject skipButton;

    [Header("Poster Settings")]
    [SerializeField] private CanvasGroup posterCanvasGroup;
    [SerializeField] private float posterDisplayDuration = 2f;
    [SerializeField] private float posterFadeDuration = 1.5f;

    [Header("Story Transition Settings")]
    [SerializeField] private CanvasGroup storyCanvasGroup;
    [SerializeField] private float storyFadeDuration = 0.5f;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip posterAudioClip;
    [SerializeField] private AudioClip storyAudioClip;
    [SerializeField] private float maxAudioVolume = 0.7f;

    [Header("Story Data")]
    [SerializeField] private StorySceneData[] introScenes;
    [SerializeField] private string nextLevelName = "Level_01_Origin";

    [Header("Developer Options")]
    [SerializeField] private bool forceShowIntro = false;

    private int currentIndex = 0;
    private bool isTransitioning = false;
    private AudioSource globalBgmSource;

    private void Start()
    {
        Time.timeScale = 1f;

        bool hasSeenIntro = !forceShowIntro && PlayerPrefs.GetInt("HasSeenIntro", 0) == 1;

        GameObject bgmPlayer = GameObject.Find("BackgroundMusicPlayer");
        if (bgmPlayer != null)
        {
            globalBgmSource = bgmPlayer.GetComponent<AudioSource>();
            if (globalBgmSource != null)
            {
                globalBgmSource.Pause();
            }
        }

        if (skipButton != null)
        {
            skipButton.SetActive(false);
        }

        if (posterCanvasGroup != null)
        {
            posterCanvasGroup.alpha = 1f;
            posterCanvasGroup.blocksRaycasts = true;
            posterCanvasGroup.gameObject.SetActive(true);
            
            SetStoryElementsActive(false);
            if (storyCanvasGroup != null)
            {
                storyCanvasGroup.alpha = 0f;
            }
            
            if (audioSource != null && posterAudioClip != null)
            {
                audioSource.clip = posterAudioClip;
                audioSource.volume = GetScaledIntroVolume();
                audioSource.loop = true;
                audioSource.Play();
            }

            StartCoroutine(PosterSequence(hasSeenIntro));
        }
        else
        {
            if (hasSeenIntro)
            {
                LoadNextLevel();
                return;
            }

            if (storyCanvasGroup != null)
            {
                storyCanvasGroup.alpha = 1f;
            }
            if (skipButton != null)
            {
                skipButton.SetActive(true);
            }

            if (audioSource != null && storyAudioClip != null)
            {
                audioSource.clip = storyAudioClip;
                audioSource.volume = GetScaledIntroVolume();
                audioSource.loop = true;
                audioSource.Play();
            }

            if (introScenes != null && introScenes.Length > 0)
            {
                UpdateUI();
            }
        }
    }

    private void Update()
    {
        if (isTransitioning) return;

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
        {
            ShowNextScene();
        }
    }

    private IEnumerator PosterSequence(bool skipStory)
    {
        isTransitioning = true;

        yield return new WaitForSeconds(posterDisplayDuration);

        float elapsedTime = 0f;
        float startVolume = (audioSource != null) ? audioSource.volume : 0f;

        while (elapsedTime < posterFadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / posterFadeDuration;
            
            posterCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            if (audioSource != null && posterAudioClip != null)
            {
                audioSource.volume = Mathf.Lerp(startVolume, 0f, t);
            }
            
            yield return null;
        }

        posterCanvasGroup.alpha = 0f;
        posterCanvasGroup.blocksRaycasts = false;
        posterCanvasGroup.gameObject.SetActive(false);

        if (audioSource != null)
        {
            audioSource.Stop();
        }

        if (skipStory)
        {
            FinishIntro();
            yield break;
        }

        SetStoryElementsActive(true);
        if (skipButton != null)
        {
            skipButton.SetActive(true);
        }
        
        if (introScenes != null && introScenes.Length > 0)
        {
            UpdateUI();
        }

        if (audioSource != null && storyAudioClip != null)
        {
            audioSource.clip = storyAudioClip;
            audioSource.volume = 0f;
            audioSource.loop = true;
            audioSource.Play();
        }

        if (storyCanvasGroup != null || audioSource != null)
        {
            elapsedTime = 0f;
            while (elapsedTime < storyFadeDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / storyFadeDuration;
                
                if (storyCanvasGroup != null)
                {
                    storyCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
                }
                if (audioSource != null && storyAudioClip != null)
                {
                    audioSource.volume = Mathf.Lerp(0f, GetScaledIntroVolume(), t);
                }
                yield return null;
            }
            
            if (storyCanvasGroup != null) storyCanvasGroup.alpha = 1f;
            if (audioSource != null) audioSource.volume = GetScaledIntroVolume();
        }

        isTransitioning = false;
    }

    private void SetStoryElementsActive(bool active)
    {
        if (displayImage != null) displayImage.gameObject.SetActive(active);
        if (displayText != null) displayText.gameObject.SetActive(active);
    }

    private void ShowNextScene()
    {
        int nextIndex = currentIndex + 1;

        if (nextIndex >= introScenes.Length)
        {
            StartCoroutine(FadeOutStoryAndLoad());
        }
        else
        {
            StartCoroutine(TransitionToNextScene(nextIndex));
        }
    }

    private IEnumerator TransitionToNextScene(int targetIndex)
    {
        isTransitioning = true;

        if (storyCanvasGroup != null)
        {
            float elapsedTime = 0f;
            while (elapsedTime < storyFadeDuration)
            {
                elapsedTime += Time.deltaTime;
                storyCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / storyFadeDuration);
                yield return null;
            }
            storyCanvasGroup.alpha = 0f;
        }

        currentIndex = targetIndex;
        UpdateUI();

        if (storyCanvasGroup != null)
        {
            float elapsedTime = 0f;
            while (elapsedTime < storyFadeDuration)
            {
                elapsedTime += Time.deltaTime;
                storyCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / storyFadeDuration);
                yield return null;
            }
            storyCanvasGroup.alpha = 1f;
        }

        isTransitioning = false;
    }

    private void UpdateUI()
    {
        if (displayImage != null && currentIndex < introScenes.Length)
            displayImage.sprite = introScenes[currentIndex].illustration;
            
        if (displayText != null && currentIndex < introScenes.Length)
            displayText.text = introScenes[currentIndex].narrativeText;
    }

    public void SkipIntro()
    {
        StopAllCoroutines();
        FinishIntro();
    }

    private void FinishIntro()
    {
        PlayerPrefs.SetInt("HasSeenIntro", 1);
        PlayerPrefs.Save();
        
        if (audioSource != null)
        {
            audioSource.Stop();
        }

        if (globalBgmSource != null)
        {
            globalBgmSource.Play();
        }

        LoadNextLevel();
    }

    private IEnumerator FadeOutStoryAndLoad()
    {
        isTransitioning = true;

        float elapsedTime = 0f;
        float startVolume = (audioSource != null) ? audioSource.volume : 0f;

        while (elapsedTime < storyFadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / storyFadeDuration;

            if (storyCanvasGroup != null)
            {
                storyCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            }
            if (audioSource != null)
            {
                audioSource.volume = Mathf.Lerp(startVolume, 0f, t);
            }
            yield return null;
        }

        FinishIntro();
    }

    private void LoadNextLevel()
    {
        if (!string.IsNullOrWhiteSpace(nextLevelName))
        {
            SceneManager.LoadScene(nextLevelName);
        }
    }

    private float GetScaledIntroVolume()
    {
        return maxAudioVolume * TinyDragon.UI.SettingsManager.GlobalSFXVolume;
    }
}
