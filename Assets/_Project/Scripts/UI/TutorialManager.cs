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
        [Header("Step Gating")]
        [SerializeField] private bool advanceStepsWithInput = false;
        [SerializeField] private bool gatePlayerActions = false;
        [SerializeField] private KeyCode moveLeftKey = KeyCode.A;
        [SerializeField] private KeyCode moveRightKey = KeyCode.D;
        [SerializeField] private KeyCode jumpKey = KeyCode.Space;
        [SerializeField] private KeyCode attackKey = KeyCode.J;

        private int currentStepIndex;
        private PlayerInputReader playerInput;

        void Awake()
        {
            ApplyLayoutIfEnabled();
            if (!advanceStepsWithInput)
            {
                ShowAllSteps();
            }
        }

        void Start()
        {
            if (advanceStepsWithInput)
            {
                BeginStepTutorial();
                return;
            }

            if (darkBackground != null)
            {
                darkBackground.SetActive(false);
            }

            ApplyLayoutIfEnabled();
            ShowAllSteps();
        }

        void Update()
        {
            if (!advanceStepsWithInput || tutorialSteps == null || currentStepIndex >= tutorialSteps.Length)
            {
                return;
            }

            if (WasCurrentStepInputPressed())
            {
                NextStep();
            }
        }

        void OnDisable()
        {
            if (gatePlayerActions && playerInput != null)
            {
                playerInput.ResetInputRestrictions();
            }
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

        void BeginStepTutorial()
        {
            if (tutorialSteps == null || tutorialSteps.Length == 0)
            {
                Debug.LogWarning("TutorialManager is missing tutorial steps.", this);
                enabled = false;
                return;
            }

            playerInput = FindAnyObjectByType<PlayerInputReader>();
            currentStepIndex = 0;

            if (darkBackground != null)
            {
                darkBackground.SetActive(true);
            }

            ApplyLayoutIfEnabled();
            ShowStep(currentStepIndex);
        }

        bool WasCurrentStepInputPressed()
        {
            if (currentStepIndex == 0)
            {
                return Input.GetKeyDown(moveLeftKey)
                    || Input.GetKeyDown(moveRightKey)
                    || Input.GetKeyDown(KeyCode.LeftArrow)
                    || Input.GetKeyDown(KeyCode.RightArrow);
            }

            if (currentStepIndex == 1)
            {
                return Input.GetKeyDown(jumpKey) || Input.GetButtonDown("Jump");
            }

            if (currentStepIndex == 2)
            {
                return Input.GetKeyDown(attackKey);
            }

            return Input.anyKeyDown;
        }

        void NextStep()
        {
            currentStepIndex++;
            if (currentStepIndex < tutorialSteps.Length)
            {
                ShowStep(currentStepIndex);
                return;
            }

            CompleteTutorial();
        }

        void ShowStep(int index)
        {
            HideAllSteps();

            if (index >= 0 && index < tutorialSteps.Length && tutorialSteps[index] != null)
            {
                tutorialSteps[index].SetActive(true);
            }

            ApplyPlayerInputForStep(index);
        }

        void HideAllSteps()
        {
            if (tutorialSteps == null)
            {
                return;
            }

            for (int i = 0; i < tutorialSteps.Length; i++)
            {
                if (tutorialSteps[i] != null)
                {
                    tutorialSteps[i].SetActive(false);
                }
            }
        }

        void CompleteTutorial()
        {
            HideAllSteps();

            if (darkBackground != null)
            {
                darkBackground.SetActive(false);
            }

            if (playerInput != null)
            {
                playerInput.ResetInputRestrictions();
            }

            enabled = false;
            Debug.Log("Tutorial completed.");
        }

        void ApplyPlayerInputForStep(int index)
        {
            if (!gatePlayerActions)
            {
                return;
            }

            if (playerInput == null)
            {
                playerInput = FindAnyObjectByType<PlayerInputReader>();
            }

            if (playerInput == null)
            {
                return;
            }

            bool allowJump = index >= 1;
            bool allowAttack = index >= 2;
            playerInput.SetInputEnabled(true, allowJump, allowAttack, allowAttack, allowAttack, allowAttack);
        }
    }
}
