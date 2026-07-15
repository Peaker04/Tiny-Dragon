using System.Collections.Generic;
using TinyDragon.Data;
using TinyDragon.Config;
using TinyDragon.Shared.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TinyDragon.UI
{
    [System.Serializable]
    public struct InventoryItemSlot
    {
        public Image iconImage;
        public Text nameText;
        public Text statText;
        public Button button;
    }

    public sealed class InventoryPanel : MonoBehaviour
    {
        private static InventoryPanel instance;
        public static bool IsVisible => instance != null && instance.panel != null && instance.panel.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapRuntimeInventory()
        {
            SceneManager.sceneLoaded -= HandleGlobalSceneLoaded;
            SceneManager.sceneLoaded += HandleGlobalSceneLoaded;
            EnsureRuntimeInstance();
        }

        private static void HandleGlobalSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureRuntimeInstance();
        }

        private static InventoryPanel EnsureRuntimeInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            if (!ShouldSceneHaveRuntimeInventory())
            {
                return null;
            }

            TinyDragonRuntimeConfig config = TinyDragonRuntimeConfigProvider.Resolve(null);
            GameObject prefab = ResourceLoader.Load<GameObject>(config.Resources.inventoryCanvasPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"InventoryPanel could not load prefab at Resources/{config.Resources.inventoryCanvasPrefabPath}.");
                return null;
            }

            GameObject created = Instantiate(prefab);
            created.name = prefab.name;
            return created.GetComponentInChildren<InventoryPanel>(true);
        }

        private static bool ShouldSceneHaveRuntimeInventory()
        {
            if (SceneManager.GetActiveScene().name == "Level_01_Original")
            {
                return false;
            }

            return ObjectLookup.Any<PlayerHealth>() != null;
        }

        /// <summary>Ẩn inventory nếu đang mở. Dùng khi mở Pause / Settings.</summary>
        public static void HideIfVisible()
        {
            if (IsVisible) instance.SetVisible(false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            SceneManager.sceneLoaded -= HandleGlobalSceneLoaded;
            instance = null;
        }

        [Header("UI Elements")]
        [SerializeField] private TinyDragonRuntimeConfig runtimeConfig;
        [SerializeField] private Canvas canvas;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text nameText;
        [SerializeField] private Text questStatsText;
        [SerializeField] private Text itemStatsText;
        [SerializeField] private Text skillStatsText;
        [SerializeField] private Text featureStatsText;
        [SerializeField] private Text goldText;
        [SerializeField] private Text bossGemText;
        [SerializeField] private Text premiumText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Image avatarImage;

        [Header("Item List Fields (Scroll Content)")]
        [SerializeField] private GameObject itemListPanel;
        [SerializeField] private GameObject questPanel;
        [SerializeField] private GameObject skillPanel;
        [SerializeField] private GameObject featurePanel;
        [SerializeField] private List<InventoryItemSlot> itemSlots = new List<InventoryItemSlot>();

        [Header("Tab Controls")]
        [SerializeField] private List<Button> tabButtons = new List<Button>();
        [SerializeField] private Button downArrowButton;

        private InventoryViewData currentInventoryData;
        private float nextRuntimeStatsRefreshTime;
        private int activeTabIndex = 1; // Default to Tab 1: Hành Trang (Inventory)
        private int initialSlotsCount;
        private int selectedItemIndex = -1;
        private int selectedSkillIndex = -1;
        private TinyDragonRuntimeConfig Config => TinyDragonRuntimeConfigProvider.Resolve(runtimeConfig);

        private void Awake()
        {
            if (canvas == null) canvas = GetComponent<Canvas>();

            if (panel == null)
            {
                Transform p = transform.Find("Panel");
                if (p != null) panel = p.gameObject;
            }

            if (panel != null)
            {
                if (nameText == null) nameText = FindComponent<Text>(panel.transform, "Header/Name");
                if (questStatsText == null) questStatsText = FindComponent<Text>(panel.transform, "Header/Stats/QuestStats") ?? FindComponent<Text>(panel.transform, "Header/Stats/queststats");
                if (itemStatsText == null) itemStatsText = FindComponent<Text>(panel.transform, "Header/Stats/ItemStats") ?? FindComponent<Text>(panel.transform, "Header/Stats/itemstats");
                if (skillStatsText == null) skillStatsText = FindComponent<Text>(panel.transform, "Header/Stats/SkillStats") ?? FindComponent<Text>(panel.transform, "Header/Stats/skillstats");
                if (featureStatsText == null) featureStatsText = FindComponent<Text>(panel.transform, "Header/Stats/FeatureStats") ?? FindComponent<Text>(panel.transform, "Header/Stats/featurestats");
            }

            if (Application.isPlaying && instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            initialSlotsCount = itemSlots.Count;

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() => SetVisible(false));
            }

            // Setup Tab Click Listeners
            for (int i = 0; i < tabButtons.Count; i++)
            {
                int index = i;
                if (tabButtons[i] != null)
                {
                    tabButtons[i].onClick.RemoveAllListeners();
                    tabButtons[i].onClick.AddListener(() => OnTabClicked(index));
                }
            }

            if (downArrowButton != null)
            {
                downArrowButton.onClick.RemoveAllListeners();
                downArrowButton.onClick.AddListener(ScrollDown);
            }

            if (Application.isPlaying)
            {
                SetVisible(false);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
                TinyDragonSaveManager.Instance.GoldChanged += HandleGoldChanged;
            }
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                TinyDragonSaveManager manager = TinyDragonSaveManager.ExistingInstance;
                if (manager != null)
                {
                    manager.GoldChanged -= HandleGoldChanged;
                }
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            PlayerInputReader inputReader = ObjectLookup.Any<PlayerInputReader>();
            
            // Check if inventory should be forcibly closed
            if (panel != null && panel.activeSelf && !ShouldInventoryBeOpenable())
            {
                SetVisible(false);
            }

            if (inputReader != null && inputReader.ConsumeInventoryPressed())
            {
                if (!ShouldInventoryBeOpenable())
                {
                    SetVisible(false);
                    return;
                }

                if (panel != null)
                {
                    SetVisible(!panel.activeSelf);
                }
            }

            if (panel != null && panel.activeSelf && Time.unscaledTime >= nextRuntimeStatsRefreshTime)
            {
                nextRuntimeStatsRefreshTime = Time.unscaledTime + 0.25f;
                Refresh();
            }
        }

        private void OnTabClicked(int tabIndex)
        {
            activeTabIndex = tabIndex;
            selectedItemIndex = -1;
            selectedSkillIndex = -1;
            if (skillStatsText != null) skillStatsText.text = "Chọn chỉ số hoặc kỹ năng để xem chi tiết";
            UpdateTabVisuals();
            Refresh();
        }

        private void ScrollDown()
        {
            GameObject activePanel = null;
            if (activeTabIndex == 0) activePanel = questPanel;
            else if (activeTabIndex == 1) activePanel = itemListPanel;
            else if (activeTabIndex == 2) activePanel = skillPanel;
            else if (activeTabIndex == 3) activePanel = featurePanel;

            if (activePanel != null)
            {
                ScrollRect scrollRect = activePanel.GetComponent<ScrollRect>();
                if (scrollRect != null)
                {
                    scrollRect.verticalNormalizedPosition = Mathf.Max(0f, scrollRect.verticalNormalizedPosition - 1f);
                }
            }
        }

        private void UpdateTabVisuals()
        {
            UiTheme uiTheme = Config.Ui;
            Color32 activeTabColor = uiTheme.inventoryActiveTabColor;
            Color32 inactiveTabColor = uiTheme.inventoryInactiveTabColor;
            Color32 activeTabTextColor = uiTheme.inventoryActiveTabTextColor;
            Color32 inactiveTabTextColor = uiTheme.inventoryInactiveTabTextColor;

            for (int i = 0; i < tabButtons.Count; i++)
            {
                if (tabButtons[i] == null) continue;
                Image img = tabButtons[i].GetComponent<Image>();
                Text txt = tabButtons[i].GetComponentInChildren<Text>();
                bool isActive = (i == activeTabIndex);
                if (img != null) img.color = isActive ? activeTabColor : inactiveTabColor;
                if (txt != null) txt.color = isActive ? activeTabTextColor : inactiveTabTextColor;
            }

            if (questPanel != null) questPanel.SetActive(activeTabIndex == 0);
            if (itemListPanel != null) itemListPanel.SetActive(activeTabIndex == 1);
            if (skillPanel != null) skillPanel.SetActive(activeTabIndex == 2);
            if (featurePanel != null) featurePanel.SetActive(activeTabIndex == 3);

            if (questStatsText != null) questStatsText.gameObject.SetActive(activeTabIndex == 0);
            if (itemStatsText != null) itemStatsText.gameObject.SetActive(activeTabIndex == 1);
            if (skillStatsText != null) skillStatsText.gameObject.SetActive(activeTabIndex == 2);
            if (featureStatsText != null) featureStatsText.gameObject.SetActive(activeTabIndex == 3);
        }

        private void SetVisible(bool visible)
        {
            if (panel != null)
            {
                panel.SetActive(visible);
            }

            if (visible)
            {
                UpdateTabVisuals();
                Refresh();
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!ShouldInventoryBeOpenable()) SetVisible(false);
        }

        private void HandleGoldChanged(int totalGold)
        {
            if (goldText != null)
            {
                goldText.text = totalGold.ToString();
            }
        }

        private bool ShouldInventoryBeOpenable()
        {
            if (SceneManager.GetActiveScene().name == "Level_01_Original")
            {
                return false;
            }

            return ObjectLookup.Any<PlayerHealth>() != null;
        }

        private void Refresh()
        {
            if (TinyDragonSaveManager.Instance == null) return;

            // Gather slots for the active panel dynamically at runtime
            GameObject activePanel = null;
            if (activeTabIndex == 1) activePanel = itemListPanel;
            else if (activeTabIndex == 2) activePanel = skillPanel;

            if (activePanel != null)
            {
                Transform container = activePanel.transform.Find("Viewport/Content");
                if (container == null) container = activePanel.transform;

                itemSlots.Clear();
                
                // Get all children of the container
                List<Transform> children = new List<Transform>();
                foreach (Transform child in container)
                {
                    children.Add(child);
                }
                
                // Sort children by Y coordinate descending (top to bottom visually on screen)
                children.Sort((a, b) => b.localPosition.y.CompareTo(a.localPosition.y));

                foreach (Transform child in children)
                {
                    var texts = child.GetComponentsInChildren<Text>(true);
                    var images = child.GetComponentsInChildren<Image>(true);
                    
                    if (texts.Length > 0 || images.Length > 0)
                    {
                        InventoryItemSlot slot = new InventoryItemSlot();
                        
                        // Find icon image
                        slot.iconImage = FindComponent<Image>(child, "IconCell/Icon");
                        if (slot.iconImage == null)
                        {
                            foreach (var img in images)
                            {
                                if (img.transform != child)
                                {
                                    slot.iconImage = img;
                                    break;
                                }
                            }
                        }
                        
                        // Find text components (fallback to index order if not found by name)
                        if (texts.Length > 0) slot.nameText = texts[0];
                        if (texts.Length > 1) slot.statText = texts[1];
                        
                        Text namedName = FindComponent<Text>(child, "ItemName");
                        if (namedName != null) slot.nameText = namedName;
                        
                        Text namedStat = FindComponent<Text>(child, "ItemStat");
                        if (namedStat != null) slot.statText = namedStat;

                        slot.button = child.GetComponent<Button>();
                        
                        itemSlots.Add(slot);
                    }
                }
                initialSlotsCount = itemSlots.Count;
            }

            CleanupNullSlots();

            InventoryViewData data = TinyDragonSaveManager.Instance.LoadInventory();
            currentInventoryData = data;

            if (nameText != null)
            {
                nameText.text = string.IsNullOrWhiteSpace(data.DisplayName) ? "Player" : data.DisplayName;
            }

            if (avatarImage != null)
            {
                if (!string.IsNullOrEmpty(data.AvatarPath))
                {
                    Sprite sprite = ResourceLoader.Load<Sprite>(data.AvatarPath);
                    if (sprite != null)
                    {
                        avatarImage.sprite = sprite;
                    }
                }
            }

            ApplyStats(data);

            if (goldText != null) goldText.text = data.Gold.ToString();
            if (bossGemText != null) bossGemText.text = data.BossGem.ToString();
            if (premiumText != null) premiumText.text = data.PremiumCoin.ToString();

            if (activeTabIndex == 1)
            {
                int itemsCount = data.Items.Count;
                int currentSlotsCount = itemSlots.Count;

                // 1. If we have more slots than needed and we are above initial limit, destroy extra rows
                if (currentSlotsCount > initialSlotsCount && currentSlotsCount > itemsCount)
                {
                    int targetCount = Mathf.Max(initialSlotsCount, itemsCount);
                    for (int i = currentSlotsCount - 1; i >= targetCount; i--)
                    {
                        var slot = itemSlots[i];
                        GameObject rowObject = null;
                        if (slot.nameText != null)
                        {
                            rowObject = slot.nameText.transform.parent.gameObject;
                        }
                        else if (slot.iconImage != null)
                        {
                            rowObject = slot.iconImage.transform.parent.parent.gameObject;
                        }

                        if (rowObject != null)
                        {
                            Destroy(rowObject);
                        }
                        itemSlots.RemoveAt(i);
                    }
                }

                // 2. If items exceed current slots, dynamically instantiate more rows
                currentSlotsCount = itemSlots.Count;
                if (itemsCount > currentSlotsCount && currentSlotsCount > 0 && itemListPanel != null)
                {
                    GameObject template = null;
                    if (itemSlots[0].nameText != null)
                    {
                        template = itemSlots[0].nameText.transform.parent.gameObject;
                    }
                    else if (itemSlots[0].iconImage != null)
                    {
                        template = itemSlots[0].iconImage.transform.parent.parent.gameObject;
                    }

                    if (template != null)
                    {
                        for (int i = currentSlotsCount; i < itemsCount; i++)
                        {
                            GameObject newRow = Instantiate(template, template.transform.parent);
                            newRow.name = $"ItemRow {i}";

                            InventoryItemSlot newSlot = new InventoryItemSlot
                            {
                                iconImage = FindComponent<Image>(newRow.transform, "IconCell/Icon"),
                                nameText = FindComponent<Text>(newRow.transform, "ItemName"),
                                statText = FindComponent<Text>(newRow.transform, "ItemStat"),
                                button = newRow.GetComponent<Button>()
                            };
                            itemSlots.Add(newSlot);
                        }
                    }
                }

                // 3. Populate item slots
                for (int i = 0; i < itemSlots.Count; i++)
                {
                    if (i < data.Items.Count)
                    {
                        ApplyItem(i, data.Items[i]);
                    }
                    else
                    {
                        ClearItem(i);
                    }
                }
            }
            else if (activeTabIndex == 2)
            {
                int skillsCount = 5 + data.CombatSkills.Count;
                int currentSlotsCount = itemSlots.Count;

                // 1. If we have more slots than needed, destroy extra rows
                if (currentSlotsCount > initialSlotsCount && currentSlotsCount > skillsCount)
                {
                    int targetCount = Mathf.Max(initialSlotsCount, skillsCount);
                    for (int i = currentSlotsCount - 1; i >= targetCount; i--)
                    {
                        var slot = itemSlots[i];
                        GameObject rowObject = null;
                        if (slot.nameText != null)
                        {
                            rowObject = slot.nameText.transform.parent.gameObject;
                        }
                        else if (slot.iconImage != null)
                        {
                            rowObject = slot.iconImage.transform.parent.parent.gameObject;
                        }

                        if (rowObject != null)
                        {
                            Destroy(rowObject);
                        }
                        itemSlots.RemoveAt(i);
                    }
                }

                // 2. Instantiate more rows if skillsCount exceed current slots
                currentSlotsCount = itemSlots.Count;
                if (skillsCount > currentSlotsCount && currentSlotsCount > 0 && skillPanel != null)
                {
                    GameObject template = null;
                    if (itemSlots[0].nameText != null)
                    {
                        template = itemSlots[0].nameText.transform.parent.gameObject;
                    }
                    else if (itemSlots[0].iconImage != null)
                    {
                        template = itemSlots[0].iconImage.transform.parent.parent.gameObject;
                    }

                    if (template != null)
                    {
                        for (int i = currentSlotsCount; i < skillsCount; i++)
                        {
                            GameObject newRow = Instantiate(template, template.transform.parent);
                            newRow.name = $"ItemRow {i}";

                            InventoryItemSlot newSlot = new InventoryItemSlot
                            {
                                iconImage = FindComponent<Image>(newRow.transform, "IconCell/Icon"),
                                nameText = FindComponent<Text>(newRow.transform, "ItemName"),
                                statText = FindComponent<Text>(newRow.transform, "ItemStat"),
                                button = newRow.GetComponent<Button>()
                            };
                            itemSlots.Add(newSlot);
                        }
                    }
                }

                // 3. Populate skills slots (5 stats + combat skills)
                Color32 hpColor = new Color32(46, 204, 113, 255); // Green
                Color32 kiColor = new Color32(52, 152, 219, 255); // Blue
                Color32 atkColor = new Color32(231, 76, 60, 255); // Red
                Color32 defColor = new Color32(149, 165, 166, 255); // Grey
                Color32 critColor = new Color32(241, 196, 15, 255); // Yellow

                int hpCost = data.BaseHP * 10;
                ApplySkillSlot(0, $"HP gốc: {data.BaseHP}", $"{hpCost:N0} tiềm năng: tăng 20", "res/x4/mainimage/myTexture2dHP", hpColor);

                int kiCost = data.BaseKi * 10;
                ApplySkillSlot(1, $"KI gốc: {data.BaseKi}", $"{kiCost:N0} tiềm năng: tăng 20", "res/x4/mainimage/myTexture2dMP", kiColor);

                int atkCost = data.BaseAtk * 100;
                ApplySkillSlot(2, $"Sức đánh gốc: {data.BaseAtk}", $"{atkCost:N0} tiềm năng: tăng 1", "UI/Currency/gem_green", atkColor);

                int defCost = (data.BaseDef + 1) * 500000;
                ApplySkillSlot(3, $"Giáp gốc: {data.BaseDef}", $"{defCost:N0} tiềm năng: tăng 1", "UI/Currency/coin_stack", defColor);

                int critCost = (data.BaseCritPercent + 1) * 50000000;
                ApplySkillSlot(4, $"Chí mạng gốc: {data.BaseCritPercent}%", $"{critCost:N0} tiềm năng: tăng 1%", "UI/Currency/gem_green", critColor);

                for (int i = 5; i < itemSlots.Count; i++)
                {
                    int skillIdx = i - 5;
                    if (skillIdx < data.CombatSkills.Count)
                    {
                        var skill = data.CombatSkills[skillIdx];
                        string statText = skill.SkillLevel == 0 ? "Chưa học (Bấm để học)" : $"Cấp {skill.SkillLevel}";
                        string skillIconPath = string.IsNullOrEmpty(skill.IconKey) ? Config.Resources.hudKiSpritePath : skill.IconKey;
                        ApplySkillSlot(i, skill.Name, statText, skillIconPath, new Color32(155, 89, 182, 255));
                    }
                    else
                    {
                        ClearItem(i);
                    }
                }
            }
            else
            {
                for (int i = 0; i < itemSlots.Count; i++)
                {
                    ClearItem(i);
                }
            }

            // Bind click events to buttons
            for (int i = 0; i < itemSlots.Count; i++)
            {
                int index = i;
                if (itemSlots[i].button != null && itemSlots[i].button)
                {
                    itemSlots[i].button.onClick.RemoveAllListeners();
                    itemSlots[i].button.onClick.AddListener(() => OnRowClicked(index));
                }
            }
        }

        private void ApplyStats(InventoryViewData data)
        {
            if (data == null)
            {
                return;
            }

            int totalHP = data.BaseHP;
            int totalKi = data.BaseKi;
            int totalAtk = data.BaseAtk;
            int totalDefense = data.BaseDef;
            int totalCrit = data.BaseCritPercent;
            int totalDamageReduction = data.BaseDamageReductionPercent;
            int totalCritDamage = data.BaseCritDamagePercent;
            float totalSpeed = data.BaseSpd;

            foreach (InventoryItemViewData item in data.Items)
            {
                totalHP += item.BonusHP;
                totalKi += item.BonusKi;
                totalAtk += item.BonusAtk;
                totalDefense += item.BonusDef;
                totalDamageReduction += item.BonusDamageReductionPercent;
                totalCrit += item.BonusCritPercent;
                totalCritDamage += item.BonusCritDamagePercent;
                totalSpeed += item.BonusSpd;
            }

            int currentHP = data.CurrentHP;
            int currentKi = data.CurrentKi;
            if (Application.isPlaying)
            {
                PlayerHealth playerHealth = ObjectLookup.Any<PlayerHealth>();
                if (playerHealth != null)
                {
                    currentHP = playerHealth.CurrentHealth;
                    totalHP = playerHealth.MaxHealth;
                }

                PlayerAttack playerAttack = ObjectLookup.Any<PlayerAttack>();
                if (playerAttack != null)
                {
                    currentKi = Mathf.RoundToInt(playerAttack.CurrentMana);
                    totalKi = Mathf.RoundToInt(playerAttack.MaxMana);
                }
            }

            string itemStatsContent =
                $"HP: {Mathf.Min(currentHP, totalHP)} / {totalHP}\n" +
                $"KI: {Mathf.Min(currentKi, totalKi)} / {totalKi}\n" +
                $"Sức đánh: {totalAtk}  Crit: {totalCrit}%\n" +
                $"Giáp: {totalDefense}  Giảm ST: {totalDamageReduction}%";

            if (itemStatsText != null)
            {
                itemStatsText.text = itemStatsContent;
            }

            if (skillStatsText != null)
            {
                skillStatsText.text =
                    $"Top: 0\n" +
                    $"Điểm tiềm năng: {data.Exp:N0}\n" +
                    $"Năng động: 0";
            }

            if (questStatsText != null)
            {
                questStatsText.text = "Nhiệm vụ: Chưa có nhiệm vụ hoạt động";
            }

            if (featureStatsText != null)
            {
                featureStatsText.text = "Chức năng hệ thống";
            }
        }

        private void ApplyItem(int index, InventoryItemViewData item)
        {
            if (index >= itemSlots.Count) return;

            var slot = itemSlots[index];
            if (slot.iconImage != null && slot.iconImage)
            {
                slot.iconImage.enabled = true;
                if (!string.IsNullOrEmpty(item.SpritePath))
                {
                    Sprite sprite = ResourceLoader.Load<Sprite>(item.SpritePath);
                    if (sprite != null)
                    {
                        slot.iconImage.sprite = sprite;
                        slot.iconImage.color = Color.white;
                    }
                    else
                    {
                        slot.iconImage.sprite = null;
                        slot.iconImage.color = InventoryPanelFormatting.GetIconColor(item);
                    }
                }
                else
                {
                    slot.iconImage.sprite = null;
                    slot.iconImage.color = InventoryPanelFormatting.GetIconColor(item);
                }
            }

            if (slot.nameText != null && slot.nameText)
            {
                slot.nameText.text = item.Name;
            }

            if (slot.statText != null && slot.statText)
            {
                slot.statText.text = InventoryPanelFormatting.BuildItemStat(item);
            }
        }

        private void ClearItem(int index)
        {
            if (index >= itemSlots.Count) return;

            var slot = itemSlots[index];
            if (slot.iconImage != null && slot.iconImage) slot.iconImage.enabled = false;
            if (slot.nameText != null && slot.nameText) slot.nameText.text = string.Empty;
            if (slot.statText != null && slot.statText) slot.statText.text = string.Empty;
        }

        private void ApplySkillSlot(int index, string name, string statDesc, string spritePath, Color32 fallbackColor)
        {
            if (index >= itemSlots.Count) return;

            var slot = itemSlots[index];
            if (slot.iconImage != null && slot.iconImage)
            {
                slot.iconImage.enabled = true;
                if (!string.IsNullOrEmpty(spritePath))
                {
                    Sprite sprite = ResourceLoader.Load<Sprite>(spritePath);
                    if (sprite != null)
                    {
                        slot.iconImage.sprite = sprite;
                        slot.iconImage.color = Color.white;
                    }
                    else
                    {
                        slot.iconImage.sprite = null;
                        slot.iconImage.color = fallbackColor;
                    }
                }
                else
                {
                    slot.iconImage.sprite = null;
                    slot.iconImage.color = fallbackColor;
                }
            }

            if (slot.nameText != null && slot.nameText)
            {
                slot.nameText.text = name;
            }

            if (slot.statText != null && slot.statText)
            {
                slot.statText.text = statDesc;
            }
        }

        private void OnRowClicked(int index)
        {
            if (activeTabIndex == 1)
            {
                selectedItemIndex = index;
                if (index < currentInventoryData.Items.Count)
                {
                    var item = currentInventoryData.Items[index];
                    if (itemStatsText != null)
                    {
                        itemStatsText.text = $"{item.Name}\n" +
                                             $"{InventoryPanelFormatting.BuildItemStatsDescription(item)}\n" +
                                             $"Số lượng: {item.Quantity}\n" +
                                             $"Cấp nâng cấp: {item.UpgradeLevel}";
                    }
                }
            }
            else if (activeTabIndex == 2)
            {
                if (selectedSkillIndex == index)
                {
                    PerformSkillUpgrade(index);
                }
                else
                {
                    selectedSkillIndex = index;
                    ShowSkillDetails(index);
                }
            }
        }

        private void ShowSkillDetails(int index)
        {
            if (currentInventoryData == null || skillStatsText == null) return;

            skillStatsText.text = InventoryPanelFormatting.BuildSkillDetails(index, currentInventoryData);
        }

        private void PerformSkillUpgrade(int index)
        {
            if (currentInventoryData == null) return;

            string statType = InventoryPanelFormatting.GetUpgradeableBaseStat(index);

            if (statType != null)
            {
                bool success = TinyDragonSaveManager.Instance.UpgradePlayerStat(statType);
                if (success)
                {
                    Refresh();
                    ShowSkillDetails(index);
                }
                else
                {
                    if (skillStatsText != null)
                    {
                        skillStatsText.text += "\n<color=red>Thất bại: Không đủ tiềm năng!</color>";
                    }
                }
            }
            else if (index >= 5)
            {
                if (skillStatsText != null)
                {
                    skillStatsText.text += "\n<color=orange>Tính năng nâng cấp chiêu thức này đang phát triển!</color>";
                }
            }
        }

        private T FindComponent<T>(Transform root, string path) where T : Component
        {
            Transform child = root.Find(path);
            return child != null ? child.GetComponent<T>() : null;
        }

        private void CleanupNullSlots()
        {
            itemSlots.RemoveAll(slot => slot.iconImage == null && slot.nameText == null && slot.statText == null);
            tabButtons.RemoveAll(btn => btn == null);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CleanupNullSlots();

            if (canvas == null) canvas = GetComponent<Canvas>();
            if (panel == null)
            {
                Transform p = transform.Find("Panel");
                if (p != null) panel = p.gameObject;
            }

            if (panel != null)
            {
                if (nameText == null) nameText = FindComponent<Text>(panel.transform, "Header/Name");
                if (questStatsText == null) questStatsText = FindComponent<Text>(panel.transform, "Header/Stats/QuestStats") ?? FindComponent<Text>(panel.transform, "Header/Stats/queststats");
                if (itemStatsText == null) itemStatsText = FindComponent<Text>(panel.transform, "Header/Stats/ItemStats") ?? FindComponent<Text>(panel.transform, "Header/Stats/itemstats");
                if (skillStatsText == null) skillStatsText = FindComponent<Text>(panel.transform, "Header/Stats/SkillStats") ?? FindComponent<Text>(panel.transform, "Header/Stats/skillstats");
                if (featureStatsText == null) featureStatsText = FindComponent<Text>(panel.transform, "Header/Stats/FeatureStats") ?? FindComponent<Text>(panel.transform, "Header/Stats/featurestats");
                if (goldText == null) goldText = FindComponent<Text>(panel.transform, "CurrencyBar/Gold");
                if (bossGemText == null) bossGemText = FindComponent<Text>(panel.transform, "CurrencyBar/BossGem");
                if (premiumText == null) premiumText = FindComponent<Text>(panel.transform, "CurrencyBar/Premium");
                if (closeButton == null) closeButton = FindComponent<Button>(panel.transform, "Header/Close");
                if (avatarImage == null)
                {
                    avatarImage = FindComponent<Image>(panel.transform, "Header/AvatarFrame/Avatar");
                    if (avatarImage == null) avatarImage = FindComponent<Image>(panel.transform, "Header/Avatar");
                    if (avatarImage == null) avatarImage = FindComponent<Image>(panel.transform, "AvatarFrame/Avatar");
                    if (avatarImage == null) avatarImage = FindComponent<Image>(panel.transform, "Avatar");
                }

                if (itemListPanel == null)
                {
                    Transform t = panel.transform.Find("ItemList");
                    if (t == null) t = panel.transform.Find("Scroll View/Viewport/Content");
                    if (t != null) itemListPanel = t.gameObject;
                }
                if (questPanel == null)
                {
                    Transform t = panel.transform.Find("QuestList");
                    if (t == null) t = panel.transform.Find("Quest");
                    if (t != null) questPanel = t.gameObject;
                }
                if (skillPanel == null)
                {
                    Transform t = panel.transform.Find("SkillList");
                    if (t == null) t = panel.transform.Find("Skill");
                    if (t != null) skillPanel = t.gameObject;
                }
                if (featurePanel == null)
                {
                    Transform t = panel.transform.Find("FeatureList");
                    if (t == null) t = panel.transform.Find("Feature");
                    if (t != null) featurePanel = t.gameObject;
                }
                if (downArrowButton == null)
                {
                    Transform t = panel.transform.Find("ItemList/DownArrow");
                    if (t == null) t = panel.transform.Find("DownArrow");
                    if (t != null) downArrowButton = t.GetComponent<Button>();
                }

                // Auto-locate Tabs
                if (tabButtons.Count == 0)
                {
                    tabButtons.Clear();
                    for (int i = 0; i < 4; i++)
                    {
                        Transform tab = panel.transform.Find($"Tab {i}");
                        if (tab != null)
                        {
                            Button btn = tab.GetComponent<Button>();
                            if (btn != null) tabButtons.Add(btn);
                        }
                    }
                }

                // Find all item rows in the item list panel
                if (itemListPanel != null)
                {
                    Transform container = itemListPanel.transform.Find("Viewport/Content");
                    if (container == null) container = itemListPanel.transform;

                    itemSlots.Clear();
                    foreach (Transform child in container)
                    {
                        if (child.name.StartsWith("ItemRow"))
                        {
                            InventoryItemSlot slot = new InventoryItemSlot
                            {
                                iconImage = FindComponent<Image>(child, "IconCell/Icon"),
                                nameText = FindComponent<Text>(child, "ItemName"),
                                statText = FindComponent<Text>(child, "ItemStat")
                            };
                            itemSlots.Add(slot);
                        }
                    }
                }
            }
        }
#endif
    }
}
