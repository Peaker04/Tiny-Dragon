using UnityEngine;
using UnityEngine.UI;
using TinyDragon.Audio;
namespace TinyDragon.UI
{
    [System.Serializable]
    public struct SliderSettingElement
    {
        public string settingName;
        public Slider slider;
        public string playerPrefsKey;
        [Range(0f, 1f)] public float defaultValue;
        public bool isBgm;
    }

    public class SettingsManager : MonoBehaviour
    {
        private static SettingsManager instance;
        public static SettingsManager Instance => instance;

        [Header("UI Canvases")]
        [SerializeField] private Canvas settingsCanvas;
        [SerializeField] private Canvas pauseCanvas;

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
        }

        private void Start()
        {
            if (settingsCanvas == null) settingsCanvas = GetComponent<Canvas>();
            if (pauseCanvas == null)
            {
                PauseManager pm = FindFirstObjectByType<PauseManager>();
                if (pm != null) pauseCanvas = pm.GetComponent<Canvas>();
            }

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
            InventoryPanel.HideIfVisible();
            TinyDragon.Audio.UiSoundPlayer.PlayClick();
            if (pauseCanvas != null) pauseCanvas.enabled = false;
            if (settingsCanvas != null) settingsCanvas.enabled = true;
            LoadAndApplySettings();
        }

        public void CloseSettings()
        {
            if (settingsCanvas != null) settingsCanvas.enabled = false;
            
            if (pauseCanvas != null && Time.timeScale == 0f)
            {
                pauseCanvas.enabled = true;
            }
            
            PlayerPrefs.Save();
        }

        private void SetupEventListeners()
        {
            for (int i = 0; i < sliderSettings.Length; i++)
            {
                if (sliderSettings[i].slider != null)
                {
                    int index = i;
                    sliderSettings[index].slider.onValueChanged.RemoveAllListeners();
                    sliderSettings[index].slider.onValueChanged.AddListener((val) => OnSliderValueChanged(index, val));
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
            GlobalSFXVolume = Mathf.Clamp01(volume);
            AudioSource[] allSources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include);
            foreach (var source in allSources)
            {
                if (source.gameObject.name == "BackgroundMusicPlayer") continue;

                PlayerAuraAudio auraAudio = source.GetComponent<PlayerAuraAudio>();
                if (auraAudio != null)
                {
                    auraAudio.RefreshVolumeFromSettings();
                    continue;
                }

                if (source.GetComponent<PlayerActionAudioEmitter>() != null || source.gameObject.name == "UiSoundPlayer")
                {
                    source.volume = 1f;
                    continue;
                }

                source.volume = GlobalSFXVolume;
            }
        }
    }
}
