using TinyDragon.Data;
using TinyDragon.Shared.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TinyDragon.UI
{
    public class PlayerStatusHud : MonoBehaviour
    {
        private const string PanelSpritePath = "res/x4/mainimage/myTexture2dpanel";
        private const string PanelSpriteName = "myTexture2dpanel_0";
        private const string HealthSpritePath = "res/x4/mainimage/myTexture2dHP";
        private const string HealthSpriteName = "myTexture2dHP_0";
        private const string KiSpritePath = "res/x4/mainimage/myTexture2dMP";
        private const string KiSpriteName = "myTexture2dMP_0";
        private const float SenzuDoubleClickWindow = 0.35f;
        private const float SenzuButtonPixelSize = 82f;
        private static readonly Vector2 SenzuButtonRuntimePadding = new Vector2(18f, 14f);

        private static PlayerStatusHud activeHud;

        [SerializeField] private Vector2 panelSize = new Vector2(230f, 61f);
        [SerializeField] private Vector2 panelPadding = new Vector2(12f, 8f);
        [SerializeField] private Vector2 healthBarPosition = new Vector2(121f, -8f);
        [SerializeField] private Vector2 healthBarSize = new Vector2(98f, 15f);
        [SerializeField] private Vector2 kiBarPosition = new Vector2(123f, -31f);
        [SerializeField] private Vector2 kiBarSize = new Vector2(88f, 9f);
        [SerializeField] private Vector2 targetInfoPosition = new Vector2(22f, -8f);
        [SerializeField] private Vector2 targetInfoSize = new Vector2(90f, 43f);
        [SerializeField] private Vector2 targetHealthBarPosition = new Vector2(121f, -8f);
        [SerializeField] private Vector2 targetHealthBarSize = new Vector2(98f, 15f);
        [SerializeField] private string[] hiddenScenes = { "Level_01_Origin" };

        private PlayerHealth playerHealth;
        private PlayerAttack playerAttack;
        private EnemyHealth selectedEnemy;
        private bool isVisibleInCurrentScene = true;
        private Image frameImage;
        private Image healthBarImage;
        private Image kiBarImage;
        private Image targetHealthBarImage;
        private Text targetNameText;
        private Text targetHpText;
        private Button senzuButton;
        private Text senzuCountText;
        private float lastSenzuClickTime = -1f;
        private float nextSenzuRefreshTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveHud()
        {
            activeHud = null;
        }

        public static void EnsureFor(PlayerHealth playerHealth)
        {
            if (playerHealth == null)
            {
                return;
            }

            if (activeHud == null)
            {
                activeHud = ObjectLookup.Any<PlayerStatusHud>();
            }

            if (activeHud == null)
            {
                GameObject hudObject = new GameObject("PlayerStatusHud");
                activeHud = hudObject.AddComponent<PlayerStatusHud>();
                DontDestroyOnLoad(hudObject);
            }

            activeHud.EnsureBuilt();
            activeHud.Bind(playerHealth);
        }

        private void Awake()
        {
            if (activeHud != null && activeHud != this)
            {
                Destroy(gameObject);
                return;
            }

            activeHud = this;
            DontDestroyOnLoad(gameObject);
            EnsureBuilt();
            ApplySceneVisibility(SceneManager.GetActiveScene());
        }

        private void OnEnable()
        {
            PlayerHealth.PlayerAvailable += HandlePlayerAvailable;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            Bind(ObjectLookup.Any<PlayerHealth>());
            ApplySceneVisibility(SceneManager.GetActiveScene());
        }

        private void OnDisable()
        {
            PlayerHealth.PlayerAvailable -= HandlePlayerAvailable;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnbindHealth();
        }

        private void Start()
        {
            Bind(ObjectLookup.Any<PlayerHealth>());
            ApplySceneVisibility(SceneManager.GetActiveScene());
        }

        private void Update()
        {
            if (!isVisibleInCurrentScene)
            {
                return;
            }

            HandleTargetClick();
            UpdateBars();
            UpdateTargetInfo();
            if (Time.unscaledTime >= nextSenzuRefreshTime)
            {
                nextSenzuRefreshTime = Time.unscaledTime + 0.25f;
                RefreshSenzuCount();
            }
        }

        private void HandlePlayerAvailable(PlayerHealth availablePlayerHealth)
        {
            Bind(availablePlayerHealth);
        }

        private void Bind(PlayerHealth newPlayerHealth)
        {
            if (playerHealth == newPlayerHealth)
            {
                UpdateBars();
                return;
            }

            UnbindHealth();
            playerHealth = newPlayerHealth;
            playerAttack = playerHealth != null ? playerHealth.GetComponent<PlayerAttack>() : null;

            if (playerHealth != null)
            {
                playerHealth.HealthChanged += HandleHealthChanged;
            }

            UpdateBars();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplySceneVisibility(scene);
        }

        private void UnbindHealth()
        {
            if (playerHealth != null)
            {
                playerHealth.HealthChanged -= HandleHealthChanged;
            }

            playerHealth = null;
            playerAttack = null;
        }

        private void HandleHealthChanged(PlayerHealth changedPlayerHealth)
        {
            if (changedPlayerHealth == playerHealth)
            {
                UpdateBars();
            }
        }

        private void BuildHud()
        {
            ClearExistingChildren();

            bool canvasWasNew = GetComponent<Canvas>() == null;
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            // Only override renderMode & sortingOrder when the Canvas is brand-new.
            // If it already existed (set up in Inspector), leave it alone so the
            // user's Screen Space – Camera setting is preserved after scene loads.
            if (canvasWasNew)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;
            }

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            RectTransform panelRoot = CreateRect("Panel", transform);
            panelRoot.anchorMin = new Vector2(0f, 1f);
            panelRoot.anchorMax = new Vector2(0f, 1f);
            panelRoot.pivot = new Vector2(0f, 1f);
            panelRoot.anchoredPosition = new Vector2(panelPadding.x, -panelPadding.y);
            panelRoot.sizeDelta = panelSize;

            RectTransform frame = CreateRect("Frame", panelRoot);
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.offsetMin = Vector2.zero;
            frame.offsetMax = Vector2.zero;

            Image panelImage = frame.gameObject.AddComponent<Image>();
            panelImage.sprite = LoadPanelSprite();
            panelImage.preserveAspect = true;
            panelImage.raycastTarget = false;
            frameImage = panelImage;

            healthBarImage = CreateStatusBar("HP Bar", frame, HealthSpritePath, HealthSpriteName, healthBarPosition, healthBarSize);
            kiBarImage = CreateStatusBar("Ki Bar", frame, KiSpritePath, KiSpriteName, kiBarPosition, kiBarSize);
            targetHealthBarImage = CreateStatusBar("Target HP Bar", frame, HealthSpritePath, HealthSpriteName, targetHealthBarPosition, targetHealthBarSize);
            targetHealthBarImage.gameObject.SetActive(false);
            BuildTargetInfo(frame);
            BuildSenzuButton();
            ApplySceneVisibility(SceneManager.GetActiveScene());
        }

        private void ClearExistingChildren()
        {
            Transform panel = transform.Find("Panel");
            if (panel != null)
            {
                Destroy(panel.gameObject);
            }

            Transform senzuButtonTransform = transform.Find("Senzu Button");
            if (senzuButtonTransform != null)
            {
                Destroy(senzuButtonTransform.gameObject);
            }

            frameImage = null;
            healthBarImage = null;
            kiBarImage = null;
            targetHealthBarImage = null;
            targetNameText = null;
            targetHpText = null;
            senzuButton = null;
            senzuCountText = null;
        }

        private void EnsureBuilt()
        {
            if (frameImage != null && healthBarImage != null && kiBarImage != null && targetNameText != null && targetHpText != null && senzuButton != null && senzuCountText != null)
            {
                return;
            }

            if (CacheExistingHud())
            {
                return;
            }

            BuildHud();
        }

        private bool CacheExistingHud()
        {
            Transform panel = transform.Find("Panel");
            Transform frame = panel != null ? panel.Find("Frame") : null;
            if (frame == null)
            {
                return false;
            }

            frameImage = frame.GetComponent<Image>();
            Transform healthBar = frame.Find("HP Bar");
            Transform kiBar = frame.Find("Ki Bar");
            Transform targetHealthBar = frame.Find("Target HP Bar");
            Transform targetInfo = frame.Find("Target Info");
            Transform senzu = transform.Find("Senzu Button");
            healthBarImage = healthBar != null ? healthBar.GetComponent<Image>() : null;
            kiBarImage = kiBar != null ? kiBar.GetComponent<Image>() : null;
            targetHealthBarImage = targetHealthBar != null ? targetHealthBar.GetComponent<Image>() : null;
            targetNameText = targetInfo != null && targetInfo.Find("Name") != null ? targetInfo.Find("Name").GetComponent<Text>() : null;
            targetHpText = targetInfo != null && targetInfo.Find("HP") != null ? targetInfo.Find("HP").GetComponent<Text>() : null;
            senzuButton = senzu != null ? senzu.GetComponent<Button>() : null;
            senzuCountText = senzu != null && senzu.Find("Count") != null ? senzu.Find("Count").GetComponent<Text>() : null;
            ConfigureSenzuButtonClick();
            ApplySenzuButtonLayout(senzu as RectTransform);

            return frameImage != null && healthBarImage != null && kiBarImage != null && targetNameText != null && targetHpText != null && senzuButton != null && senzuCountText != null;
        }

        private void BuildTargetInfo(RectTransform frame)
        {
            RectTransform targetInfo = CreateRect("Target Info", frame);
            targetInfo.anchorMin = new Vector2(0f, 1f);
            targetInfo.anchorMax = new Vector2(0f, 1f);
            targetInfo.pivot = new Vector2(0f, 1f);
            targetInfo.anchoredPosition = targetInfoPosition;
            targetInfo.sizeDelta = targetInfoSize;

            targetNameText = CreateText("Name", targetInfo, 17, FontStyle.Bold, new Color32(22, 87, 33, 255));
            targetNameText.alignment = TextAnchor.MiddleCenter;
            targetNameText.resizeTextForBestFit = true;
            targetNameText.resizeTextMinSize = 10;
            targetNameText.resizeTextMaxSize = 17;
            AddTextShadow(targetNameText.gameObject);
            SetTextRect(targetNameText.rectTransform, new Vector2(2f, -2f), new Vector2(targetInfoSize.x - 4f, 22f));

            targetHpText = CreateText("HP", targetInfo, 17, FontStyle.Bold, new Color32(24, 81, 32, 255));
            targetHpText.alignment = TextAnchor.MiddleCenter;
            AddTextShadow(targetHpText.gameObject);
            SetTextRect(targetHpText.rectTransform, new Vector2(2f, -22f), new Vector2(targetInfoSize.x - 4f, 20f));

            SetTargetVisible(false);
        }

        private void BuildSenzuButton()
        {
            RectTransform buttonRect = CreateRect("Senzu Button", transform);
            buttonRect.anchorMin = new Vector2(1f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(1f, 0f);
            buttonRect.anchoredPosition = new Vector2(-SenzuButtonRuntimePadding.x, SenzuButtonRuntimePadding.y);
            buttonRect.sizeDelta = Vector2.one * SenzuButtonPixelSize;

            Image buttonImage = buttonRect.gameObject.AddComponent<Image>();
            buttonImage.sprite = CreateSenzuButtonSprite();
            buttonImage.type = Image.Type.Simple;
            buttonImage.raycastTarget = true;
            buttonImage.alphaHitTestMinimumThreshold = 0.1f;

            senzuButton = buttonRect.gameObject.AddComponent<Button>();
            ColorBlock colors = senzuButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(255, 242, 188, 255);
            colors.pressedColor = new Color32(255, 215, 120, 255);
            colors.selectedColor = colors.highlightedColor;
            senzuButton.colors = colors;
            ConfigureSenzuButtonClick();

            RectTransform heartRect = CreateRect("Heart", buttonRect);
            SetCenteredTextRect(heartRect, Vector2.zero, new Vector2(58f, 48f));
            Image heartImage = heartRect.gameObject.AddComponent<Image>();
            heartImage.sprite = CreateHeartSprite();
            heartImage.preserveAspect = true;
            heartImage.raycastTarget = false;

            senzuCountText = CreateText("Count", buttonRect, 18, FontStyle.Bold, new Color32(255, 74, 54, 255));
            senzuCountText.alignment = TextAnchor.MiddleCenter;
            AddTextShadow(senzuCountText.gameObject);
            SetCenteredTextRect(senzuCountText.rectTransform, Vector2.zero, new Vector2(46f, 24f));
            RefreshSenzuCount();
        }

        private void ApplySenzuButtonLayout(RectTransform buttonRect)
        {
            if (buttonRect == null)
            {
                return;
            }

            buttonRect.anchorMin = new Vector2(1f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(1f, 0f);
            buttonRect.anchoredPosition = new Vector2(-SenzuButtonRuntimePadding.x, SenzuButtonRuntimePadding.y);
            buttonRect.sizeDelta = Vector2.one * SenzuButtonPixelSize;

            Image buttonImage = buttonRect.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.sprite = CreateSenzuButtonSprite();
                buttonImage.preserveAspect = false;
                buttonImage.raycastTarget = true;
                buttonImage.alphaHitTestMinimumThreshold = 0.1f;
            }

            RectTransform heartRect = buttonRect.Find("Heart") as RectTransform;
            if (heartRect != null)
            {
                SetCenteredTextRect(heartRect, Vector2.zero, new Vector2(58f, 48f));
            }

            if (senzuCountText != null)
            {
                senzuCountText.fontSize = 18;
                senzuCountText.fontStyle = FontStyle.Bold;
                SetCenteredTextRect(senzuCountText.rectTransform, Vector2.zero, new Vector2(46f, 24f));
            }
        }

        private void ConfigureSenzuButtonClick()
        {
            if (senzuButton == null)
            {
                return;
            }

            senzuButton.onClick.RemoveAllListeners();
            senzuButton.onClick.AddListener(HandleSenzuButtonClicked);
        }

        private Image CreateStatusBar(
            string objectName,
            RectTransform parent,
            string spritePath,
            string spriteName,
            Vector2 anchoredPosition,
            Vector2 size
        )
        {
            RectTransform rectTransform = CreateRect(objectName, parent);
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Image image = rectTransform.gameObject.AddComponent<Image>();
            image.sprite = LoadSprite(spritePath, spriteName);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = 0;
            image.fillAmount = 1f;
            image.raycastTarget = false;
            return image;
        }

        private RectTransform CreateRect(string objectName, Transform parent)
        {
            GameObject rectObject = new GameObject(objectName);
            rectObject.transform.SetParent(parent, false);
            return rectObject.AddComponent<RectTransform>();
        }

        private Text CreateText(string objectName, Transform parent, int fontSize, FontStyle fontStyle, Color color)
        {
            RectTransform rectTransform = CreateRect(objectName, parent);
            Text text = rectTransform.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private void SetTextRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }

        private void SetCenteredTextRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }

        private void AddTextShadow(GameObject target)
        {
            Shadow shadow = target.AddComponent<Shadow>();
            shadow.effectColor = new Color32(255, 255, 255, 160);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        private void HandleSenzuButtonClicked()
        {
            float now = Time.unscaledTime;
            if (now - lastSenzuClickTime > SenzuDoubleClickWindow)
            {
                lastSenzuClickTime = now;
                return;
            }

            lastSenzuClickTime = -1f;
            UseSenzuBean();
        }

        private void UseSenzuBean()
        {
            TinyDragonSaveManager saveManager = TinyDragonSaveManager.Instance;
            if (saveManager == null || saveManager.GetSenzuBeanQuantity() <= 0)
            {
                RefreshSenzuCount();
                return;
            }

            Bind(ObjectLookup.Any<PlayerHealth>());

            if (playerAttack == null && playerHealth != null)
            {
                playerAttack = playerHealth.GetComponent<PlayerAttack>();
            }

            if (playerHealth == null || !saveManager.UseSenzuBean(out int restoreHP, out int restoreKi))
            {
                RefreshSenzuCount();
                return;
            }

            int healedHealth = Mathf.Min(playerHealth.CurrentHealth + restoreHP, playerHealth.MaxHealth);
            playerHealth.RestoreHealth(healedHealth, playerHealth.MaxHealth);
            saveManager.SaveCurrentHealth(playerHealth.CurrentHealth, playerHealth.MaxHealth);

            if (playerAttack != null && restoreKi > 0)
            {
                float restoredMana = Mathf.Min(playerAttack.CurrentMana + restoreKi, playerAttack.MaxMana);
                playerAttack.RestoreMana(restoredMana, playerAttack.MaxMana);
                saveManager.SaveCurrentKi(Mathf.RoundToInt(playerAttack.CurrentMana), Mathf.RoundToInt(playerAttack.MaxMana));
            }

            ForceRefreshVitalBars();
            RefreshSenzuCount();
            InventoryPanel.RefreshIfVisible();
        }

        private void ForceRefreshVitalBars()
        {
            Bind(ObjectLookup.Any<PlayerHealth>());
            if (playerHealth != null)
            {
                playerAttack = playerHealth.GetComponent<PlayerAttack>();
            }

            if (playerAttack == null)
            {
                playerAttack = ObjectLookup.Any<PlayerAttack>();
            }

            UpdateBars();
            Canvas.ForceUpdateCanvases();
        }

        private void RefreshSenzuCount()
        {
            if (senzuCountText == null)
            {
                return;
            }

            TinyDragonSaveManager saveManager = TinyDragonSaveManager.Instance;
            int quantity = saveManager != null ? saveManager.GetSenzuBeanQuantity() : 0;
            senzuCountText.text = Mathf.Max(quantity, 0).ToString();
        }

        private Sprite CreateSenzuButtonSprite()
        {
            const int size = 96;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "RuntimeSenzuHeartButton";
            texture.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.48f;
            float innerRadius = size * 0.39f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance > radius)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    Color32 color;
                    if (distance > innerRadius)
                    {
                        color = new Color32(122, 38, 12, 255);
                    }
                    else
                    {
                        float t = Mathf.InverseLerp(-innerRadius, innerRadius, y - center.y);
                        color = Color32.Lerp(new Color32(205, 48, 17, 255), new Color32(255, 146, 28, 255), t);
                    }

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private Sprite CreateHeartSprite()
        {
            const int size = 96;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "RuntimeSenzuHeart";
            texture.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - size * 0.5f) / (size * 0.43f);
                    float ny = (y - size * 0.53f) / (size * 0.43f);
                    ny *= 1.14f;

                    float a = nx * nx + ny * ny - 1f;
                    float heart = a * a * a - nx * nx * ny * ny * ny;
                    bool inside = heart <= 0f && ny > -1.08f;
                    texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private Sprite LoadPanelSprite()
        {
            return LoadSprite(PanelSpritePath, PanelSpriteName);
        }

        private Sprite LoadSprite(string path, string spriteName)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(path);
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && sprites[i].name == spriteName)
                {
                    return sprites[i];
                }
            }

            return Resources.Load<Sprite>(path);
        }

        private void UpdateBars()
        {
            // Tính % HP để fill thanh máu.
            float healthPercent = 1f;
            if (playerHealth != null && playerHealth.MaxHealth > 0)
            {
                healthPercent = Mathf.Clamp01((float)playerHealth.CurrentHealth / playerHealth.MaxHealth);
            }

            if (playerAttack == null)
            {
                playerAttack = ObjectLookup.Any<PlayerAttack>();
            }

            float manaPercent = 1f;
            if (playerAttack != null && playerAttack.MaxMana > 0f)
            {
                manaPercent = Mathf.Clamp01(playerAttack.CurrentMana / playerAttack.MaxMana);
            }

            SetFillAmount(healthBarImage, healthPercent);
            SetFillAmount(kiBarImage, manaPercent);
        }

        private void HandleTargetClick()
        {
            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            UnityEngine.Camera sceneCamera = UnityEngine.Camera.main;
            if (sceneCamera == null)
            {
                return;
            }

            Vector3 worldPoint = sceneCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 clickPoint = new Vector2(worldPoint.x, worldPoint.y);
            RaycastHit2D[] hits = Physics2D.RaycastAll(clickPoint, Vector2.zero);
            for (int i = 0; i < hits.Length; i++)
            {
                EnemyHealth enemyHealth = hits[i].collider != null ? hits[i].collider.GetComponentInParent<EnemyHealth>() : null;
                if (enemyHealth != null && enemyHealth.CurrentHealth > 0)
                {
                    selectedEnemy = enemyHealth;
                    SetTargetVisible(true);
                    UpdateTargetInfo();
                    return;
                }
            }
        }

        private void UpdateTargetInfo()
        {
            if (selectedEnemy == null || selectedEnemy.CurrentHealth <= 0)
            {
                selectedEnemy = null;
                SetTargetVisible(false);
                return;
            }

            SetTargetVisible(true);
            if (targetNameText != null)
            {
                targetNameText.text = selectedEnemy.DisplayName;
            }

            if (targetHpText != null)
            {
                targetHpText.text = selectedEnemy.CurrentHealth.ToString();
            }

            float percent = selectedEnemy.MaxHealth > 0
                ? Mathf.Clamp01((float)selectedEnemy.CurrentHealth / selectedEnemy.MaxHealth)
                : 0f;
            SetFillAmount(targetHealthBarImage, percent);
        }

        private void SetTargetVisible(bool visible)
        {
            if (targetNameText != null) targetNameText.gameObject.SetActive(visible);
            if (targetHpText != null) targetHpText.gameObject.SetActive(visible);
            if (targetHealthBarImage != null) targetHealthBarImage.gameObject.SetActive(visible);
        }

        private void ApplySceneVisibility(Scene scene)
        {
            isVisibleInCurrentScene = !IsSceneHidden(scene.name);

            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = isVisibleInCurrentScene;
            }

            if (!isVisibleInCurrentScene)
            {
                selectedEnemy = null;
                SetTargetVisible(false);
            }
        }

        private bool IsSceneHidden(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || hiddenScenes == null)
            {
                return false;
            }

            for (int i = 0; i < hiddenScenes.Length; i++)
            {
                if (sceneName == hiddenScenes[i])
                {
                    return true;
                }
            }

            return false;
        }

        private void SetFillAmount(Image image, float percent)
        {
            if (image == null)
            {
                return;
            }

            image.fillAmount = Mathf.Clamp01(percent);
        }

        public void OnPauseClicked()
        {
            PauseManager pauseManager = PauseManager.Instance;
            if (pauseManager != null)
            {
                pauseManager.TogglePause();
            }
        }

        public void OnSettingsClicked()
        {
            SettingsManager settingsManager = SettingsManager.Instance;
            if (settingsManager != null)
            {
                settingsManager.ToggleSettings();
            }
        }
    }
}
