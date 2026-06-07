using UnityEngine;
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

        private PlayerHealth playerHealth;
        private Image frameImage;
        private Image healthBarImage;
        private Image kiBarImage;
        private float kiPercent = 1f;

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
        }

        private void Update()
        {
            UpdateBars();
        }

        private void Bind(PlayerHealth newPlayerHealth)
        {
            playerHealth = newPlayerHealth;
            UpdateBars();
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
        }

        private void EnsureBuilt()
        {
            if (frameImage != null && healthBarImage != null && kiBarImage != null)
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
            healthBarImage = healthBar != null ? healthBar.GetComponent<Image>() : null;
            kiBarImage = kiBar != null ? kiBar.GetComponent<Image>() : null;

            return frameImage != null && healthBarImage != null && kiBarImage != null;
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

            SetFillAmount(healthBarImage, healthPercent);
            SetFillAmount(kiBarImage, kiPercent);
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
