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
        [SerializeField] private string[] hiddenScenes = { "Level_01_Original" };

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
                activeHud = FindAnyObjectByType<PlayerStatusHud>();
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
            Bind(FindAnyObjectByType<PlayerHealth>());
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
            Bind(FindAnyObjectByType<PlayerHealth>());
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

            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

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
            ApplySceneVisibility(SceneManager.GetActiveScene());
        }

        private void ClearExistingChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            frameImage = null;
            healthBarImage = null;
            kiBarImage = null;
            targetHealthBarImage = null;
            targetNameText = null;
            targetHpText = null;
        }

        private void EnsureBuilt()
        {
            if (frameImage != null && healthBarImage != null && kiBarImage != null && targetNameText != null && targetHpText != null)
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
            healthBarImage = healthBar != null ? healthBar.GetComponent<Image>() : null;
            kiBarImage = kiBar != null ? kiBar.GetComponent<Image>() : null;
            targetHealthBarImage = targetHealthBar != null ? targetHealthBar.GetComponent<Image>() : null;
            targetNameText = targetInfo != null && targetInfo.Find("Name") != null ? targetInfo.Find("Name").GetComponent<Text>() : null;
            targetHpText = targetInfo != null && targetInfo.Find("HP") != null ? targetInfo.Find("HP").GetComponent<Text>() : null;

            return frameImage != null && healthBarImage != null && kiBarImage != null && targetNameText != null && targetHpText != null;
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
            if (text.font == null)
            {
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

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

        private void AddTextShadow(GameObject target)
        {
            Shadow shadow = target.AddComponent<Shadow>();
            shadow.effectColor = new Color32(255, 255, 255, 160);
            shadow.effectDistance = new Vector2(1f, -1f);
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
            float healthPercent = 1f;
            if (playerHealth != null && playerHealth.MaxHealth > 0)
            {
                healthPercent = Mathf.Clamp01((float)playerHealth.CurrentHealth / playerHealth.MaxHealth);
            }

            if (playerAttack == null)
            {
                playerAttack = FindAnyObjectByType<PlayerAttack>();
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

            Camera sceneCamera = Camera.main;
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
    }
}
