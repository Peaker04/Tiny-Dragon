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
        public bool enableAll;
        public bool enableMovement;
        public bool enableJump;
        public bool enableAttack;
        public bool enablePowerShot;
        public bool enablePunch;
        public bool enableKick;
        public bool enableInventory;
        public bool enablePause;
        public bool enableSettings;

        [Header("Custom Enables (Text)")]
        public string[] customEnables;

        [Header("Extra Actions")]
        public UnityEngine.Events.UnityEvent onStepStart;

        [HideInInspector] public bool _previousEnableAll;
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

            playerInput = FindAnyObjectByType<PlayerInputReader>();

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
                    if (step.enableAll)
                    {
                        playerInput.EnableAll();
                    }
                    else
                    {
                        playerInput.EnableMovement(step.enableMovement);
                        playerInput.EnableJump(step.enableJump);
                        playerInput.EnableAttack(step.enableAttack);
                        playerInput.EnablePowerShot(step.enablePowerShot);
                        playerInput.EnablePunch(step.enablePunch);
                        playerInput.EnableKick(step.enableKick);
                        playerInput.EnableInventory(step.enableInventory);
                        playerInput.EnablePause(step.enablePause);
                        playerInput.EnableSettings(step.enableSettings);
                    }

                    // Only clear custom features at the very start of the tutorial (step 0).
                    // This allows custom features from previous steps to carry over to subsequent steps.
                    if (index == 0)
                    {
                        // playerInput.ClearCustomFeatures();
                    }

                    if (step.customEnables != null)
                    {
                        foreach (string customFeature in step.customEnables)
                        {
                            if (!string.IsNullOrWhiteSpace(customFeature))
                            {
                                // playerInput.EnableCustomFeature(customFeature);
                            }
                        }
                    }
                }

                tutorialSteps[index].onStepStart?.Invoke();
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
                playerInput.EnableAll();
            }

            this.enabled = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (tutorialSteps != null)
            {
                for (int i = 0; i < tutorialSteps.Length; i++)
                {
                    // Nếu giá trị của ô enableAll vừa bị người dùng thay đổi
                    if (tutorialSteps[i].enableAll != tutorialSteps[i]._previousEnableAll)
                    {
                        if (tutorialSteps[i].enableAll)
                        {
                            // Người dùng vừa tick vào Enable All -> Tự động tick tất cả ô con
                            tutorialSteps[i].enableMovement = true;
                            tutorialSteps[i].enableJump = true;
                            tutorialSteps[i].enableAttack = true;
                            tutorialSteps[i].enablePowerShot = true;
                            tutorialSteps[i].enablePunch = true;
                            tutorialSteps[i].enableKick = true;
                            tutorialSteps[i].enableInventory = true;
                            tutorialSteps[i].enablePause = true;
                            tutorialSteps[i].enableSettings = true;
                        }
                        else
                        {
                            // Người dùng vừa bỏ tick Enable All -> Tự động bỏ tick tất cả ô con
                            tutorialSteps[i].enableMovement = false;
                            tutorialSteps[i].enableJump = false;
                            tutorialSteps[i].enableAttack = false;
                            tutorialSteps[i].enablePowerShot = false;
                            tutorialSteps[i].enablePunch = false;
                            tutorialSteps[i].enableKick = false;
                            tutorialSteps[i].enableInventory = false;
                            tutorialSteps[i].enablePause = false;
                            tutorialSteps[i].enableSettings = false;
                        }
                        // Cập nhật lại trạng thái previous
                        tutorialSteps[i]._previousEnableAll = tutorialSteps[i].enableAll;
                    }
                    else
                    {
                        // Nếu enableAll KHÔNG bị thay đổi, tức là người dùng vừa click vào một ô con bất kỳ.
                        // Kiểm tra xem tất cả các ô con có đang được tick hay không
                        bool allEnabled = tutorialSteps[i].enableMovement && 
                                          tutorialSteps[i].enableJump && 
                                          tutorialSteps[i].enableAttack && 
                                          tutorialSteps[i].enablePowerShot && 
                                          tutorialSteps[i].enablePunch &&
                                          tutorialSteps[i].enableKick &&
                                          tutorialSteps[i].enableInventory &&
                                          tutorialSteps[i].enablePause &&
                                          tutorialSteps[i].enableSettings;
                        
                        // Tự động điều chỉnh ô Enable All dựa theo các ô con
                        tutorialSteps[i].enableAll = allEnabled;
                        tutorialSteps[i]._previousEnableAll = allEnabled;
                    }
                }
            }
        }
#endif
    }
}