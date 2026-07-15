using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TinyDragon.Config;
using TinyDragon.Shared.Unity;

namespace TinyDragon.UI
{
    public class GameOver : MonoBehaviour
    {
        private static GameOver instance;

        [SerializeField] private TinyDragonRuntimeConfig runtimeConfig;
        [SerializeField] private string menuSceneName = "Level_01_Origin";
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button menuButton;

        public static GameOver ResolveOrCreate()
        {
            GameOver resolved = instance != null ? instance : ObjectLookup.InactiveAny<GameOver>();
            if (resolved != null)
            {
                return resolved;
            }

            return CreateRuntimeGameOver();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(transform.root.gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
            EnsureEventSystem();
            BindButtons();

            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
            }
        }

        private static GameOver CreateRuntimeGameOver()
        {
            GameObject root = new GameObject("GameOver");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            GameObject panel = CreateRect(root.transform, "GameOverPanel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image overlay = panel.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.65f);

            Text title = CreateText(panel.transform, "Title", "GAME OVER", 48, FontStyle.Bold);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 120f);
            titleRect.sizeDelta = new Vector2(520f, 70f);

            Button playAgain = CreateButton(panel.transform, "PlayAgain", "CHƠI LẠI", new Vector2(0f, 0f));
            Button menu = CreateButton(panel.transform, "Menu", "VỀ MENU", new Vector2(0f, -76f));

            GameOver gameOver = root.AddComponent<GameOver>();
            gameOver.gameOverPanel = panel;
            gameOver.playAgainButton = playAgain;
            gameOver.menuButton = menu;
            gameOver.BindButtons();
            panel.SetActive(false);

            return gameOver;
        }

        private static GameObject CreateRect(
            Transform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            GameObject created = new GameObject(objectName);
            created.transform.SetParent(parent, false);
            RectTransform rect = created.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return created;
        }

        private static Text CreateText(Transform parent, string objectName, string content, int fontSize, FontStyle fontStyle)
        {
            GameObject created = CreateRect(parent, objectName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Text text = created.AddComponent<Text>();
            text.text = content;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return text;
        }

        private static Button CreateButton(Transform parent, string objectName, string label, Vector2 anchoredPosition)
        {
            GameObject buttonObject = CreateRect(parent, objectName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchoredPosition = anchoredPosition;
            buttonRect.sizeDelta = new Vector2(165f, 50f);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(1f, 0.93f, 0f, 1f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText(buttonObject.transform, "Text", label, 22, FontStyle.Normal);
            text.color = new Color(0.2f, 0.18f, 0.08f, 1f);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
        }

        private void OnEnable()
        {
            BindButtons();
        }

        private void BindButtons()
        {
            if (playAgainButton == null || menuButton == null)
            {
                foreach (Button button in GetComponentsInChildren<Button>(true))
                {
                    if (button.name == "PlayAgain")
                    {
                        playAgainButton = button;
                    }
                    else if (button.name == "Menu")
                    {
                        menuButton = button;
                    }
                }
            }

            if (playAgainButton != null)
            {
                playAgainButton.onClick.RemoveListener(PlayAgain);
                playAgainButton.onClick.AddListener(PlayAgain);
            }

            if (menuButton != null)
            {
                menuButton.onClick.RemoveListener(Menu);
                menuButton.onClick.AddListener(Menu);
            }
        }

        public void GameOverActive()
        {
            EnsureEventSystem();
            BindButtons();

            if (gameOverPanel != null)
                gameOverPanel.SetActive(true);
            else
                gameObject.SetActive(true);

            Time.timeScale = 0f;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(eventSystemObject);
        }

        public void PlayAgain()
        {
            Time.timeScale = 1f;

            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
            else
                gameObject.SetActive(false);

            PlayerHealth playerHealth = ObjectLookup.Any<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.Revive();
            }

            SceneNavigator.ReloadActiveScene();
        }

        public void Menu()
        {
            Time.timeScale = 1f;

            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
            else
                gameObject.SetActive(false);

            PlayerHealth playerHealth = ObjectLookup.Any<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.Revive();
            }

            string configuredScene = TinyDragonRuntimeConfigProvider.Resolve(runtimeConfig).Scenes.gameOverMenuSceneName;
            SceneNavigator.LoadSceneIfSet(string.IsNullOrWhiteSpace(configuredScene) ? menuSceneName : configuredScene);
        }
    }
}
