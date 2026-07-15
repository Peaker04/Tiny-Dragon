using System.Collections;
using TinyDragon.Shared.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace TinyDragon.UI
{
    public sealed class BossKOOverlay : MonoBehaviour
    {
        private const string OverlayName = "BossKOOverlay";

        [SerializeField] private float enterDuration = .18f;
        [SerializeField] private float holdDuration = 2.1f;
        [SerializeField] private float fadeDuration = .32f;

        private CanvasGroup canvasGroup;
        private CanvasGroup contentGroup;
        private RectTransform contentRoot;
        private Text koText;

        public static void Show()
        {
            BossKOOverlay overlay = ObjectLookup.InactiveAny<BossKOOverlay>();
            if (overlay == null)
            {
                overlay = CreateOverlay();
            }

            overlay.ShowInternal();
        }

        private static BossKOOverlay CreateOverlay()
        {
            GameObject root = new GameObject(OverlayName);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            root.AddComponent<GraphicRaycaster>();
            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            CreateFullScreenPanel(root.transform, "Dim", new Color(0f, 0f, 0f, .5f));
            CreateLetterbox(root.transform, "TopBar", new Vector2(.5f, 1f), new Vector2(1920f, 130f));
            CreateLetterbox(root.transform, "BottomBar", new Vector2(.5f, 0f), new Vector2(1920f, 130f));

            GameObject content = new GameObject("Content");
            content.transform.SetParent(root.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(.5f, .5f);
            contentRect.anchorMax = new Vector2(.5f, .5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(960f, 420f);
            CanvasGroup contentCanvasGroup = content.AddComponent<CanvasGroup>();
            contentCanvasGroup.blocksRaycasts = false;
            contentCanvasGroup.interactable = false;

            CreateAccentBand(content.transform, "BackSlash", new Vector2(0f, -2f), new Vector2(840f, 96f), new Color(.55f, .02f, .02f, .9f), -7f);
            CreateAccentBand(content.transform, "GoldLine", new Vector2(0f, -74f), new Vector2(760f, 10f), new Color(1f, .72f, .1f, .95f), -7f);

            Font font = LoadUiFont();
            CreateText(content.transform, "KOShadow", "K.O", font, 178, FontStyle.Bold, new Color(.1f, 0f, 0f, .94f), new Vector2(12f, -18f), new Vector2(760f, 250f));
            Text text = CreateText(content.transform, "KOText", "K.O", font, 178, FontStyle.Bold, new Color(1f, .9f, .16f, 1f), new Vector2(0f, 0f), new Vector2(760f, 250f));
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.12f, .02f, 0f, 1f);
            outline.effectDistance = new Vector2(7f, -7f);
            CreateText(content.transform, "Subtitle", "BOSS DEFEATED", font, 36, FontStyle.Bold, new Color(1f, 1f, 1f, .92f), new Vector2(0f, -128f), new Vector2(620f, 70f));

            BossKOOverlay overlay = root.AddComponent<BossKOOverlay>();
            overlay.canvasGroup = group;
            overlay.contentGroup = contentCanvasGroup;
            overlay.contentRoot = contentRect;
            overlay.koText = text;
            root.SetActive(false);
            return overlay;
        }

        private static void CreateFullScreenPanel(Transform parent, string objectName, Color color)
        {
            GameObject panel = new GameObject(objectName);
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = panel.AddComponent<Image>();
            image.color = color;
        }

        private static void CreateLetterbox(Transform parent, string objectName, Vector2 anchor, Vector2 size)
        {
            GameObject bar = new GameObject(objectName);
            bar.transform.SetParent(parent, false);
            RectTransform rect = bar.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            Image image = bar.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, .76f);
        }

        private static void CreateAccentBand(Transform parent, string objectName, Vector2 position, Vector2 size, Color color, float zRotation)
        {
            GameObject band = new GameObject(objectName);
            band.transform.SetParent(parent, false);
            RectTransform rect = band.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, zRotation);
            Image image = band.AddComponent<Image>();
            image.color = color;
        }

        private static Text CreateText(Transform parent, string objectName, string value, Font font, int size, FontStyle style, Color color, Vector2 position, Vector2 boxSize)
        {
            GameObject textObject = new GameObject(objectName);
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = boxSize;

            Text text = textObject.AddComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(12, size / 2);
            text.resizeTextMaxSize = size;
            text.text = value;
            return text;
        }

        private static Font LoadUiFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                return font;
            }

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font != null ? font : Font.CreateDynamicFontFromOSFont("Arial", 128);
        }

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (koText == null)
            {
                koText = GetComponentInChildren<Text>(true);
            }

            if (contentGroup == null)
            {
                Transform content = transform.Find("Content");
                if (content != null)
                {
                    contentGroup = content.GetComponent<CanvasGroup>();
                    contentRoot = content.GetComponent<RectTransform>();
                }
            }
        }

        private void ShowInternal()
        {
            StopAllCoroutines();
            gameObject.SetActive(true);
            if (koText != null)
            {
                koText.text = "K.O";
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            if (contentGroup != null)
            {
                contentGroup.alpha = 0f;
            }

            if (contentRoot != null)
            {
                contentRoot.localScale = Vector3.one * .78f;
            }

            StartCoroutine(AnimateAndHide());
        }

        private IEnumerator AnimateAndHide()
        {
            float elapsed = 0f;
            while (elapsed < enterDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / enterDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                if (contentGroup != null)
                {
                    contentGroup.alpha = eased;
                }

                if (contentRoot != null)
                {
                    float scale = Mathf.Lerp(.78f, 1.08f, eased);
                    contentRoot.localScale = Vector3.one * scale;
                }

                yield return null;
            }

            if (contentRoot != null)
            {
                contentRoot.localScale = Vector3.one;
            }

            yield return new WaitForSecondsRealtime(holdDuration);

            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                }

                yield return null;
            }

            gameObject.SetActive(false);
        }
    }
}
