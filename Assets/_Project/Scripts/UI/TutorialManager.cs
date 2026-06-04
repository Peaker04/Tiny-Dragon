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

        [Header("Input")]
        [SerializeField] private KeyCode moveLeftKey = KeyCode.A;
        [SerializeField] private KeyCode moveRightKey = KeyCode.D;
        [SerializeField] private KeyCode jumpKey = KeyCode.Space;

        private int currentStepIndex = 0;
        private bool tutorialFinished;

        void Awake()
        {
            HideAllSteps();
        }

        void Start()
        {
            currentStepIndex = 0;
            tutorialFinished = false;

            if (darkBackground == null)
            {
                Debug.LogWarning("Chưa gán Dark Background vào TutorialManager!");
                return;
            }

            if (tutorialSteps == null || tutorialSteps.Length == 0)
            {
                Debug.LogWarning("Chưa có bước hướng dẫn nào trong TutorialManager!");
                return;
            }

            // Bật nền đen mờ
            darkBackground.SetActive(true);
            
            // Hiển thị bước đầu tiên
            ShowStep(0); 
        }

        void Update()
        {
            if (tutorialFinished || currentStepIndex >= tutorialSteps.Length)
            {
                return;
            }

            if (WasCurrentStepInputPressed())
            {
                NextStep();
            }
        }

        bool WasCurrentStepInputPressed()
        {
            switch (currentStepIndex)
            {
                case 0:
                    return Input.GetKeyDown(moveLeftKey)
                        || Input.GetKeyDown(moveRightKey)
                        || Input.GetKeyDown(KeyCode.LeftArrow)
                        || Input.GetKeyDown(KeyCode.RightArrow);
                case 1:
                    return Input.GetKeyDown(jumpKey);
                default:
                    return false;
            }
        }

        void ShowStep(int index)
        {
            HideAllSteps();
            
            // Bật bảng hướng dẫn hiện tại
            if (index < tutorialSteps.Length && tutorialSteps[index] != null)
            {
                tutorialSteps[index].SetActive(true);
            }
        }

        void NextStep()
        {
            currentStepIndex++;

            if (currentStepIndex < tutorialSteps.Length)
            {
                // Bật bước tiếp theo
                ShowStep(currentStepIndex);
            }
            else
            {
                CompleteTutorial();
            }
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
            tutorialFinished = true;
            HideAllSteps();

            if (darkBackground != null)
            {
                darkBackground.SetActive(false);
            }

            this.enabled = false;

            Debug.Log("Kết thúc hướng dẫn, vào game!");
        }
    }
}
