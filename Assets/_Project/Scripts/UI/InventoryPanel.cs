using System.Collections.Generic;
using TinyDragon.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace TinyDragon.UI
{
    [ExecuteAlways]
    public sealed class InventoryPanel : MonoBehaviour
    {
        private const int CurrentLayoutVersion = 7;
        private const string PreviewStatsText = "HP: 230 / 260\nKI: 100 / 100\nSức đánh: 12, Crit: 0%\nGiáp: 2, Giảm ST: 0%";

        private static InventoryPanel instance;

        private readonly List<Image> itemIconImages = new List<Image>();
        private readonly List<Text> itemNameTexts = new List<Text>();
        private readonly List<Text> itemStatTexts = new List<Text>();

        [SerializeField] private int layoutVersion;

        private Canvas canvas;
        private RectTransform canvasRoot;
        private RectTransform panel;
        private Text nameText;
        private Text statsText;
        private Text goldText;
        private Text bossGemText;
        private Text premiumText;
        private Font uiFont;
        private Sprite whiteSprite;
        private Sprite circleSprite;
        private Sprite coinSprite;
        private Sprite gemSprite;
        private InventoryViewData currentInventoryData;
        private float nextRuntimeStatsRefreshTime;
        private bool isBuilt;

        private void Awake()
        {
            if (Application.isPlaying && instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            Build();

            if (Application.isPlaying)
            {
                SetVisible(false);
            }
        }

        private void OnEnable()
        {
            Build();

            if (Application.isPlaying)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!CanBuildInCurrentContext())
            {
                return;
            }

            isBuilt = false;
            EditorApplication.delayCall -= BuildEditorPreviewDelayed;
            EditorApplication.delayCall += BuildEditorPreviewDelayed;
        }

        [ContextMenu("Rebuild Inventory UI")]
        private void RebuildInventoryUi()
        {
            if (!CanBuildInCurrentContext())
            {
                Debug.LogWarning("Open Assets/_Project/Prefabs/UI/InventoryPanel.prefab to rebuild and customize the Inventory UI. Scene instances stay lightweight.", this);
                return;
            }

            isBuilt = false;
            layoutVersion = 0;
            ClearChildrenImmediate();
            Build();
            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(gameObject);
        }

        private void BuildEditorPreviewDelayed()
        {
            EditorApplication.delayCall -= BuildEditorPreviewDelayed;
            if (this == null || !CanBuildInCurrentContext())
            {
                return;
            }

            isBuilt = false;
            Build();
            EditorUtility.SetDirty(this);
        }

        private bool CanBuildInCurrentContext()
        {
            if (Application.isPlaying)
            {
                return true;
            }

            if (EditorUtility.IsPersistent(gameObject))
            {
                return false;
            }

            return PrefabStageUtility.GetPrefabStage(gameObject) != null;
        }
#endif

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.B))
            {
                if (!CanOpenInventory())
                {
                    SetVisible(false);
                    return;
                }

                SetVisible(!panel.gameObject.activeSelf);
            }

            if (panel != null && panel.gameObject.activeSelf && Time.unscaledTime >= nextRuntimeStatsRefreshTime)
            {
                nextRuntimeStatsRefreshTime = Time.unscaledTime + 0.25f;
                Refresh();
            }
        }

        private void SetVisible(bool visible)
        {
            Build();
            panel.gameObject.SetActive(visible);

            if (visible)
            {
                Refresh();
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!CanOpenInventory())
            {
                SetVisible(false);
            }
        }

        private bool CanOpenInventory()
        {
            var playerInput = FindAnyObjectByType<PlayerInputReader>();
            if (playerInput != null && !playerInput.inventoryInputEnabled)
            {
                return false;
            }

            if (SceneManager.GetActiveScene().name == "Level_01_Original")
            {
                return false;
            }

            return FindAnyObjectByType<PlayerHealth>() != null;
        }

        private void Build()
        {
#if UNITY_EDITOR
            if (!CanBuildInCurrentContext())
            {
                return;
            }
#endif

            if (isBuilt)
            {
                return;
            }

            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (uiFont == null)
            {
                uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            whiteSprite = CreateSolidSprite(Color.white);
            circleSprite = CreateCircleSprite(64, Color.white);
            coinSprite = Resources.Load<Sprite>("UI/Currency/coin_stack");
            gemSprite = Resources.Load<Sprite>("UI/Currency/gem_green");

            if (!Application.isPlaying && layoutVersion < CurrentLayoutVersion && transform.childCount > 0)
            {
                ClearChildrenImmediate();
                layoutVersion = CurrentLayoutVersion;
            }

            EnsureCanvasComponents();

            if (TryCacheExistingUi())
            {
                ApplyEditorPreview();
                isBuilt = true;
                return;
            }

            panel = CreateRect("Panel", canvasRoot, new Vector2(355f, 640f));
            SetRect(panel, new Vector2(4f, -18f), new Vector2(355f, 640f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            AddImage(panel, new Color32(95, 56, 25, 250));

            RectTransform border = CreateRect("Border", panel, new Vector2(351f, 636f));
            border.anchorMin = Vector2.zero;
            border.anchorMax = Vector2.one;
            border.offsetMin = new Vector2(2f, 2f);
            border.offsetMax = new Vector2(-2f, -2f);
            AddImage(border, new Color32(236, 145, 26, 255));
            border.SetAsFirstSibling();

            RectTransform surface = CreateRect("Surface", panel, new Vector2(345f, 630f));
            surface.anchorMin = Vector2.zero;
            surface.anchorMax = Vector2.one;
            surface.offsetMin = new Vector2(5f, 5f);
            surface.offsetMax = new Vector2(-5f, -5f);
            AddImage(surface, new Color32(148, 98, 50, 230));
            surface.SetSiblingIndex(1);

            BuildHeader();
            BuildTabs();
            BuildItemList();
            BuildCurrencyBar();
            ApplyEditorPreview();

            layoutVersion = CurrentLayoutVersion;
            isBuilt = true;
        }

        private void EnsureCanvasComponents()
        {
            Transform canvasTransform = transform.Find("InventoryCanvas");
            if (canvasTransform == null)
            {
                canvasRoot = CreateRect("InventoryCanvas", transform, Vector2.zero);
            }
            else
            {
                canvasRoot = canvasTransform as RectTransform;
                if (canvasRoot == null)
                {
                    DestroyImmediate(canvasTransform.gameObject);
                    canvasRoot = CreateRect("InventoryCanvas", transform, Vector2.zero);
                }
            }

            canvasRoot.anchorMin = Vector2.zero;
            canvasRoot.anchorMax = Vector2.one;
            canvasRoot.pivot = new Vector2(0.5f, 0.5f);
            canvasRoot.anchoredPosition = Vector2.zero;
            canvasRoot.sizeDelta = Vector2.zero;

            canvas = canvasRoot.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1600;

            CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            if (canvasRoot.GetComponent<GraphicRaycaster>() == null)
            {
                canvasRoot.gameObject.AddComponent<GraphicRaycaster>();
            }

            if (Application.isPlaying)
            {
                EnsureEventSystem();
            }
        }

        private bool TryCacheExistingUi()
        {
            Transform panelTransform = transform.Find("InventoryCanvas/Panel");
            if (panelTransform == null)
            {
                panelTransform = transform.Find("Panel");
            }

            if (panelTransform == null)
            {
                return false;
            }

            panel = panelTransform as RectTransform;
            nameText = FindComponent<Text>(panelTransform, "Header/Name");
            statsText = FindComponent<Text>(panelTransform, "Header/Stats");
            goldText = FindComponent<Text>(panelTransform, "CurrencyBar/Gold");
            bossGemText = FindComponent<Text>(panelTransform, "CurrencyBar/BossGem");
            premiumText = FindComponent<Text>(panelTransform, "CurrencyBar/Premium");

            Button closeButton = FindComponent<Button>(panelTransform, "Header/Close");
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() => SetVisible(false));
            }

            itemIconImages.Clear();
            itemNameTexts.Clear();
            itemStatTexts.Clear();

            Transform itemList = panelTransform.Find("ItemList");
            if (itemList != null)
            {
                for (int i = 0; i < itemList.childCount; i++)
                {
                    Transform row = itemList.GetChild(i);
                    if (!row.name.StartsWith("ItemRow"))
                    {
                        continue;
                    }

                    itemIconImages.Add(FindComponent<Image>(row, "IconCell/Icon"));
                    itemNameTexts.Add(FindComponent<Text>(row, "ItemName"));
                    itemStatTexts.Add(FindComponent<Text>(row, "ItemStat"));
                }
            }

            return panel != null && nameText != null && statsText != null;
        }

        private void BuildHeader()
        {
            RectTransform header = CreateRect("Header", panel, new Vector2(345f, 128f));
            SetRect(header, new Vector2(5f, -5f), new Vector2(345f, 128f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            AddImage(header, new Color32(201, 112, 13, 238));

            nameText = CreateText("Name", header, "DragonBoy250", 14, FontStyle.Bold, new Color32(46, 31, 18, 255));
            SetRect(nameText.rectTransform, new Vector2(9f, -6f), new Vector2(150f, 20f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            RectTransform avatarFrame = CreateRect("AvatarFrame", header, new Vector2(108f, 96f));
            SetRect(avatarFrame, new Vector2(8f, -27f), new Vector2(108f, 96f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            AddImage(avatarFrame, new Color32(255, 229, 151, 255));
            AddOutline(avatarFrame.gameObject, new Color32(88, 45, 12, 255), new Vector2(2f, -2f));

            RectTransform avatarRoot = CreateRect("Avatar", avatarFrame, new Vector2(94f, 84f));
            SetRect(avatarRoot, new Vector2(7f, -6f), new Vector2(94f, 84f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            AddImage(avatarRoot, new Color32(255, 214, 150, 255)).sprite = circleSprite;

            RectTransform hair = CreateRect("Hair", avatarRoot, new Vector2(96f, 42f));
            SetRect(hair, new Vector2(-1f, -1f), new Vector2(96f, 42f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            AddImage(hair, new Color32(8, 7, 6, 255));

            RectTransform face = CreateRect("Face", avatarRoot, new Vector2(72f, 52f));
            SetRect(face, new Vector2(11f, -36f), new Vector2(72f, 52f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            AddImage(face, new Color32(255, 224, 169, 255)).sprite = circleSprite;

            CreateEye(avatarRoot, new Vector2(28f, -51f));
            CreateEye(avatarRoot, new Vector2(54f, -51f));

            statsText = CreateText("Stats", header, PreviewStatsText, 16, FontStyle.Bold, new Color32(255, 244, 84, 255));
            statsText.lineSpacing = 1.05f;
            AddShadow(statsText.gameObject, new Color32(82, 43, 5, 210), new Vector2(1.25f, -1.25f));
            SetRect(statsText.rectTransform, new Vector2(122f, -32f), new Vector2(206f, 92f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            Button closeButton = CreateButton("Close", header, "X", new Color32(239, 75, 20, 255), new Color32(255, 255, 255, 255), 24);
            AddOutline(closeButton.gameObject, new Color32(92, 30, 0, 255), new Vector2(2f, -2f));
            SetRect(closeButton.GetComponent<RectTransform>(), new Vector2(307f, -33f), new Vector2(31f, 31f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            closeButton.onClick.AddListener(() => SetVisible(false));
        }

        private void BuildTabs()
        {
            string[] labels = { "Nhiệm\nVụ", "Hành\nTrang", "Kỹ\nNăng", "Chức\nNăng" };
            for (int i = 0; i < labels.Length; i++)
            {
                bool active = i == 1;
                Button tab = CreateButton(
                    $"Tab {i}",
                    panel,
                    labels[i],
                    active ? new Color32(151, 238, 159, 255) : new Color32(255, 238, 205, 255),
                    active ? new Color32(24, 91, 43, 255) : new Color32(91, 74, 58, 255),
                    16
                );
                AddOutline(tab.gameObject, active ? new Color32(32, 112, 48, 255) : new Color32(154, 100, 40, 255), new Vector2(1.5f, -1.5f));
                SetRect(tab.GetComponent<RectTransform>(), new Vector2(20f + i * 78f, -137f), new Vector2(75f, 43f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            }

            CreatePageButton("Page 1", "1", new Vector2(6f, -188f), new Color32(255, 252, 46, 255), new Color32(112, 112, 0, 255));
            CreatePageButton("Page 2", "2", new Vector2(178f, -188f), new Color32(231, 227, 219, 255), new Color32(88, 88, 88, 255));
        }

        private void CreatePageButton(string objectName, string value, Vector2 position, Color32 background, Color32 textColor)
        {
            RectTransform backgroundRect = CreateRect($"{objectName} Background", panel, new Vector2(171f, 34f));
            SetRect(backgroundRect, position, new Vector2(171f, 34f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            AddImage(backgroundRect, background);

            Text pageText = CreateText(objectName, backgroundRect, value, 18, FontStyle.Bold, textColor);
            pageText.alignment = TextAnchor.MiddleCenter;
            SetRect(pageText.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one);
            pageText.rectTransform.offsetMin = Vector2.zero;
            pageText.rectTransform.offsetMax = Vector2.zero;
        }

        private void BuildItemList()
        {
            RectTransform list = CreateRect("ItemList", panel, new Vector2(348f, 384f));
            SetRect(list, new Vector2(5f, -237f), new Vector2(348f, 384f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            AddImage(list, new Color32(226, 216, 197, 255));
            AddOutline(list.gameObject, new Color32(154, 94, 30, 255), new Vector2(1f, -1f));

            for (int i = 0; i < 8; i++)
            {
                RectTransform row = CreateRect($"ItemRow {i}", list, new Vector2(348f, 48f));
                SetRect(row, new Vector2(0f, -i * 48f), new Vector2(348f, 48f), new Vector2(0f, 1f), new Vector2(0f, 1f));
                AddImage(row, i % 2 == 0 ? new Color32(232, 224, 210, 255) : new Color32(222, 211, 193, 255));

                RectTransform iconCell = CreateRect("IconCell", row, new Vector2(70f, 48f));
                SetRect(iconCell, Vector2.zero, new Vector2(70f, 48f), new Vector2(0f, 1f), new Vector2(0f, 1f));
                AddImage(iconCell, new Color32(159, 126, 78, 255));

                RectTransform iconRect = CreateRect("Icon", iconCell, new Vector2(30f, 24f));
                Image icon = AddImage(iconRect, new Color32(120, 125, 128, 255));
                SetRect(icon.rectTransform, new Vector2(22f, -12f), new Vector2(30f, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f));

                Text itemName = CreateText("ItemName", row, string.Empty, 16, FontStyle.Bold, new Color32(0, 112, 51, 255));
                AddShadow(itemName.gameObject, new Color32(255, 255, 255, 130), new Vector2(0.75f, -0.75f));
                SetRect(itemName.rectTransform, new Vector2(84f, -5f), new Vector2(240f, 22f), new Vector2(0f, 1f), new Vector2(0f, 1f));

                Text itemStat = CreateText("ItemStat", row, string.Empty, 15, FontStyle.Bold, new Color32(0, 128, 255, 255));
                SetRect(itemStat.rectTransform, new Vector2(84f, -25f), new Vector2(240f, 20f), new Vector2(0f, 1f), new Vector2(0f, 1f));

                itemIconImages.Add(icon);
                itemNameTexts.Add(itemName);
                itemStatTexts.Add(itemStat);
            }

            Text downArrow = CreateText("DownArrow", panel, "▼", 17, FontStyle.Bold, new Color32(0, 165, 255, 255));
            downArrow.alignment = TextAnchor.MiddleCenter;
            SetRect(downArrow.rectTransform, new Vector2(324f, -585f), new Vector2(24f, 20f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        }

        private void BuildCurrencyBar()
        {
            RectTransform bar = CreateRect("CurrencyBar", panel, new Vector2(348f, 33f));
            SetRect(bar, new Vector2(5f, -604f), new Vector2(348f, 33f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            AddImage(bar, new Color32(183, 148, 99, 255));
            AddOutline(bar.gameObject, new Color32(119, 76, 31, 255), new Vector2(1f, -1f));

            CreateCurrencyIcon(bar, new Vector2(5f, -5f), new Vector2(38f, 29f), coinSprite, new Color32(255, 219, 28, 255));
            goldText = CreateText("Gold", bar, "0", 16, FontStyle.Bold, new Color32(255, 235, 0, 255));
            SetRect(goldText.rectTransform, new Vector2(48f, -7f), new Vector2(75f, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            CreateCurrencyIcon(bar, new Vector2(140f, -5f), new Vector2(31f, 26f), gemSprite, new Color32(0, 235, 111, 255));
            bossGemText = CreateText("BossGem", bar, "0", 16, FontStyle.Bold, new Color32(255, 235, 0, 255));
            SetRect(bossGemText.rectTransform, new Vector2(174f, -7f), new Vector2(62f, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            CreateCurrencyIcon(bar, new Vector2(250f, -8f), new Color32(247, 31, 77, 255));
            premiumText = CreateText("Premium", bar, "0", 16, FontStyle.Bold, new Color32(255, 235, 0, 255));
            SetRect(premiumText.rectTransform, new Vector2(278f, -7f), new Vector2(62f, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        }

        private void Refresh()
        {
            InventoryViewData data = TinyDragonSaveManager.Instance.LoadInventory();
            currentInventoryData = data;

            if (nameText != null)
            {
                nameText.text = string.IsNullOrWhiteSpace(data.DisplayName) ? "DragonBoy250" : data.DisplayName;
            }

            ApplyStats(data);

            if (goldText != null) goldText.text = data.Gold.ToString();
            if (bossGemText != null) bossGemText.text = data.BossGem.ToString();
            if (premiumText != null) premiumText.text = data.PremiumCoin.ToString();

            for (int i = 0; i < itemNameTexts.Count; i++)
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

        private void ApplyStats(InventoryViewData data)
        {
            if (statsText == null || data == null)
            {
                return;
            }

            int totalHP = data.BaseHP;
            int totalKi = data.BaseKi;
            int totalAtk = data.BaseAtk;
            int totalDefense = data.BaseDef;
            int totalCrit = data.BaseCritPercent;
            int totalDamageReduction = data.BaseDamageReductionPercent;
            foreach (InventoryItemViewData item in data.Items)
            {
                totalHP += item.BonusHP;
                totalKi += item.BonusKi;
                totalAtk += item.BonusAtk;
                totalDefense += item.BonusDef;
                totalDamageReduction += item.BonusDamageReductionPercent;
                totalCrit += item.BonusCritPercent;
            }

            int currentHP = data.CurrentHP;
            int currentKi = data.CurrentKi;
            if (Application.isPlaying)
            {
                PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>();
                if (playerHealth != null)
                {
                    currentHP = playerHealth.CurrentHealth;
                    totalHP = playerHealth.MaxHealth;
                }

                PlayerAttack playerAttack = FindAnyObjectByType<PlayerAttack>();
                if (playerAttack != null)
                {
                    currentKi = Mathf.RoundToInt(playerAttack.CurrentMana);
                    totalKi = Mathf.RoundToInt(playerAttack.MaxMana);
                }
            }

            statsText.text =
                $"HP: {Mathf.Min(currentHP, totalHP)} / {totalHP}\n" +
                $"KI: {Mathf.Min(currentKi, totalKi)} / {totalKi}\n" +
                $"Sức đánh: {totalAtk}, Crit: {totalCrit}%\n" +
                $"Giáp: {totalDefense}, Giảm ST: {totalDamageReduction}%";
        }

        private void ApplyEditorPreview()
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (nameText != null)
            {
                nameText.text = "DragonBoy250";
            }

            if (statsText != null)
            {
                statsText.text = PreviewStatsText;
            }

            if (goldText != null) goldText.text = "2000";
            if (bossGemText != null) bossGemText.text = "0";
            if (premiumText != null) premiumText.text = "20";

            for (int i = 0; i < itemNameTexts.Count; i++)
            {
                if (itemIconImages[i] != null)
                {
                    itemIconImages[i].enabled = i < 2;
                    itemIconImages[i].color = i == 0
                        ? new Color32(195, 199, 200, 255)
                        : new Color32(49, 61, 78, 255);
                }

                if (itemNameTexts[i] != null)
                {
                    itemNameTexts[i].text = i == 0 ? "Áo vải 3 lỗ" : i == 1 ? "Quần vải đen" : string.Empty;
                }

                if (itemStatTexts[i] != null)
                {
                    itemStatTexts[i].text = i == 0 ? "Giáp+2" : i == 1 ? "HP+30" : string.Empty;
                }
            }
        }

        private void ApplyItem(int index, InventoryItemViewData item)
        {
            if (itemIconImages[index] != null)
            {
                itemIconImages[index].enabled = true;
                itemIconImages[index].color = GetIconColor(item);
            }

            if (itemNameTexts[index] != null)
            {
                itemNameTexts[index].text = item.Name;
            }

            if (itemStatTexts[index] != null)
            {
                itemStatTexts[index].text = BuildItemStat(item);
            }
        }

        private void ClearItem(int index)
        {
            if (itemIconImages[index] != null) itemIconImages[index].enabled = false;
            if (itemNameTexts[index] != null) itemNameTexts[index].text = string.Empty;
            if (itemStatTexts[index] != null) itemStatTexts[index].text = string.Empty;
        }

        private string BuildItemStat(InventoryItemViewData item)
        {
            if (item.UpgradeLevel > 0)
            {
                return $"Giáp+{item.UpgradeLevel}";
            }

            if (item.BonusHP != 0)
            {
                return $"HP+{item.BonusHP}";
            }

            if (item.BonusAtk != 0)
            {
                return $"Sức đánh+{item.BonusAtk}";
            }

            return item.Quantity > 1 ? $"x{item.Quantity}" : item.ItemType;
        }

        private Color32 GetIconColor(InventoryItemViewData item)
        {
            if (item.SlotType == "LEG")
            {
                return new Color32(49, 61, 78, 255);
            }

            if (item.SlotType == "BODY")
            {
                return new Color32(195, 199, 200, 255);
            }

            return new Color32(118, 132, 146, 255);
        }

        private void CreateEye(Transform parent, Vector2 position)
        {
            RectTransform eye = CreateRect("Eye", parent, new Vector2(16f, 22f));
            SetRect(eye, position, new Vector2(16f, 22f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            AddImage(eye, Color.white).sprite = circleSprite;

            RectTransform pupil = CreateRect("Pupil", eye, new Vector2(6f, 9f));
            SetRect(pupil, new Vector2(5f, -7f), new Vector2(6f, 9f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            AddImage(pupil, Color.black).sprite = circleSprite;
        }

        private void CreateCurrencyIcon(Transform parent, Vector2 position, Vector2 size, Sprite sprite, Color fallbackColor)
        {
            RectTransform icon = CreateRect("CurrencyIcon", parent, size);
            SetRect(icon, position, size, new Vector2(0f, 1f), new Vector2(0f, 1f));
            Image image = AddImage(icon, sprite != null ? Color.white : fallbackColor);
            image.sprite = sprite != null ? sprite : circleSprite;
            image.preserveAspect = true;
        }

        private void CreateCurrencyIcon(Transform parent, Vector2 position, Color color)
        {
            CreateCurrencyIcon(parent, position, new Vector2(22f, 18f), null, color);
        }

        private Button CreateButton(string objectName, Transform parent, string label, Color32 background, Color32 textColor, int fontSize)
        {
            RectTransform rect = CreateRect(objectName, parent, new Vector2(64f, 32f));
            Image image = AddImage(rect, background);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText("Label", rect, label, fontSize, FontStyle.Bold, textColor);
            text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = fontSize;
            SetRect(text.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private Text CreateText(string objectName, Transform parent, string value, int fontSize, FontStyle style, Color color)
        {
            RectTransform rect = CreateRect(objectName, parent, new Vector2(100f, 24f));
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = uiFont;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private RectTransform CreateRect(string objectName, Transform parent, Vector2 size)
        {
            GameObject rectObject = new GameObject(objectName);
            rectObject.transform.SetParent(parent, false);
            RectTransform rect = rectObject.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            return rect;
        }

        private Image AddImage(RectTransform rect, Color color)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = whiteSprite;
            image.color = color;
            return image;
        }

        private void AddOutline(GameObject target, Color color, Vector2 distance)
        {
            Outline outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }

            outline.effectColor = color;
            outline.effectDistance = distance;
        }

        private void AddShadow(GameObject target, Color color, Vector2 distance)
        {
            Shadow shadow = target.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = target.AddComponent<Shadow>();
            }

            shadow.effectColor = color;
            shadow.effectDistance = distance;
        }

        private T FindComponent<T>(Transform root, string path) where T : Component
        {
            Transform child = root.Find(path);
            return child != null ? child.GetComponent<T>() : null;
        }

        private void SetRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            if (size != Vector2.zero)
            {
                rect.sizeDelta = size;
            }
        }

        private Sprite CreateSolidSprite(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        private Sprite CreateCircleSprite(int size, Color color)
        {
            Texture2D texture = new Texture2D(size, size);
            float center = (size - 1) * 0.5f;
            float radius = size * 0.48f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    texture.SetPixel(x, y, distance <= radius ? color : Color.clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private void ClearChildrenImmediate()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }
    }
}
