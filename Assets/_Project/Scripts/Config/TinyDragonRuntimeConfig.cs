using System;
using UnityEngine;

namespace TinyDragon.Config
{
    [CreateAssetMenu(menuName = "Tiny Dragon/Runtime Config", fileName = "TinyDragonRuntimeConfig")]
    public sealed class TinyDragonRuntimeConfig : ScriptableObject
    {
        [SerializeField] private SceneCatalog scenes = new SceneCatalog();
        [SerializeField] private ResourceCatalog resources = new ResourceCatalog();
        [SerializeField] private GameplayTuning gameplay = new GameplayTuning();
        [SerializeField] private UiTheme ui = new UiTheme();
        [SerializeField] private Level03EncounterConfig level03 = new Level03EncounterConfig();

        public SceneCatalog Scenes => scenes;
        public ResourceCatalog Resources => resources;
        public GameplayTuning Gameplay => gameplay;
        public UiTheme Ui => ui;
        public Level03EncounterConfig Level03 => level03;
    }

    [Serializable]
    public sealed class SceneCatalog
    {
        public string mainMenuSceneName = "MainMenu";
        public string newGameSceneName = "LangAru";
        public string gameOverMenuSceneName = "Level_01_Origin";
        public string guideSceneName = "Level_01_guide";
        public string playerImmortalSceneName = "LangAru";
        public string introNextSceneName = "Level_01_Origin";
        public string level02SceneName = "Level_02";
        public string[] hiddenHudScenes = { "Level_01_Origin" };
    }

    [Serializable]
    public sealed class ResourceCatalog
    {
        public string databaseSchemaPath = "Database/tiny_dragon_schema";
        public string databaseSeedPath = "Database/tiny_dragon_seed";
        public string backgroundMusicClipPath = "Music/XenoverseTrack16Loop";
        public string damagePopupPrefabPath = "Combat/DamagePopup";
        public string playerProjectilePrefabPath = "Combat/PlayerProjectile";
        public string enemyProjectilePrefabPath = "Combat/EnemyProjectile";
        public string inventoryCanvasPrefabPath = "UI/InventoryCanvas";

        public string hudPanelSpritePath = "res/x4/mainimage/myTexture2dpanel";
        public string hudPanelSpriteName = "myTexture2dpanel_0";
        public string hudHealthSpritePath = "res/x4/mainimage/myTexture2dHP";
        public string hudHealthSpriteName = "myTexture2dHP_0";
        public string hudKiSpritePath = "res/x4/mainimage/myTexture2dMP";
        public string hudKiSpriteName = "myTexture2dMP_0";
    }

    [Serializable]
    public sealed class GameplayTuning
    {
        public int level02MaxEnemiesOnGround = 2;
        public bool level02RespawnKilledEnemies;
        public int level02TotalEnemiesBeforeBoss = 2;
        public bool level02SpawnBossAfterNormalEnemies = true;
    }

    [Serializable]
    public sealed class UiTheme
    {
        public Color32 inventoryActiveTabColor = new Color32(151, 238, 159, 255);
        public Color32 inventoryInactiveTabColor = new Color32(255, 238, 205, 255);
        public Color32 inventoryActiveTabTextColor = new Color32(24, 91, 43, 255);
        public Color32 inventoryInactiveTabTextColor = new Color32(91, 74, 58, 255);
        public Color32 hudTargetNameColor = new Color32(22, 87, 33, 255);
        public Color32 hudTargetHpColor = new Color32(24, 81, 32, 255);
        public Color32 hudTextShadowColor = new Color32(255, 255, 255, 160);
    }

    [Serializable]
    public sealed class Level03EncounterConfig
    {
        public int requiredFragments = 2;
        public float shieldIntroDelay = 0.8f;
        public float shieldHitAnnouncementDuration = 0.75f;
        public float fragmentAnnouncementDuration = 1f;
        public float mergeAnnouncementDuration = 1.2f;
        public float preMergeDelay = 0.25f;
        public float mergeDuration = 0.75f;
        public float postMergeEffectDelay = 0.75f;
        public float bossVulnerableAnnouncementDuration = 2f;
        public float hideCompleteGemDelay = 1.5f;
        public float platformFadeDelay = 0.15f;
        public float platformTransitionDuration = 0.25f;
        public float fragmentMoveDuration = 0.32f;
        public float fragmentPixelsPerUnit = 512f;
        public Vector3 mergePoint = new Vector3(0f, 15f, 0f);
        public Vector3 fragmentStandOffset = new Vector3(0f, 0.06f, 0f);
        public Vector2 platformColliderSize = new Vector2(2.6f, 0.15f);
        public Vector2 platformColliderOffset = new Vector2(0f, -0.075f);
        public Vector3 platformVisualScale = new Vector3(1.35f, 0.22f, 1f);
        public Vector3[] platformGroupA =
        {
            new Vector3(-6f, -2.5f, 0f),
            new Vector3(0f, -2.5f, 0f),
            new Vector3(6f, -2.5f, 0f),
            new Vector3(-6f, 1f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(6f, 1f, 0f),
            new Vector3(-6f, 5f, 0f),
            new Vector3(0f, 5f, 0f),
            new Vector3(6f, 5f, 0f),
            new Vector3(-6f, 9f, 0f),
            new Vector3(0f, 9f, 0f),
            new Vector3(6f, 9f, 0f),
            new Vector3(-6f, 13f, 0f),
            new Vector3(0f, 13f, 0f),
            new Vector3(6f, 13f, 0f)
        };
        public Vector3[] platformGroupB =
        {
            new Vector3(-4f, -1f, 0f),
            new Vector3(2f, -1f, 0f),
            new Vector3(8f, -1f, 0f),
            new Vector3(-8f, 3f, 0f),
            new Vector3(-2f, 3f, 0f),
            new Vector3(4f, 3f, 0f),
            new Vector3(-4f, 7f, 0f),
            new Vector3(2f, 7f, 0f),
            new Vector3(8f, 7f, 0f),
            new Vector3(-8f, 11f, 0f),
            new Vector3(-2f, 11f, 0f),
            new Vector3(4f, 11f, 0f),
            new Vector3(-4f, 14.5f, 0f),
            new Vector3(2f, 14.5f, 0f),
            new Vector3(8f, 14.5f, 0f)
        };
    }
}
