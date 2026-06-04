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
        [SerializeField] private KeyCode continueKey = KeyCode.Space;

        private int currentStepIndex = 0;

        void Start()
        {
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
            // Lắng nghe người chơi Click chuột trái để qua bài
            if ((Input.GetMouseButtonDown(0) || Input.GetKeyDown(continueKey)) && currentStepIndex < tutorialSteps.Length)
            {
                NextStep();
            }
        }

        void ShowStep(int index)
        {
            // Tắt toàn bộ các bảng hướng dẫn đi
            for (int i = 0; i < tutorialSteps.Length; i++)
            {
                if (tutorialSteps[i] != null)
                {
                    tutorialSteps[i].SetActive(false);
                }
            }
            
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
                // Hết hướng dẫn -> Tắt nền đen mờ
                if (darkBackground != null)
                {
                    darkBackground.SetActive(false);
                }
                
                // Xóa các bảng chữ để dọn dẹp bộ nhớ (hoặc dùng SetActive(false) nếu muốn dùng lại sau)
                foreach (GameObject step in tutorialSteps)
                {
                    if (step != null) Destroy(step);
                }
                
                // Tắt script này đi vì đã hoàn thành nhiệm vụ
                this.enabled = false;
                
                Debug.Log("Kết thúc hướng dẫn, vào game!");
            }
        }
    }
}
