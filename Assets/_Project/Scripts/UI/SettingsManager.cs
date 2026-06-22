using UnityEngine;
using UnityEngine.UI;

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
        [Header("UI Canvases")]
        [SerializeField] private Canvas settingsCanvas;
        [SerializeField] private Canvas pauseCanvas; // Reference to Pause Canvas to toggle automatically

        [Header("Key Controls")]
        [SerializeField] private KeyCode settingsKey = KeyCode.I;

        [Header("Settings Elements (Arrays)")]
        [SerializeField] private SliderSettingElement[] sliderSettings;

        private void Start()
        {
            // Initialize canvas state (disable rendering, but keep GameObject active)
            if (settingsCanvas != null) settingsCanvas.enabled = false;

            LoadAndApplySettings();
            SetupEventListeners();
        }

        private void Update()
        {
            // Restrict settings input during tutorial steps
            TutorialManager tutorial = FindFirstObjectByType<TutorialManager>();
            if (tutorial != null && tutorial.enabled)
            {
                var inputReader = FindFirstObjectByType<PlayerInputReader>();
                if (inputReader != null && !inputReader.IsFeatureEnabled("Settings"))
                {
                    return; // Ignore settings input during this tutorial step
                }
            }

            if (Input.GetKeyDown(settingsKey))
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
    }
}
