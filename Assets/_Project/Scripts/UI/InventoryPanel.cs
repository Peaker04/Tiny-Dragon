using System.Collections.Generic;
using System.Collections;
using TinyDragon.Audio;
using TinyDragon.Data;
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
        private const int CarriedSlotCount = 10;
        private const int StoredMinimumSlotCount = 10;
        private const int StatUpgradeSlotCount = 5;
        private static readonly Color32 CarriedRowColor = new Color32(232, 224, 210, 255);
        private static readonly Color32 StoredRowColor = new Color32(246, 242, 233, 118);
        private static readonly Color32 CarriedIconCellColor = new Color32(166, 123, 67, 255);
        private static readonly Color32 StoredIconCellColor = new Color32(183, 145, 92, 178);
        private static readonly Color32 SelectedRowColor = new Color32(255, 248, 42, 255);

        private static InventoryPanel instance;
        public static bool IsVisible => instance != null && instance.panel != null && instance.panel.activeSelf;

        public static void HideIfVisible()
        {
            if (IsVisible) instance.SetVisible(false);
        }

        public static void RefreshIfVisible()
        {
            if (IsVisible) instance.Refresh();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            instance = null;
        }

        [Header("UI Elements")]
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
        private int activeTabIndex = 1;
        private int initialSlotsCount;
        private int selectedItemIndex = -1;
        private int selectedSkillIndex = -1;
        private InventoryItemViewData pendingTransferItem;
        private bool pendingTransferToCarried;
        private GameObject itemTransferDialog;
        private Text itemTransferMessageText;
        private Button itemTransferConfirmButton;
        private Button itemTransferCancelButton;
        private InventorySkillViewData pendingSkill;
        private InventoryStatUpgradeViewData pendingStatUpgrade;
        private GameObject skillLearnDialog;
        private Text skillLearnMessageText;
        private Button skillLearnConfirmButton;
        private Button skillLearnCancelButton;
        private GameObject alertDialog;
        private Text alertMessageText;
        private Button alertOkButton;

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
            if (Application.isPlaying) SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            if (Application.isPlaying) SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            PlayerInputReader inputReader = ObjectLookup.Any<PlayerInputReader>();
            bool inventoryPressed = inputReader != null
                ? inputReader.ConsumeInventoryPressed()
                : Input.GetKeyDown(KeyCode.B);
            
            if (panel != null && panel.activeSelf && !ShouldInventoryBeOpenable())
            {
                SetVisible(false);
            }

            if (inventoryPressed)
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
                ScrollRect scrollRect = GetScrollRect(activePanel);
                if (scrollRect != null)
                {
                    StopAllCoroutines();
                    StartCoroutine(SmoothScrollTo(scrollRect, Mathf.Max(0f, scrollRect.verticalNormalizedPosition - 0.35f)));
                }
            }
        }

        private ScrollRect GetScrollRect(GameObject panelObject)
        {
            if (panelObject == null)
            {
                return null;
            }

            ScrollRect scrollRect = panelObject.GetComponent<ScrollRect>();
            return scrollRect != null ? scrollRect : panelObject.GetComponentInChildren<ScrollRect>(true);
        }

        private void ConfigureSmoothScroll(GameObject panelObject)
        {
            ScrollRect scrollRect = GetScrollRect(panelObject);
            if (scrollRect == null)
            {
                return;
            }

            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.08f;
            scrollRect.scrollSensitivity = 24f;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.08f;
        }

        private IEnumerator SmoothScrollTo(ScrollRect scrollRect, float target)
        {
            if (scrollRect == null)
            {
                yield break;
            }

            float start = scrollRect.verticalNormalizedPosition;
            float elapsed = 0f;
            const float duration = 0.18f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 3f);
                scrollRect.verticalNormalizedPosition = Mathf.Lerp(start, target, t);
                yield return null;
            }

            scrollRect.verticalNormalizedPosition = target;
        }

        private void UpdateTabVisuals()
        {
            Color32 activeTabColor = new Color32(151, 238, 159, 255);
            Color32 inactiveTabColor = new Color32(255, 238, 205, 255);
            Color32 activeTabTextColor = new Color32(24, 91, 43, 255);
            Color32 inactiveTabTextColor = new Color32(91, 74, 58, 255);

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
            ConfigureSmoothScroll(questPanel);
            ConfigureSmoothScroll(itemListPanel);
            ConfigureSmoothScroll(skillPanel);
            ConfigureSmoothScroll(featurePanel);

            if (questStatsText != null) questStatsText.gameObject.SetActive(activeTabIndex == 0);
            if (itemStatsText != null) itemStatsText.gameObject.SetActive(activeTabIndex == 1);
            if (skillStatsText != null) skillStatsText.gameObject.SetActive(activeTabIndex == 2);
            if (featureStatsText != null) featureStatsText.gameObject.SetActive(activeTabIndex == 3);
        }

        private void SetVisible(bool visible)
        {
            bool wasVisible = panel != null && panel.activeSelf;
            if (panel != null)
            {
                panel.SetActive(visible);
            }

            if (visible && !wasVisible && Application.isPlaying)
            {
                UiSoundPlayer.PlayInventoryOpen();
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

        private bool ShouldInventoryBeOpenable()
        {
            if (SceneManager.GetActiveScene().name == "Level_01_Original")
            {
                return false;
            }

            return ObjectLookup.InactiveAny<PlayerHealth>() != null;
        }

        private void Refresh()
        {
            if (TinyDragonSaveManager.Instance == null) return;

            GameObject activePanel = null;
            if (activeTabIndex == 1) activePanel = itemListPanel;
            else if (activeTabIndex == 2) activePanel = skillPanel;

            if (activePanel != null)
            {
                Transform container = activePanel.transform.Find("Viewport/Content");
                if (container == null) container = activePanel.transform;

                itemSlots.Clear();
                
                List<Transform> children = new List<Transform>();
                foreach (Transform child in container)
                {
                    children.Add(child);
                }
                
                children.Sort((a, b) => b.localPosition.y.CompareTo(a.localPosition.y));

                foreach (Transform child in children)
                {
                    var texts = child.GetComponentsInChildren<Text>(true);
                    var images = child.GetComponentsInChildren<Image>(true);
                    
                    if (texts.Length > 0 || images.Length > 0)
                    {
                        InventoryItemSlot slot = new InventoryItemSlot();
                        
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
                    Sprite sprite = Resources.Load<Sprite>(data.AvatarPath);
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
                RefreshInventoryItems(data);
            }
            else if (activeTabIndex == 2)
            {
                int skillsCount = StatUpgradeSlotCount + data.CombatSkills.Count;
                int currentSlotsCount = itemSlots.Count;

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

                Color32[] statColors =
                {
                    new Color32(46, 204, 113, 255),
                    new Color32(52, 152, 219, 255),
                    new Color32(231, 76, 60, 255),
                    new Color32(149, 165, 166, 255),
                    new Color32(241, 196, 15, 255)
                };

                for (int i = 0; i < StatUpgradeSlotCount; i++)
                {
                    InventoryStatUpgradeViewData upgrade = GetStatUpgrade(i);
                    if (upgrade == null)
                    {
                        ClearItem(i);
                        continue;
                    }

                    ApplySkillSlot(
                        i,
                        $"{upgrade.Name}: {GetStatUpgradeCurrentValue(upgrade)}",
                        $"{GetStatUpgradeCost(upgrade):N0} tiềm năng: tăng {upgrade.EffectValue}",
                        string.IsNullOrEmpty(upgrade.IconKey) ? "UI/Skills/stat_hp_root" : upgrade.IconKey,
                        statColors[Mathf.Min(i, statColors.Length - 1)]
                    );
                }

                for (int i = StatUpgradeSlotCount; i < itemSlots.Count; i++)
                {
                    int skillIdx = i - StatUpgradeSlotCount;
                    if (skillIdx < data.CombatSkills.Count)
                    {
                        var skill = data.CombatSkills[skillIdx];
                        string statText = skill.SkillLevel == 0 ? "Chưa học (Bấm để học)" : $"Cấp {skill.SkillLevel}";
                        string skillIconPath = string.IsNullOrEmpty(skill.IconKey) ? "res/x4/mainimage/myTexture2dMP" : skill.IconKey;
                        ApplySkillSlot(i, skill.Name, statText, skillIconPath, new Color32(155, 89, 182, 255));
                    }
                    else
                    {
                        ClearItem(i);
                        SetItemSlotVisual(i, false, false);
                    }
                }
            }
            else
            {
                for (int i = 0; i < itemSlots.Count; i++)
                {
                    ClearItem(i);
                    SetItemSlotVisual(i, false, false);
                }
            }

            for (int i = 0; i < itemSlots.Count; i++)
            {
                int index = i;
                Button rowButton = EnsureSlotButton(i);
                if (rowButton != null)
                {
                    if (activeTabIndex != 1)
                    {
                        rowButton.interactable = true;
                    }
                    rowButton.onClick.RemoveAllListeners();
                    rowButton.onClick.AddListener(() => OnRowClicked(index));
                }
            }
        }

        private void RefreshInventoryItems(InventoryViewData data)
        {
            List<InventoryItemViewData> carriedItems = GetInventoryItems(data, true);
            List<InventoryItemViewData> storedItems = GetInventoryItems(data, false);
            int storedSlotCount = Mathf.Max(StoredMinimumSlotCount, storedItems.Count);
            int requiredSlotCount = CarriedSlotCount + storedSlotCount;

            EnsureItemSlotCount(requiredSlotCount);

            for (int i = 0; i < itemSlots.Count; i++)
            {
                GameObject rowObject = GetSlotRowObject(itemSlots[i]);
                bool shouldShowRow = i < requiredSlotCount;
                if (rowObject != null)
                {
                    rowObject.SetActive(shouldShowRow);
                    rowObject.name = i < CarriedSlotCount ? $"CarriedItemRow {i}" : $"StoredItemRow {i - CarriedSlotCount}";
                }

                if (!shouldShowRow)
                {
                    ClearItem(i);
                    continue;
                }

                bool isStoredSlot = i >= CarriedSlotCount;
                InventoryItemViewData item = null;
                if (!isStoredSlot)
                {
                    if (i < carriedItems.Count) item = carriedItems[i];
                }
                else
                {
                    int storedIndex = i - CarriedSlotCount;
                    if (storedIndex < storedItems.Count) item = storedItems[storedIndex];
                }

                if (item != null)
                {
                    ApplyItem(i, item, isStoredSlot);
                }
                else
                {
                    ClearItem(i);
                    SetItemSlotVisual(i, isStoredSlot, false);
                }

                Button rowButton = EnsureSlotButton(i);
                if (rowButton != null)
                {
                    rowButton.interactable = true;
                }
            }
        }

        private List<InventoryItemViewData> GetInventoryItems(InventoryViewData data, bool isCarried)
        {
            List<InventoryItemViewData> result = new List<InventoryItemViewData>();
            if (data == null)
            {
                return result;
            }

            foreach (InventoryItemViewData item in data.Items)
            {
                if (item.IsCarried == isCarried)
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private InventoryItemViewData GetVisibleInventoryItem(int index, out bool isCarried)
        {
            isCarried = index < CarriedSlotCount;
            if (currentInventoryData == null || index < 0)
            {
                return null;
            }

            List<InventoryItemViewData> items = GetInventoryItems(currentInventoryData, isCarried);
            int itemIndex = isCarried ? index : index - CarriedSlotCount;
            return itemIndex >= 0 && itemIndex < items.Count ? items[itemIndex] : null;
        }

        private void EnsureItemSlotCount(int requiredSlotCount)
        {
            if (itemSlots.Count == 0 || itemListPanel == null)
            {
                return;
            }

            GameObject template = GetSlotRowObject(itemSlots[0]);
            if (template == null)
            {
                return;
            }

            Transform parent = template.transform.parent;
            while (itemSlots.Count < requiredSlotCount)
            {
                GameObject newRow = Instantiate(template, parent);
                newRow.name = $"StoredItemRow {itemSlots.Count - CarriedSlotCount}";
                newRow.SetActive(true);
                itemSlots.Add(CreateSlotFromRow(newRow));
            }
        }

        private InventoryItemSlot CreateSlotFromRow(GameObject rowObject)
        {
            return new InventoryItemSlot
            {
                iconImage = FindComponent<Image>(rowObject.transform, "IconCell/Icon"),
                nameText = FindComponent<Text>(rowObject.transform, "ItemName"),
                statText = FindComponent<Text>(rowObject.transform, "ItemStat"),
                button = rowObject.GetComponent<Button>()
            };
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
                if (!ShouldApplyItemStats(item))
                {
                    continue;
                }

                totalHP += item.BonusHP;
                totalKi += item.BonusKi;
                totalAtk += item.BonusAtk;
                totalDefense += item.BonusDef;
                totalDamageReduction += item.BonusDamageReductionPercent;
                totalCrit += item.BonusCritPercent;
                totalCritDamage += item.BonusCritDamagePercent;
                totalSpeed += item.BonusSpd;
            }
            foreach (InventorySkillViewData skill in data.CombatSkills)
            {
                if (skill.SkillLevel > 0)
                {
                    totalAtk += Mathf.Max(skill.EffectValue, 0) * skill.SkillLevel;
                }
            }

            int currentHP = data.CurrentHP;
            int currentKi = data.CurrentKi;
            if (Application.isPlaying)
            {
                PlayerHealth playerHealth = ObjectLookup.Any<PlayerHealth>();
                if (playerHealth != null)
                {
                    currentHP = playerHealth.CurrentHealth;
                }

                PlayerAttack playerAttack = ObjectLookup.Any<PlayerAttack>();
                if (playerAttack != null)
                {
                    currentKi = Mathf.RoundToInt(playerAttack.CurrentMana);
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

        private bool ShouldApplyItemStats(InventoryItemViewData item)
        {
            return item != null
                && item.IsCarried
                && !string.IsNullOrWhiteSpace(item.SlotType)
                && item.ItemType != "CONSUMABLE";
        }

        private void ApplyItem(int index, InventoryItemViewData item, bool isStoredSlot = false)
        {
            if (index >= itemSlots.Count) return;

            var slot = itemSlots[index];
            if (slot.iconImage != null && slot.iconImage)
            {
                slot.iconImage.enabled = true;
                if (!string.IsNullOrEmpty(item.SpritePath))
                {
                    Sprite sprite = LoadSpriteResource(item.SpritePath);
                    if (sprite != null)
                    {
                        slot.iconImage.sprite = sprite;
                        slot.iconImage.color = Color.white;
                    }
                    else
                    {
                        slot.iconImage.sprite = null;
                        slot.iconImage.color = GetIconColor(item);
                    }
                }
                else
                {
                    slot.iconImage.sprite = null;
                    slot.iconImage.color = GetIconColor(item);
                }
            }

            if (slot.nameText != null && slot.nameText)
            {
                slot.nameText.text = item.Name;
            }

            if (slot.statText != null && slot.statText)
            {
                slot.statText.text = BuildItemStat(item);
            }

            Text quantityText = EnsureQuantityText(slot);
            if (quantityText != null)
            {
                quantityText.text = item.Quantity > 1 ? item.Quantity.ToString() : string.Empty;
                quantityText.gameObject.SetActive(item.Quantity > 1);
            }

            SetItemSlotVisual(index, isStoredSlot, true);
        }

        private void ClearItem(int index)
        {
            if (index >= itemSlots.Count) return;

            var slot = itemSlots[index];
            if (slot.iconImage != null && slot.iconImage) slot.iconImage.enabled = false;
            if (slot.nameText != null && slot.nameText) slot.nameText.text = string.Empty;
            if (slot.statText != null && slot.statText) slot.statText.text = string.Empty;
            Text quantityText = GetQuantityText(slot);
            if (quantityText != null)
            {
                quantityText.text = string.Empty;
                quantityText.gameObject.SetActive(false);
            }
        }

        private void SetItemSlotVisual(int index, bool isStoredSlot, bool hasItem)
        {
            if (index >= itemSlots.Count)
            {
                return;
            }

            var slot = itemSlots[index];
            GameObject rowObject = GetSlotRowObject(slot);
            Image rowImage = rowObject != null ? rowObject.GetComponent<Image>() : null;
            if (rowImage != null)
            {
                rowImage.color = IsSlotSelected(index)
                    ? SelectedRowColor
                    : isStoredSlot ? StoredRowColor : CarriedRowColor;
            }

            Image iconCellImage = GetIconCellImage(slot);
            if (iconCellImage != null)
            {
                iconCellImage.color = isStoredSlot ? StoredIconCellColor : CarriedIconCellColor;
            }

            float contentAlpha = isStoredSlot && hasItem && !IsSlotSelected(index) ? 0.32f : 1f;
            SetGraphicAlpha(slot.iconImage, contentAlpha);
            SetGraphicAlpha(slot.nameText, isStoredSlot && !IsSlotSelected(index) ? 0.48f : 1f);
            SetGraphicAlpha(slot.statText, isStoredSlot && !IsSlotSelected(index) ? 0.48f : 1f);
            SetGraphicAlpha(GetQuantityText(slot), isStoredSlot && !IsSlotSelected(index) ? 0.55f : 1f);
        }

        private bool IsSlotSelected(int index)
        {
            if (activeTabIndex == 1)
            {
                return selectedItemIndex == index;
            }

            if (activeTabIndex == 2)
            {
                return selectedSkillIndex == index;
            }

            return false;
        }

        private void RefreshSelectedRowVisuals()
        {
            for (int i = 0; i < itemSlots.Count; i++)
            {
                bool hasContent = false;
                if (activeTabIndex == 1)
                {
                    hasContent = GetVisibleInventoryItem(i, out _) != null;
                    SetItemSlotVisual(i, i >= CarriedSlotCount, hasContent);
                }
                else if (activeTabIndex == 2)
                {
                    hasContent = i < StatUpgradeSlotCount
                        ? GetStatUpgrade(i) != null
                        : currentInventoryData != null && i - StatUpgradeSlotCount < currentInventoryData.CombatSkills.Count;
                    SetItemSlotVisual(i, false, hasContent);
                }
            }
        }

        private void SetGraphicAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
            {
                return;
            }

            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        private GameObject GetSlotRowObject(InventoryItemSlot slot)
        {
            if (slot.nameText != null)
            {
                return slot.nameText.transform.parent.gameObject;
            }

            if (slot.iconImage != null && slot.iconImage.transform.parent != null)
            {
                Transform row = slot.iconImage.transform.parent.parent;
                if (row != null)
                {
                    return row.gameObject;
                }
            }

            if (slot.button != null)
            {
                return slot.button.gameObject;
            }

            return null;
        }

        private Image GetIconCellImage(InventoryItemSlot slot)
        {
            if (slot.iconImage != null && slot.iconImage.transform.parent != null)
            {
                return slot.iconImage.transform.parent.GetComponent<Image>();
            }

            if (slot.nameText != null)
            {
                Transform iconCell = slot.nameText.transform.parent.Find("IconCell");
                if (iconCell != null)
                {
                    return iconCell.GetComponent<Image>();
                }
            }

            return null;
        }

        private Text GetQuantityText(InventoryItemSlot slot)
        {
            Image iconCellImage = GetIconCellImage(slot);
            if (iconCellImage == null)
            {
                return null;
            }

            Transform quantity = iconCellImage.transform.Find("QuantityText");
            return quantity != null ? quantity.GetComponent<Text>() : null;
        }

        private Text EnsureQuantityText(InventoryItemSlot slot)
        {
            Image iconCellImage = GetIconCellImage(slot);
            if (iconCellImage == null)
            {
                return null;
            }

            Text quantityText = GetQuantityText(slot);
            if (quantityText != null)
            {
                return quantityText;
            }

            GameObject quantityObject = new GameObject("QuantityText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            quantityObject.transform.SetParent(iconCellImage.transform, false);
            RectTransform rect = quantityObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-3f, 2f);
            rect.sizeDelta = new Vector2(28f, 18f);

            quantityText = quantityObject.GetComponent<Text>();
            quantityText.font = GetDialogFont();
            quantityText.fontSize = 12;
            quantityText.fontStyle = FontStyle.Bold;
            quantityText.alignment = TextAnchor.LowerRight;
            quantityText.color = Color.white;
            quantityText.raycastTarget = false;

            Outline outline = quantityObject.AddComponent<Outline>();
            outline.effectColor = new Color32(42, 24, 10, 255);
            outline.effectDistance = new Vector2(1f, -1f);
            return quantityText;
        }

        private Button EnsureSlotButton(int index)
        {
            if (index < 0 || index >= itemSlots.Count)
            {
                return null;
            }

            InventoryItemSlot slot = itemSlots[index];
            if (slot.button != null)
            {
                return slot.button;
            }

            GameObject rowObject = GetSlotRowObject(slot);
            if (rowObject == null)
            {
                return null;
            }

            Button button = rowObject.GetComponent<Button>();
            if (button == null)
            {
                button = rowObject.AddComponent<Button>();
            }

            Image rowImage = rowObject.GetComponent<Image>();
            if (rowImage != null && button.targetGraphic == null)
            {
                button.targetGraphic = rowImage;
            }

            slot.button = button;
            itemSlots[index] = slot;
            return button;
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
                    Sprite sprite = LoadSpriteResource(spritePath);
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

            SetItemSlotVisual(index, false, true);
            if (slot.button != null)
            {
                slot.button.interactable = true;
            }
        }

        private Sprite LoadSpriteResource(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return null;
            }

            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                return sprite;
            }

            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            if (sprites == null || sprites.Length == 0)
            {
                return null;
            }

            string expectedName = resourcePath;
            int slashIndex = expectedName.LastIndexOf('/');
            if (slashIndex >= 0)
            {
                expectedName = expectedName.Substring(slashIndex + 1);
            }

            Sprite largestSprite = sprites[0];
            foreach (Sprite candidate in sprites)
            {
                if (candidate.name == expectedName || candidate.name == expectedName + "_0")
                {
                    return candidate;
                }

                if (candidate.rect.width * candidate.rect.height > largestSprite.rect.width * largestSprite.rect.height)
                {
                    largestSprite = candidate;
                }
            }

            return largestSprite;
        }

        private void OnRowClicked(int index)
        {
            if (activeTabIndex == 1)
            {
                selectedItemIndex = index;
                RefreshSelectedRowVisuals();
                InventoryItemViewData item = GetVisibleInventoryItem(index, out bool isCarried);
                if (item == null)
                {
                    return;
                }

                ShowItemTransferDialog(item, !isCarried);
            }
            else if (activeTabIndex == 2)
            {
                selectedSkillIndex = index;
                RefreshSelectedRowVisuals();
                ShowSkillDetails(index);

                if (index >= StatUpgradeSlotCount)
                {
                    ShowSkillLearnDialog(index);
                }
                else
                {
                    ShowStatUpgradeDialog(index);
                }
            }
        }

        private void ShowItemTransferDialog(InventoryItemViewData item, bool toCarried)
        {
            if (item == null)
            {
                return;
            }

            EnsureItemTransferDialog();
            pendingTransferItem = item;
            pendingTransferToCarried = toCarried;

            if (itemTransferMessageText != null)
            {
                string action = toCarried ? "Mang vật phẩm này lên người?" : "Cất vật phẩm này xuống túi?";
                string stateText = toCarried ? "Chỉ số sẽ được cộng ngay." : "Chỉ số đang cộng sẽ bị trừ.";
                itemTransferMessageText.text =
                    $"<b>{item.Name}</b>\n" +
                    $"<color=#117A3A>{GetItemStatsDescription(item)}</color>\n" +
                    $"<color=#7B6044>SL {item.Quantity}  |  Cấp {item.UpgradeLevel}</color>\n\n" +
                    $"{stateText}\n{action}";
            }

            if (itemTransferConfirmButton != null)
            {
                itemTransferConfirmButton.onClick.RemoveAllListeners();
                itemTransferConfirmButton.onClick.AddListener(ConfirmItemTransfer);
            }

            if (itemTransferCancelButton != null)
            {
                itemTransferCancelButton.onClick.RemoveAllListeners();
                itemTransferCancelButton.onClick.AddListener(HideItemTransferDialog);
            }

            if (itemTransferDialog != null)
            {
                itemTransferDialog.SetActive(true);
                itemTransferDialog.transform.SetAsLastSibling();
            }
        }

        private void ConfirmItemTransfer()
        {
            if (pendingTransferItem == null)
            {
                HideItemTransferDialog();
                return;
            }

            bool success = TinyDragonSaveManager.Instance.SetPlayerItemCarried(
                pendingTransferItem.PlayerItemId,
                pendingTransferToCarried
            );

            if (!success)
            {
                if (itemTransferMessageText != null)
                {
                    itemTransferMessageText.text = "Không thể thêm item.\nHành trang có thể đã đầy hoặc bạn đang mang một item cùng loại.";
                }
                return;
            }

            HideItemTransferDialog();
            Refresh();
        }

        private void HideItemTransferDialog()
        {
            if (itemTransferDialog != null)
            {
                itemTransferDialog.SetActive(false);
            }

            pendingTransferItem = null;
        }

        private void EnsureItemTransferDialog()
        {
            if (itemTransferDialog != null)
            {
                return;
            }

            Transform parent = panel != null ? panel.transform : transform;
            Font font = GetDialogFont();

            itemTransferDialog = CreateDialogOverlay("ItemTransferDialog", parent);
            GameObject box = CreateDialogBox(itemTransferDialog.transform, font, "Hành trang", new Vector2(414f, 286f), new Color32(255, 247, 229, 255));

            itemTransferMessageText = CreateDialogText("Message", box.transform, font, new Vector2(0f, -4f), new Vector2(354f, 156f), 19, TextAnchor.MiddleCenter);
            itemTransferConfirmButton = CreateDialogButton("ConfirmButton", box.transform, font, "Đồng ý", new Vector2(-92f, -108f), new Color32(76, 164, 86, 255));
            itemTransferCancelButton = CreateDialogButton("CancelButton", box.transform, font, "Để sau", new Vector2(92f, -108f), new Color32(190, 92, 63, 255));

            itemTransferDialog.SetActive(false);
        }

        private Text CreateDialogText(string objectName, Transform parent, Font font, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor anchor)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = anchor;
            text.color = new Color32(58, 28, 12, 255);
            text.supportRichText = true;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(14, fontSize - 3);
            text.resizeTextMaxSize = fontSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Button CreateDialogButton(string objectName, Transform parent, Font font, string label, Vector2 anchoredPosition, Color32 color)
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(122f, 40f);
            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = color;
            Shadow shadow = buttonObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(66, 34, 18, 150);
            shadow.effectDistance = new Vector2(3f, -3f);
            Outline outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = new Color32(112, 61, 31, 220);
            outline.effectDistance = new Vector2(1f, -1f);

            Text labelText = CreateDialogText("Label", buttonObject.transform, font, Vector2.zero, rect.sizeDelta, 18, TextAnchor.MiddleCenter);
            labelText.text = label;
            labelText.color = Color.white;
            labelText.fontStyle = FontStyle.Bold;
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(255, 238, 185, 255);
            colors.pressedColor = new Color32(224, 190, 126, 255);
            colors.selectedColor = Color.white;
            button.colors = colors;
            return button;
        }

        private Font GetDialogFont()
        {
            Font font = Font.CreateDynamicFontFromOSFont(
                new[] { "Trebuchet MS", "Verdana", "Segoe UI", "Tahoma" },
                18
            );
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private GameObject CreateDialogOverlay(string objectName, Transform parent)
        {
            GameObject overlay = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlay.transform.SetParent(parent, false);
            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color32(28, 18, 8, 118);
            return overlay;
        }

        private GameObject CreateDialogBox(Transform parent, Font font, string title, Vector2 size, Color32 bodyColor)
        {
            GameObject box = new GameObject("DialogBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            box.transform.SetParent(parent, false);
            RectTransform boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.anchoredPosition = new Vector2(72f, 0f);
            boxRect.sizeDelta = size;

            Image boxImage = box.GetComponent<Image>();
            boxImage.color = bodyColor;
            Outline boxOutline = box.AddComponent<Outline>();
            boxOutline.effectColor = new Color32(129, 78, 34, 255);
            boxOutline.effectDistance = new Vector2(3f, -3f);
            Shadow boxShadow = box.AddComponent<Shadow>();
            boxShadow.effectColor = new Color32(55, 29, 12, 150);
            boxShadow.effectDistance = new Vector2(6f, -6f);

            GameObject header = new GameObject("Header", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            header.transform.SetParent(box.transform, false);
            RectTransform headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.5f, 1f);
            headerRect.anchorMax = new Vector2(0.5f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = new Vector2(0f, -8f);
            headerRect.sizeDelta = new Vector2(size.x - 18f, 44f);
            Image headerImage = header.GetComponent<Image>();
            headerImage.color = new Color32(65, 142, 73, 255);
            Outline headerOutline = header.AddComponent<Outline>();
            headerOutline.effectColor = new Color32(38, 94, 45, 255);
            headerOutline.effectDistance = new Vector2(2f, -2f);

            Text titleText = CreateDialogText("Title", header.transform, font, Vector2.zero, headerRect.sizeDelta, 21, TextAnchor.MiddleCenter);
            titleText.text = title;
            titleText.color = new Color32(255, 248, 210, 255);
            titleText.fontStyle = FontStyle.Bold;
            return box;
        }

        private void ShowAlertDialog(string message)
        {
            EnsureAlertDialog();

            if (alertMessageText != null)
            {
                alertMessageText.text = message;
            }

            if (alertOkButton != null)
            {
                alertOkButton.onClick.RemoveAllListeners();
                alertOkButton.onClick.AddListener(HideAlertDialog);
            }

            if (alertDialog != null)
            {
                alertDialog.SetActive(true);
                alertDialog.transform.SetAsLastSibling();
            }
        }

        private void HideAlertDialog()
        {
            if (alertDialog != null)
            {
                alertDialog.SetActive(false);
            }
        }

        private void EnsureAlertDialog()
        {
            if (alertDialog != null)
            {
                return;
            }

            Transform parent = panel != null ? panel.transform : transform;
            Font font = GetDialogFont();

            alertDialog = CreateDialogOverlay("InventoryAlertDialog", parent);
            GameObject box = CreateDialogBox(alertDialog.transform, font, "Thông báo", new Vector2(344f, 194f), new Color32(255, 246, 226, 255));

            alertMessageText = CreateDialogText("Message", box.transform, font, new Vector2(0f, -4f), new Vector2(286f, 78f), 19, TextAnchor.MiddleCenter);
            alertOkButton = CreateDialogButton("OkButton", box.transform, font, "Đóng", new Vector2(0f, -72f), new Color32(190, 92, 63, 255));
            alertDialog.SetActive(false);
        }

        private void ShowSkillLearnDialog(int index)
        {
            if (currentInventoryData == null)
            {
                return;
            }

            int skillIdx = index - StatUpgradeSlotCount;
            if (skillIdx < 0 || skillIdx >= currentInventoryData.CombatSkills.Count)
            {
                return;
            }

            EnsureSkillLearnDialog();
            pendingSkill = currentInventoryData.CombatSkills[skillIdx];
            pendingStatUpgrade = null;
            UpdateSkillLearnDialogMessage(null);

            if (skillLearnConfirmButton != null)
            {
                skillLearnConfirmButton.onClick.RemoveAllListeners();
                skillLearnConfirmButton.onClick.AddListener(ConfirmSkillLearn);
                Text label = skillLearnConfirmButton.GetComponentInChildren<Text>(true);
                if (label != null) label.text = pendingSkill.SkillLevel <= 0 ? "Học" : "Nâng";
            }

            if (skillLearnCancelButton != null)
            {
                skillLearnCancelButton.onClick.RemoveAllListeners();
                skillLearnCancelButton.onClick.AddListener(HideSkillLearnDialog);
            }

            if (skillLearnDialog != null)
            {
                skillLearnDialog.SetActive(true);
                skillLearnDialog.transform.SetAsLastSibling();
            }
        }

        private void UpdateSkillLearnDialogMessage(string warning)
        {
            if (pendingSkill == null || skillLearnMessageText == null)
            {
                return;
            }

            int cost = (pendingSkill.SkillLevel + 1) * 5000;
            int nextLevel = pendingSkill.SkillLevel + 1;
            int currentBonus = Mathf.Max(pendingSkill.EffectValue, 0) * pendingSkill.SkillLevel;
            int nextBonus = Mathf.Max(pendingSkill.EffectValue, 0) * nextLevel;
            string levelText = pendingSkill.SkillLevel <= 0 ? "Chưa học" : $"Cấp {pendingSkill.SkillLevel}";
            string warningText = string.IsNullOrEmpty(warning) ? string.Empty : $"\n<color=red>{warning}</color>";

            skillLearnMessageText.text =
                $"<b>{pendingSkill.Name}</b>  <color=#236BC8>{levelText}</color>\n" +
                $"Sức đánh: <color=#00B824>+{currentBonus} -> +{nextBonus}</color>\n" +
                $"Tốn <color=#A9552E>{cost:N0}</color> tiềm năng.\n" +
                $"Xác nhận học/nâng kỹ năng?" +
                warningText;
        }

        private void ShowStatUpgradeDialog(int index)
        {
            InventoryStatUpgradeViewData upgrade = GetStatUpgrade(index);
            if (upgrade == null)
            {
                return;
            }

            EnsureSkillLearnDialog();
            pendingSkill = null;
            pendingStatUpgrade = upgrade;
            UpdateStatUpgradeDialogMessage(null);

            if (skillLearnConfirmButton != null)
            {
                skillLearnConfirmButton.onClick.RemoveAllListeners();
                skillLearnConfirmButton.onClick.AddListener(ConfirmStatUpgrade);
                Text label = skillLearnConfirmButton.GetComponentInChildren<Text>(true);
                if (label != null) label.text = "Nâng";
            }

            if (skillLearnCancelButton != null)
            {
                skillLearnCancelButton.onClick.RemoveAllListeners();
                skillLearnCancelButton.onClick.AddListener(HideSkillLearnDialog);
            }

            if (skillLearnDialog != null)
            {
                skillLearnDialog.SetActive(true);
                skillLearnDialog.transform.SetAsLastSibling();
            }
        }

        private void UpdateStatUpgradeDialogMessage(string warning)
        {
            if (pendingStatUpgrade == null || skillLearnMessageText == null)
            {
                return;
            }

            int currentValue = GetStatUpgradeCurrentValue(pendingStatUpgrade);
            int nextValue = currentValue + pendingStatUpgrade.EffectValue;
            int cost = GetStatUpgradeCost(pendingStatUpgrade);
            string warningText = string.IsNullOrEmpty(warning) ? string.Empty : $"\n<color=red>{warning}</color>";

            skillLearnMessageText.text =
                $"Tăng <b>{pendingStatUpgrade.Name}</b>\n" +
                $"<color=#236BC8>{currentValue}</color>  ->  <color=#00B824>{nextValue}</color>\n" +
                $"Tốn <color=#A9552E>{cost:N0}</color> tiềm năng." +
                warningText;
        }

        private void ConfirmSkillLearn()
        {
            if (pendingSkill == null)
            {
                HideSkillLearnDialog();
                return;
            }

            bool success = TinyDragonSaveManager.Instance.LearnOrUpgradeSkill(pendingSkill.SkillId);
            if (!success)
            {
                string skillName = pendingSkill.Name;
                HideSkillLearnDialog();
                ShowAlertDialog($"Không đủ điểm tiềm năng để học hoặc nâng cấp\n{skillName}.");
                return;
            }

            HideSkillLearnDialog();
            Refresh();
        }

        private void ConfirmStatUpgrade()
        {
            if (pendingStatUpgrade == null)
            {
                HideSkillLearnDialog();
                return;
            }

            string statName = pendingStatUpgrade.Name;
            bool success = TinyDragonSaveManager.Instance.UpgradePlayerStat(pendingStatUpgrade.StatType);
            if (!success)
            {
                HideSkillLearnDialog();
                ShowAlertDialog($"Không đủ điểm tiềm năng để nâng\n{statName}.");
                return;
            }

            HideSkillLearnDialog();
            Refresh();
        }

        private void HideSkillLearnDialog()
        {
            if (skillLearnDialog != null)
            {
                skillLearnDialog.SetActive(false);
            }

            pendingSkill = null;
            pendingStatUpgrade = null;
        }

        private void EnsureSkillLearnDialog()
        {
            if (skillLearnDialog != null)
            {
                return;
            }

            Transform parent = panel != null ? panel.transform : transform;
            Font font = GetDialogFont();

            skillLearnDialog = CreateDialogOverlay("SkillLearnDialog", parent);
            GameObject box = CreateDialogBox(skillLearnDialog.transform, font, "Kỹ năng", new Vector2(408f, 266f), new Color32(255, 248, 232, 255));

            skillLearnMessageText = CreateDialogText("Message", box.transform, font, new Vector2(0f, -2f), new Vector2(344f, 126f), 20, TextAnchor.MiddleCenter);
            skillLearnConfirmButton = CreateDialogButton("LearnButton", box.transform, font, "Học", new Vector2(-96f, -98f), new Color32(82, 126, 202, 255));
            skillLearnCancelButton = CreateDialogButton("CancelButton", box.transform, font, "Hủy", new Vector2(96f, -98f), new Color32(190, 92, 63, 255));

            skillLearnDialog.SetActive(false);
        }

        private InventoryStatUpgradeViewData GetStatUpgrade(int index)
        {
            if (currentInventoryData == null || index < 0 || index >= currentInventoryData.StatUpgrades.Count)
            {
                return null;
            }

            return currentInventoryData.StatUpgrades[index];
        }

        private int GetStatUpgradeCurrentValue(InventoryStatUpgradeViewData upgrade)
        {
            if (upgrade == null || currentInventoryData == null)
            {
                return 0;
            }

            if (upgrade.StatType == "HP") return currentInventoryData.BaseHP;
            if (upgrade.StatType == "KI") return currentInventoryData.BaseKi;
            if (upgrade.StatType == "ATK") return currentInventoryData.BaseAtk;
            if (upgrade.StatType == "DEF") return currentInventoryData.BaseDef;
            if (upgrade.StatType == "CRIT") return currentInventoryData.BaseCritPercent;
            return 0;
        }

        private int GetStatUpgradeCost(InventoryStatUpgradeViewData upgrade)
        {
            if (upgrade == null || currentInventoryData == null)
            {
                return 0;
            }

            if (upgrade.StatType == "HP") return currentInventoryData.BaseHP * 10;
            if (upgrade.StatType == "KI") return currentInventoryData.BaseKi * 10;
            if (upgrade.StatType == "ATK") return currentInventoryData.BaseAtk * 100;
            if (upgrade.StatType == "DEF") return (currentInventoryData.BaseDef + 1) * 500000;
            if (upgrade.StatType == "CRIT") return (currentInventoryData.BaseCritPercent + 1) * 50000000;
            return 0;
        }

        private void ShowSkillDetails(int index)
        {
            if (currentInventoryData == null || skillStatsText == null) return;

            if (index >= 0 && index < StatUpgradeSlotCount)
            {
                InventoryStatUpgradeViewData upgrade = GetStatUpgrade(index);
                if (upgrade == null)
                {
                    return;
                }

                skillStatsText.text = $"[{upgrade.Name.ToUpper()}]\n" +
                                     $"{upgrade.Description}\n" +
                                     $"Chỉ số hiện tại: {GetStatUpgradeCurrentValue(upgrade)}\n" +
                                     $"Chi phí nâng cấp: {GetStatUpgradeCost(upgrade):N0} tiềm năng (Tăng +{upgrade.EffectValue})\n" +
                                     $"Bấm vào dòng để mở hộp thoại nâng cấp.";
            }
            else if (index >= StatUpgradeSlotCount)
            {
                int skillIdx = index - StatUpgradeSlotCount;
                if (skillIdx < currentInventoryData.CombatSkills.Count)
                {
                    var skill = currentInventoryData.CombatSkills[skillIdx];
                    int cost = (skill.SkillLevel + 1) * 5000;
                    int currentBonus = Mathf.Max(skill.EffectValue, 0) * skill.SkillLevel;
                    int nextBonus = Mathf.Max(skill.EffectValue, 0) * (skill.SkillLevel + 1);
                    string levelText = skill.SkillLevel <= 0 ? "CHƯA HỌC" : $"CẤP {skill.SkillLevel}";
                    string confirmText = skill.SkillLevel <= 0 ? "Bấm vào dòng để mở hộp thoại học kỹ năng." : "Bấm vào dòng để mở hộp thoại nâng cấp.";
                    skillStatsText.text = $"[{skill.Name.ToUpper()} - {levelText}]\n" +
                                         $"Mô tả: {skill.Description}\n" +
                                         $"KI hao tổn: {skill.KiCost} | Hồi chiêu: {skill.CooldownSec}s\n" +
                                         $"Sát thương: {skill.DamageMultiplier * 100}%\n" +
                                         $"Sức đánh cộng thêm: {currentBonus} -> {nextBonus}\n" +
                                         $"Chi phí nâng cấp: {cost:N0} tiềm năng\n" +
                                         confirmText;
                }
            }
        }

        private void PerformSkillUpgrade(int index)
        {
            if (currentInventoryData == null) return;

            string statType = null;
            if (index >= 0 && index < StatUpgradeSlotCount)
            {
                InventoryStatUpgradeViewData upgrade = GetStatUpgrade(index);
                statType = upgrade != null ? upgrade.StatType : null;
            }

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
                    ShowAlertDialog("Không đủ điểm tiềm năng để nâng cấp chỉ số này.");
                }
            }
            else if (index >= StatUpgradeSlotCount)
            {
                ShowSkillLearnDialog(index);
            }
        }

        private string BuildItemStat(InventoryItemViewData item)
        {
            return InventoryPanelFormatting.BuildItemStat(item);
        }

        private string GetItemStatsDescription(InventoryItemViewData item)
        {
            return InventoryPanelFormatting.BuildItemStatsDescription(item);
        }

        private Color32 GetIconColor(InventoryItemViewData item)
        {
            return InventoryPanelFormatting.GetIconColor(item);
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
