using UnityEngine;
using TMPro;

namespace TinyDragon.UI
{
    [System.Serializable]
    public struct TutorialStepData
    {
        [TextArea(3, 5)]
        public string instructionText;
        public KeyCode[] requiredKeys;
        
        [Header("Player Input Permissions")]
        public bool enableMovement;
        public bool enableJump;
        public bool enableAttack;
        public bool enablePowerShot;
    }

    public class TutorialManager : MonoBehaviour
    {
        [Header("UI Components")]
        public GameObject darkBackground;
        public TextMeshProUGUI displayText;

        [Header("Tutorial Data")]
        public TutorialStepData[] tutorialSteps;

        [Header("Developer Options")]
        [SerializeField] private bool forceShowTutorial = false;

        private int currentStepIndex = 0;
        private PlayerInputReader playerInput;

        void Start()
        {
            if (darkBackground == null || displayText == null || tutorialSteps == null || tutorialSteps.Length == 0)
            {
                Debug.LogWarning("Chưa cấu hình Tutorial Manager!");
                this.enabled = false;
                return;
            }

            playerInput = FindFirstObjectByType<PlayerInputReader>();

            if (!forceShowTutorial && PlayerPrefs.GetInt("HasSeenTutorial", 0) == 1)
            {
                SkipTutorialLogic();
                return;
            }

            darkBackground.SetActive(true);
            displayText.gameObject.SetActive(true);
            ShowStep(0);
        }

        void Update()
        {
            if (currentStepIndex >= tutorialSteps.Length) return;

            if (WasCurrentStepInputPressed())
            {
                NextStep();
            }
        }

        bool WasCurrentStepInputPressed()
        {
            var keys = tutorialSteps[currentStepIndex].requiredKeys;
            if (keys == null || keys.Length == 0)
            {
                // Nếu bước này không yêu cầu phím đặc biệt, nhấn chuột trái hoặc phím Space để qua bước
                return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
            }

            foreach (var key in keys)
            {
                if (Input.GetKeyDown(key))
                {
                    return true;
                }
            }
            return false;
        }

        void ShowStep(int index)
        {
            if (index < tutorialSteps.Length)
            {
                displayText.text = tutorialSteps[index].instructionText;

                if (playerInput != null)
                {
                    var step = tutorialSteps[index];
                    playerInput.SetInputEnabled(step.enableMovement, step.enableJump, step.enableAttack, step.enablePowerShot);
                }
            }
        }

        void NextStep()
        {
            currentStepIndex++;

            if (currentStepIndex < tutorialSteps.Length)
            {
                ShowStep(currentStepIndex);
            }
            else
            {
                CompleteTutorial();
            }
        }

        void CompleteTutorial()
        {
            PlayerPrefs.SetInt("HasSeenTutorial", 1);
            PlayerPrefs.Save();

            SkipTutorialLogic();
            Debug.Log("Kết thúc hướng dẫn, vào game!");
        }

        void SkipTutorialLogic()
        {
            if (displayText != null)
            {
                displayText.gameObject.SetActive(false);
            }

            if (darkBackground != null)
            {
                darkBackground.SetActive(false);
            }

            if (playerInput != null)
            {
                playerInput.SetInputEnabled(true, true, true, true);
            }

            this.enabled = false;
        }
    }
}