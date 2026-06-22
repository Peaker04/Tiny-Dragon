using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TinyDragon.UI
{
    public sealed class GameOver : MonoBehaviour
    {
        private static GameOver instance;

        [SerializeField] private string menuSceneName = "Intro";

        private Canvas canvas;
        private GraphicRaycaster raycaster;
        private Button playAgainButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance()
        {
            instance = null;
        }

        public static GameOver Ensure()
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<GameOver>();
            }

            if (instance == null)
            {
                GameObject gameOverObject = new GameObject("GameOver");
                gameOverObject.AddComponent<RectTransform>();
                instance = gameOverObject.AddComponent<GameOver>();
            }

            instance.EnsureBuilt();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            EnsureBuilt();
            SetVisible(false);
        }

        public void GameOverActive()
        {
            EnsureBuilt();
            EnsureEventSystem();
            SetVisible(true);
            Time.timeScale = 0f;

            if (EventSystem.current != null && playAgainButton != null)
            {
                EventSystem.current.SetSelectedGameObject(playAgainButton.gameObject);
            }
        }

        public void PlayAgain()
        {
            Time.timeScale = 1f;
            SetVisible(false);

            Scene activeScene = SceneManager.GetActiveScene();
            if (!string.IsNullOrWhiteSpace(activeScene.name))
            {
                SceneManager.LoadScene(activeScene.name, LoadSceneMode.Single);
                return;
            }

            if (activeScene.buildIndex >= 0)
            {
                SceneManager.LoadScene(activeScene.buildIndex, LoadSceneMode.Single);
            }
        }

        public void Menu()
        {
            Time.timeScale = 1f;
            SetVisible(false);

            if (!string.IsNullOrWhiteSpace(menuSceneName))
            {
                SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
                return;
            }

            SceneManager.LoadScene(0, LoadSceneMode.Single);
        }

        private void EnsureBuilt()
        {
            canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 3000;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = gameObject.AddComponent<GraphicRaycaster>();
            }

            RectTransform root = GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            if (transform.Find("Overlay") != null)
            {
                return;
            }

            BuildOverlay(root);
        }

        private void BuildOverlay(RectTransform root)
        {
            RectTransform overlay = CreateRect("Overlay", root);
            Stretch(overlay);
            Image overlayImage = overlay.gameObject.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.72f);
            overlayImage.raycastTarget = true;

            RectTransform panel = CreateRect("Panel", overlay);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(520f, 300f);

            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.07f, 0.08f, 0.1f, 0.94f);
            panelImage.raycastTarget = true;

            Text title = CreateText("Title", panel, "GAME OVER", 58, FontStyle.Bold, Color.white);
            SetRect(title.rectTransform, new Vector2(0f, -38f), new Vector2(480f, 72f));

            playAgainButton = CreateButton("PlayAgain", panel, "Chơi lại", new Vector2(0f, -130f), PlayAgain);
            CreateButton("Menu", panel, "Menu", new Vector2(0f, -205f), Menu);
        }

        private Button CreateButton(string objectName, Transform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform buttonRect = CreateRect(objectName, parent);
            buttonRect.anchorMin = new Vector2(0.5f, 1f);
            buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.anchoredPosition = anchoredPosition;
            buttonRect.sizeDelta = new Vector2(250f, 54f);

            Image image = buttonRect.gameObject.AddComponent<Image>();
            image.color = new Color(0.95f, 0.78f, 0.16f, 1f);

            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            Text text = CreateText("Label", buttonRect, label, 28, FontStyle.Bold, new Color(0.08f, 0.07f, 0.04f, 1f));
            Stretch(text.rectTransform);
            return button;
        }

        private Text CreateText(string objectName, Transform parent, string textValue, int fontSize, FontStyle fontStyle, Color color)
        {
            RectTransform textRect = CreateRect(objectName, parent);
            Text text = textRect.gameObject.AddComponent<Text>();
            text.text = textValue;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
            {
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private void SetVisible(bool visible)
        {
            if (canvas != null)
            {
                canvas.enabled = visible;
            }

            if (raycaster != null)
            {
                raycaster.enabled = visible;
            }
        }

        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            GameObject rectObject = new GameObject(objectName);
            rectObject.transform.SetParent(parent, false);
            return rectObject.AddComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
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
        }
    }
}
