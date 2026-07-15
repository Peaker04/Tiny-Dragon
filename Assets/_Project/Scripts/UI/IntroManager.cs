using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TinyDragon.Config;
using TinyDragon.Shared.Unity;

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
    [SerializeField] private GameObject skipButton; // Optional: Drag Skip Button game object here

    [Header("Poster Settings")]
    [SerializeField] private CanvasGroup posterCanvasGroup; // Drag poster Canvas/Panel here
    [SerializeField] private float posterDisplayDuration = 2f;
    [SerializeField] private float posterFadeDuration = 1.5f;

    [Header("Story Transition Settings")]
    [SerializeField] private CanvasGroup storyCanvasGroup; // Drag story Panel/Canvas here to fade smoothly
    [SerializeField] private float storyFadeDuration = 0.5f;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;       // Drag an AudioSource here
    [SerializeField] private AudioClip posterAudioClip;     // Clip played during poster display
    [SerializeField] private AudioClip storyAudioClip;      // Clip played during story display
    [SerializeField] private float maxAudioVolume = 0.7f;   // Max volume for fade in

    [Header("Story Data")]
    [SerializeField] private TinyDragonRuntimeConfig runtimeConfig;
    [SerializeField] private StorySceneData[] introScenes;
    [SerializeField] private string nextLevelName = "Level_01_Origin";

    [Header("Developer Options")]
    [SerializeField] private bool forceShowIntro = false;

    private int currentIndex = 0;
    private bool isTransitioning = false;
    private AudioSource globalBgmSource;
    private TinyDragonRuntimeConfig Config => TinyDragonRuntimeConfigProvider.Resolve(runtimeConfig);

    private void Start()
    {
        Time.timeScale = 1f;

        bool hasSeenIntro = !forceShowIntro && PlayerPrefs.GetInt("HasSeenIntro", 0) == 1;

        // Find global background music player and pause it during intro
        GameObject bgmPlayer = ObjectLookup.SceneObject("BackgroundMusicPlayer");
        if (bgmPlayer != null)
        {
            globalBgmSource = bgmPlayer.GetComponent<AudioSource>();
            if (globalBgmSource != null)
            {
                globalBgmSource.Pause();
            }
        }

        // Hide skip button initially during poster display
        if (skipButton != null)
        {
            skipButton.SetActive(false);
        }

        // Initialize UI and Audio states
        if (posterCanvasGroup != null)
        {
            posterCanvasGroup.alpha = 1f;
            posterCanvasGroup.blocksRaycasts = true;
            posterCanvasGroup.gameObject.SetActive(true);
            
            // Hide story elements temporarily
            SetStoryElementsActive(false);
            if (storyCanvasGroup != null)
            {
                storyCanvasGroup.alpha = 0f;
            }
            
            // Play poster music if available
            if (audioSource != null && posterAudioClip != null)
            {
                audioSource.clip = posterAudioClip;
                audioSource.volume = maxAudioVolume;
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

            // Play story music immediately if no poster
            if (audioSource != null && storyAudioClip != null)
            {
                audioSource.clip = storyAudioClip;
                audioSource.volume = maxAudioVolume;
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
        if (isTransitioning) return; // Disable skip/next click during fade transitions

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
        {
            ShowNextScene();
        }
    }

    private IEnumerator PosterSequence(bool skipStory)
    {
        isTransitioning = true;

        // Display the poster
        yield return new WaitForSeconds(posterDisplayDuration);

        // Fade out poster and its audio together
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

        // Enable story elements and skip button
        SetStoryElementsActive(true);
        if (skipButton != null)
        {
            skipButton.SetActive(true);
        }
        
        if (introScenes != null && introScenes.Length > 0)
        {
            UpdateUI();
        }

        // Switch to story music and play
        if (audioSource != null && storyAudioClip != null)
        {
            audioSource.clip = storyAudioClip;
            audioSource.volume = 0f;
            audioSource.loop = true;
            audioSource.Play();
        }

        // Fade in story UI and story music
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
                    audioSource.volume = Mathf.Lerp(0f, maxAudioVolume, t);
                }
                yield return null;
            }
            
            if (storyCanvasGroup != null) storyCanvasGroup.alpha = 1f;
            if (audioSource != null) audioSource.volume = maxAudioVolume;
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

        // 1. Fade out current story scene
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

        // 2. Change content
        currentIndex = targetIndex;
        UpdateUI();

        // 3. Fade in new story scene
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

    // Public method that can be assigned to Skip Button's OnClick event
    public void SkipIntro()
    {
        StopAllCoroutines();
        FinishIntro();
    }

    private void FinishIntro()
    {
        PlayerPrefs.SetInt("HasSeenIntro", 1);
        PlayerPrefs.Save();
        
        // Stop intro music completely
        if (audioSource != null)
        {
            audioSource.Stop();
        }

        // Resume global background music before switching scene
        if (globalBgmSource != null)
        {
            globalBgmSource.Play();
        }

        LoadNextLevel();
    }

    private IEnumerator FadeOutStoryAndLoad()
    {
        isTransitioning = true;

        // Fade out story UI and intro music together before loading scene
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
        string configuredScene = Config.Scenes.introNextSceneName;
        SceneNavigator.LoadSceneIfSet(string.IsNullOrWhiteSpace(configuredScene) ? nextLevelName : configuredScene);
    }
}
