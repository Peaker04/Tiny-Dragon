using UnityEngine;

namespace TinyDragon.UI
{
    public class TutorialManager : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Hình nền đen mờ bao phủ toàn màn hình")]
        public GameObject darkBackground; 
        
        [Tooltip("Danh sách các GameObject chứa nội dung từng bước hướng dẫn (bảng chữ)")]
        public GameObject[] tutorialSteps; 

        [Header("Layout")]
        [SerializeField] private bool applyRuntimeLayout = false;
        [SerializeField] private Vector2 topRightPadding = new Vector2(24f, 14f);
        [SerializeField] private Vector2 textPadding = new Vector2(10f, 0f);
        [SerializeField] private Vector2 stepSize = new Vector2(500f, 34f);
        [SerializeField] private float stepSpacing = 4f;
        [SerializeField] private float fontSize = 22f;

        void Awake()
        {
            ApplyLayoutIfEnabled();
            ShowAllSteps();
        }

        void Start()
        {
            if (darkBackground != null)
            {
                darkBackground.SetActive(false);
            }

            ApplyLayoutIfEnabled();
            ShowAllSteps();
        }

        void ApplyLayoutIfEnabled()
        {
            if (!applyRuntimeLayout)
            {
                return;
            }

            FixTutorialLayout();
        }

        void FixTutorialLayout()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null && tutorialSteps != null)
            {
                for (int i = 0; i < tutorialSteps.Length; i++)
                {
                    if (tutorialSteps[i] == null)
                    {
                        continue;
                    }

                    canvas = tutorialSteps[i].GetComponentInParent<Canvas>();
                    if (canvas != null)
                    {
                        break;
                    }
                }
            }

            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
            }

            if (tutorialSteps == null)
            {
                return;
            }

            float currentY = topRightPadding.y;
            for (int i = 0; i < tutorialSteps.Length; i++)
            {
                if (tutorialSteps[i] == null)
                {
                    continue;
                }

                RectTransform rectTransform = tutorialSteps[i].GetComponent<RectTransform>();
                if (rectTransform == null)
                {
                    continue;
                }

                rectTransform.anchorMin = new Vector2(1f, 1f);
                rectTransform.anchorMax = new Vector2(1f, 1f);
                rectTransform.pivot = new Vector2(1f, 1f);
                rectTransform.anchoredPosition = new Vector2(-topRightPadding.x, -currentY);
                rectTransform.sizeDelta = stepSize;
                FixStepChildren(rectTransform);
                currentY += rectTransform.sizeDelta.y + stepSpacing;
            }
        }

        void FixStepChildren(RectTransform stepRoot)
        {
            for (int i = 0; i < stepRoot.childCount; i++)
            {
                RectTransform child = stepRoot.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                child.anchorMin = new Vector2(0.5f, 0.5f);
                child.anchorMax = new Vector2(0.5f, 0.5f);
                child.pivot = new Vector2(0.5f, 0.5f);
                child.anchoredPosition = textPadding.y * Vector2.up;
                child.sizeDelta = new Vector2(
                    Mathf.Max(0f, stepRoot.sizeDelta.x - textPadding.x * 2f),
                    stepRoot.sizeDelta.y
                );

                TMPro.TMP_Text text = child.GetComponent<TMPro.TMP_Text>();
                if (text != null)
                {
                    text.alignment = TMPro.TextAlignmentOptions.Center;
                    text.fontSize = fontSize;
                    text.enableAutoSizing = true;
                    text.fontSizeMin = 18f;
                    text.fontSizeMax = fontSize;
                    text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                }
            }
        }

        void ShowAllSteps()
        {
            if (tutorialSteps == null)
            {
                return;
            }

            for (int i = 0; i < tutorialSteps.Length; i++)
            {
                if (tutorialSteps[i] == null)
                {
                    continue;
                }

                tutorialSteps[i].SetActive(true);
            }
        }
    }
}
