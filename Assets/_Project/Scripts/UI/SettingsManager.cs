using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace TinyDragon.UI
{
    [System.Serializable]
    public struct SliderSettingElement
    {
        public string settingName;          // Name of the setting (e.g. "BGM Volume")
        public Slider slider;               // Reference to the UI Slider component
        public string playerPrefsKey;       // Key used to save to PlayerPrefs (e.g. "BGM_Volume")
        [Range(0f, 1f)] public float defaultValue; // Default volume level
        public bool isBgm;                  // Set to true if this controls BackgroundMusicPlayer
    }

    public class SettingsManager : MonoBehaviour
    {
        private static SettingsManager instance;

        [Header("UI Canvases")]
        [SerializeField] private Canvas settingsCanvas;
        [SerializeField] private Canvas pauseCanvas; // Reference to Pause Canvas to toggle automatically

        [Header("Settings Elements (Arrays)")]
        [SerializeField] private SliderSettingElement[] sliderSettings;

        public static float GlobalSFXVolume { get; private set; } = 1f;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(transform.root.gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
        }

        private void Start()
        {
            if (settingsCanvas == null) settingsCanvas = GetComponent<Canvas>();
            if (pauseCanvas == null)
            {
                PauseManager pm = FindFirstObjectByType<PauseManager>();
                if (pm != null) pauseCanvas = pm.GetComponent<Canvas>();
            }

            // Initialize canvas state
            if (settingsCanvas != null) settingsCanvas.enabled = false;

            LoadAndApplySettings();
            SetupEventListeners();
        }


        private void Update()
        {
            PlayerInputReader inputReader = FindAnyObjectByType<PlayerInputReader>();

            if (inputReader != null && inputReader.ConsumeSettingsPressed())
            {
                if (settingsCanvas != null)
                {
                    if (settingsCanvas.enabled)
                    {
                        CloseSettings();
                    }
                    else
                    {
                        OpenSettings();
                    }
                }
            }
        }

        public void ToggleSettings()
        {
            if (settingsCanvas != null)
            {
                if (settingsCanvas.enabled)
                {
                    CloseSettings();
                }
                else
                {
                    OpenSettings();
                }
            }
        }

        public void OpenSettings()
        {
            // Ẩn inventory nếu đang mở
            InventoryPanel.HideIfVisible();
            if (pauseCanvas != null) pauseCanvas.enabled = false; // Close pause canvas automatically
            if (settingsCanvas != null) settingsCanvas.enabled = true;
            LoadAndApplySettings(); // Refresh slider UI values
        }

        public void CloseSettings()
        {
            if (settingsCanvas != null) settingsCanvas.enabled = false;
            
            // If the game is currently paused (Time.timeScale is 0), return to pause menu
            if (pauseCanvas != null && Time.timeScale == 0f)
            {
                pauseCanvas.enabled = true;
            }
            
            PlayerPrefs.Save(); // Save values to disk
        }

        private void SetupEventListeners()
        {
            // Automatically bind OnValueChanged events dynamically to avoid manual Inspector configuration
            for (int i = 0; i < sliderSettings.Length; i++)
            {
                int index = i; // Prevent closure capture issues
                if (sliderSettings[index].slider != null)
                {
                    sliderSettings[index].slider.onValueChanged.RemoveAllListeners();
                    sliderSettings[index].slider.onValueChanged.AddListener((val) => {
                        OnSliderValueChanged(index, val);
                    });
                }
            }
        }

        private void OnSliderValueChanged(int index, float value)
        {
            var element = sliderSettings[index];
            PlayerPrefs.SetFloat(element.playerPrefsKey, value);
            
            if (element.isBgm)
            {
                ApplyBGMVolume(value);
            }
            else
            {
                ApplySFXVolume(value);
            }
        }

        private void LoadAndApplySettings()
        {
            // Load and apply Sliders
            for (int i = 0; i < sliderSettings.Length; i++)
            {
                var element = sliderSettings[i];
                if (element.slider != null)
                {
                    float savedVal = PlayerPrefs.GetFloat(element.playerPrefsKey, element.defaultValue);
                    element.slider.value = savedVal;
                    
                    if (element.isBgm)
                    {
                        ApplyBGMVolume(savedVal);
                    }
                    else
                    {
                        ApplySFXVolume(savedVal);
                    }
                }
            }
        }

        private void ApplyBGMVolume(float volume)
        {
            GameObject bgmPlayer = GameObject.Find("BackgroundMusicPlayer");
            if (bgmPlayer != null)
            {
                AudioSource source = bgmPlayer.GetComponent<AudioSource>();
                if (source != null)
                {
                    source.volume = volume;
                }
            }
        }

        private void ApplySFXVolume(float volume)
        {
            GlobalSFXVolume = volume;
            AudioSource[] allSources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var source in allSources)
            {
                if (source.gameObject.name == "BackgroundMusicPlayer") continue;
                source.volume = volume;
            }
        }
    }
}
