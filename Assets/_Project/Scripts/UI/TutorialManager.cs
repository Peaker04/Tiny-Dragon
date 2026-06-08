using UnityEngine;

namespace TinyDragon.UI
{
    public class TutorialManager : MonoBehaviour
    {
        public GameObject darkBackground;
        public GameObject[] tutorialSteps;

        [SerializeField] private KeyCode moveLeftKey = KeyCode.A;
        [SerializeField] private KeyCode moveRightKey = KeyCode.D;
        [SerializeField] private KeyCode jumpKey = KeyCode.Space;

        [Header("Developer Options")]
        [SerializeField] private bool forceShowTutorial = false;

        private int currentStepIndex = 0;
        private PlayerInputReader playerInput;

        void Start()
        {
            if (darkBackground == null || tutorialSteps == null || tutorialSteps.Length == 0)
            {
                Debug.LogWarning("Chưa cấu hình Guide!");
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
            if (currentStepIndex == 0)
            {
                return Input.GetKeyDown(moveLeftKey) || Input.GetKeyDown(moveRightKey) ||
                       Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow);
            }
            if (currentStepIndex == 1)
            {
                return Input.GetKeyDown(jumpKey);
            }
            return false;
        }

        void ShowStep(int index)
        {
            foreach (var step in tutorialSteps)
            {
                if (step != null) step.SetActive(false);
            }

            if (index < tutorialSteps.Length && tutorialSteps[index] != null)
            {
                tutorialSteps[index].SetActive(true);
            }

            if (playerInput != null)
            {
                playerInput.SetInputEnabled(true, index == 1, false, false);
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
            foreach (var step in tutorialSteps)
            {
                if (step != null) step.SetActive(false);
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