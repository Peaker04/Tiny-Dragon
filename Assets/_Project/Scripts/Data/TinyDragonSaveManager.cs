using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using TinyDragon.Config;
using TinyDragon.Shared.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinyDragon.Data
{
    public sealed class TinyDragonSaveManager : MonoBehaviour
    {
        private const string DefaultPlayerId = "player_default";
        private const string DefaultSaveSlotId = "save_slot_1";
        private const int SkillPotentialCostPerLevel = 5000;

        private static TinyDragonSaveManager instance;

        [SerializeField] private TextAsset schemaSql;
        [SerializeField] private TextAsset seedSql;
        [SerializeField] private TinyDragonRuntimeConfig runtimeConfig;
        [SerializeField] private string databaseFileName = "tiny_dragon.db";
        [SerializeField] private bool saveOnSceneChange = true;
        [SerializeField] private bool saveOnApplicationQuit = true;

        private SqliteDatabase database;
        private bool isReady;
        private TinyDragonRuntimeConfig Config => TinyDragonRuntimeConfigProvider.Resolve(runtimeConfig);

        public event Action<int> GoldChanged;

        public static TinyDragonSaveManager Instance
        {
            get
            {
                EnsureInstance();
                return instance;
            }
        }

        public static TinyDragonSaveManager ExistingInstance => instance;

        public bool IsReady => isReady && database != null && database.IsOpen;

        public string DatabasePath => Path.Combine(Application.persistentDataPath, databaseFileName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static void EnsureInstance()
        {
            if (instance != null)
            {
                return;
            }

            instance = ObjectLookup.Any<TinyDragonSaveManager>();
            if (instance != null)
            {
                return;
            }

            GameObject managerObject = new GameObject("TinyDragonSaveManager");
            instance = managerObject.AddComponent<TinyDragonSaveManager>();
            DontDestroyOnLoad(managerObject);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        }

        private void OnApplicationQuit()
        {
            if (saveOnApplicationQuit)
            {
                SaveCurrentPlayer();
            }
        }

        private void OnDestroy()
        {
            database?.Dispose();
            database = null;
            isReady = false;
        }

        public void Initialize()
        {
            if (isReady && database != null && database.IsOpen)
            {
                return;
            }

            isReady = false;
            LoadSqlAssetsIfNeeded();

            database?.Dispose();
            database = new SqliteDatabase(DatabasePath);
            if (!database.Open())
            {
                database.Dispose();
                database = null;
                return;
            }

            database.ExecuteScript(schemaSql != null ? schemaSql.text : string.Empty);
            EnsureDatabaseColumns();
            database.ExecuteScript(seedSql != null ? seedSql.text : string.Empty);
            EnsureDefaultPlayer();
            EnsureStarterInventory();
            ConsolidateDuplicatePlayerItems();
            EnsurePlayerItemUniqueness();
            EnforceCarriedItemLimit();
            SyncEquipmentWithCarriedItems();
            EnsureBalanceDefaults();
            isReady = true;
            Debug.Log($"Tiny Dragon database ready: {DatabasePath}");
        }

        public void SaveCurrentPlayer()
        {
            if (!isReady)
            {
                Initialize();
            }

            if (!isReady)
            {
                return;
            }

            PlayerHealth playerHealth = ObjectLookup.Any<PlayerHealth>();
            if (playerHealth == null)
            {
                return;
            }

            Transform playerTransform = playerHealth.transform;
            int facingDirection = playerTransform.localScale.x >= 0f ? 1 : -1;
            PlayerSaveSnapshot snapshot = new PlayerSaveSnapshot(
                SceneManager.GetActiveScene().name,
                playerTransform.position,
                facingDirection,
                playerHealth.CurrentHealth,
                playerHealth.MaxHealth
            );

            SavePlayer(snapshot);

            PlayerAttack playerAttack = playerHealth.GetComponent<PlayerAttack>();
            if (playerAttack != null)
            {
                SaveCurrentKi(
                    Mathf.RoundToInt(playerAttack.CurrentMana),
                    Mathf.RoundToInt(playerAttack.MaxMana)
                );
            }
        }

        public bool TryLoadPlayer(out PlayerSaveSnapshot snapshot)
        {
            snapshot = default;
            if (!isReady)
            {
                Initialize();
            }

            if (!isReady)
            {
                return false;
            }

            using (IDbCommand command = database.CreateCommand(
                "SELECT currentSceneName, positionX, positionY, facingDirection, currentHP, baseHP " +
                "FROM Player WHERE id = @playerId LIMIT 1;"
            ))
            {
                SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);

                using (IDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return false;
                    }

                    snapshot = new PlayerSaveSnapshot(
                        reader.GetString(0),
                        new Vector3(Convert.ToSingle(reader.GetDouble(1)), Convert.ToSingle(reader.GetDouble(2)), 0f),
                        reader.GetInt32(3),
                        reader.GetInt32(4),
                        reader.GetInt32(5)
                    );
                    return true;
                }
            }
        }

        public InventoryViewData LoadInventory()
        {
            if (!isReady)
            {
                Initialize();
            }

            InventoryViewData inventory = new InventoryViewData();
            if (!isReady)
            {
                return inventory;
            }

            LoadPlayerInventoryHeader(inventory);
            LoadPlayerInventoryItems(inventory);
            LoadStatUpgradeSkills(inventory);
            LoadPlayerSkills(inventory);
            return inventory;
        }

        public void SaveCurrentHealth(int currentHealth, int maxHealth)
        {
            if (!EnsureReady())
            {
                return;
            }

            ExecuteNonQuery(
                "UPDATE Player SET currentHP = @currentHP, " +
                "baseHP = CASE WHEN baseHP < @minimumBaseHP THEN @minimumBaseHP ELSE baseHP END " +
                "WHERE id = @playerId;",
                command =>
                {
                    SqliteDatabase.AddParameter(command, "@currentHP", Mathf.Max(currentHealth, 0));
                    SqliteDatabase.AddParameter(command, "@minimumBaseHP", GameplayBalanceDefaults.PlayerBaseHealth);
                    SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                }
            );
        }

        public void SaveCurrentKi(int currentKi, int maxKi)
        {
            if (!EnsureReady())
            {
                return;
            }

            ExecuteNonQuery(
                "UPDATE Player SET currentKi = @currentKi, " +
                "baseKi = CASE WHEN baseKi < @minimumBaseKi THEN @minimumBaseKi ELSE baseKi END " +
                "WHERE id = @playerId;",
                command =>
                {
                    SqliteDatabase.AddParameter(command, "@currentKi", Mathf.Max(currentKi, 0));
                    SqliteDatabase.AddParameter(command, "@minimumBaseKi", GameplayBalanceDefaults.PlayerBaseKi);
                    SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                }
            );
        }

        public void ResetCurrentKiToMax()
        {
            if (!EnsureReady())
            {
                return;
            }

            ExecuteNonQuery(
                "UPDATE Player SET currentKi = baseKi WHERE id = @playerId;",
                command => SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId)
            );
        }

        public void SaveCurrencies(int gold, int bossGem, int premiumCoin)
        {
            if (!EnsureReady())
            {
                return;
            }

            ExecuteNonQuery(
                "UPDATE Player SET gold = @gold, bossGem = @bossGem, premiumCoin = @premiumCoin WHERE id = @playerId;",
                command =>
                {
                    SqliteDatabase.AddParameter(command, "@gold", Mathf.Max(gold, 0));
                    SqliteDatabase.AddParameter(command, "@bossGem", Mathf.Max(bossGem, 0));
                    SqliteDatabase.AddParameter(command, "@premiumCoin", Mathf.Max(premiumCoin, 0));
                    SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                }
            );

            GoldChanged?.Invoke(Mathf.Max(gold, 0));
        }

        public bool TryAddGold(int amount, out int totalGold)
        {
            totalGold = 0;
            if (amount <= 0 || !EnsureReady())
            {
                return false;
            }

            ExecuteNonQuery(
                "UPDATE Player SET gold = MAX(gold + @amount, 0) WHERE id = @playerId;",
                command =>
                {
                    SqliteDatabase.AddParameter(command, "@amount", amount);
                    SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                }
            );

            using (IDbCommand command = database.CreateCommand(
                "SELECT gold FROM Player WHERE id = @playerId LIMIT 1;"
            ))
            {
                SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                object value = command.ExecuteScalar();
                totalGold = value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            }

            GoldChanged?.Invoke(totalGold);
            return true;
        }

        public void ApplyLoadedPlayer(PlayerHealth playerHealth)
        {
            if (playerHealth == null || !TryLoadPlayer(out PlayerSaveSnapshot snapshot))
            {
                return;
            }

            InventoryViewData inventory = LoadInventory();
            playerHealth.RestoreHealth(snapshot.CurrentHealth, Mathf.Max(snapshot.MaxHealth, GetTotalMaxHealth(inventory)));
            ApplyRuntimeStats(playerHealth.gameObject, inventory);

            if (snapshot.SceneName != SceneManager.GetActiveScene().name)
            {
                SaveCurrentPlayer();
                return;
            }

            playerHealth.transform.localScale = new Vector3(
                Mathf.Abs(playerHealth.transform.localScale.x) * snapshot.FacingDirection,
                playerHealth.transform.localScale.y,
                playerHealth.transform.localScale.z
            );

            if (!IsValidLoadedPosition(snapshot.Position))
            {
                Debug.LogWarning(
                    $"Ignored invalid saved player position {snapshot.Position} for scene {snapshot.SceneName}."
                );
                return;
            }

            playerHealth.transform.position = new Vector3(
                snapshot.Position.x,
                snapshot.Position.y,
                playerHealth.transform.position.z
            );
        }

        private bool IsValidLoadedPosition(Vector3 position)
        {
            TinyDragon.Camera.MapBounds2D mapBounds = ObjectLookup.Any<TinyDragon.Camera.MapBounds2D>();
            if (mapBounds != null)
            {
                Bounds bounds = mapBounds.GetBounds();
                const float margin = 2f;
                return position.x >= bounds.min.x - margin
                    && position.x <= bounds.max.x + margin
                    && position.y >= bounds.min.y - margin
                    && position.y <= bounds.max.y + margin;
            }

            return Mathf.Abs(position.x) <= 1000f && Mathf.Abs(position.y) <= 1000f;
        }

        private void ApplyRuntimeStats(GameObject playerObject, InventoryViewData inventory)
        {
            if (playerObject == null || inventory == null || string.IsNullOrWhiteSpace(inventory.DisplayName))
            {
                return;
            }

            int totalMaxHealth = inventory.BaseHP;
            int totalMaxKi = inventory.BaseKi;
            int totalAtk = inventory.BaseAtk;
            int totalDefense = inventory.BaseDef;
            int totalDamageReduction = inventory.BaseDamageReductionPercent;
            float totalSpeed = inventory.BaseSpd;
            foreach (InventoryItemViewData item in inventory.Items)
            {
                if (!ShouldApplyItemStats(item))
                {
                    continue;
                }

                totalMaxHealth += item.BonusHP;
                totalMaxKi += item.BonusKi;
                totalAtk += item.BonusAtk;
                totalDefense += item.BonusDef;
                totalDamageReduction += item.BonusDamageReductionPercent;
                totalSpeed += item.BonusSpd;
            }
            foreach (InventorySkillViewData skill in inventory.CombatSkills)
            {
                if (skill.SkillLevel > 0)
                {
                    totalAtk += Mathf.Max(skill.EffectValue, 0) * skill.SkillLevel;
                }
            }

            PlayerHealth playerHealth = playerObject.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.ApplyRuntimeStats(totalMaxHealth, totalDefense, totalDamageReduction);
            }

            PlayerMovement movement = playerObject.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                movement.ApplyMoveSpeed(totalSpeed);
            }

            ProjectileShooter projectileShooter = playerObject.GetComponent<ProjectileShooter>();
            if (projectileShooter != null)
            {
                projectileShooter.ApplyProjectileDamage(totalAtk);
                projectileShooter.ApplyPowerShotDamage(
                    Mathf.RoundToInt(totalAtk * GameplayBalanceDefaults.PowerShotDamageMultiplier)
                );
            }

            PlayerAttack playerAttack = playerObject.GetComponent<PlayerAttack>();
            if (playerAttack != null)
            {
                playerAttack.ApplyAttackCooldown(1f / Mathf.Max(inventory.BaseAttackSpeed, 0.1f));
                playerAttack.ApplyPowerShotTuning(
                    GameplayBalanceDefaults.PowerShotCooldown,
                    GameplayBalanceDefaults.PowerShotManaCostRatio
                );
                playerAttack.RestoreMana(inventory.CurrentKi, totalMaxKi);
            }

            PlayerComboAttack comboAttack = playerObject.GetComponent<PlayerComboAttack>();
            if (comboAttack != null)
            {
                comboAttack.ApplyBaseDamage(totalAtk);
            }
        }

        public bool TryLoadEnemyBalance(string enemyId, out EnemyBalanceData balance)
        {
            balance = default;
            if (string.IsNullOrWhiteSpace(enemyId) || !EnsureReady())
            {
                return false;
            }

            using (IDbCommand command = database.CreateCommand(
                "SELECT name, baseHP, baseAtk, baseDef, baseSpd, expReward, goldReward, bossGemReward " +
                "FROM Enemy WHERE id = @enemyId LIMIT 1;"
            ))
            {
                SqliteDatabase.AddParameter(command, "@enemyId", enemyId);
                using (IDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return false;
                    }

                    balance = new EnemyBalanceData(
                        enemyId,
                        reader.GetString(0),
                        reader.GetInt32(1),
                        reader.GetInt32(2),
                        reader.GetInt32(3),
                        Convert.ToSingle(reader.GetDouble(4)),
                        reader.GetInt32(5),
                        reader.GetInt32(6),
                        reader.GetInt32(7)
                    );
                    return true;
                }
            }
        }

        private void SavePlayer(PlayerSaveSnapshot snapshot)
        {
            string stageId = GetOrCreateStageIdForScene(snapshot.SceneName);

            using (IDbCommand command = database.CreateCommand(
                "UPDATE Player SET currentStageId = @stageId, currentSceneName = @sceneName, " +
                "positionX = @positionX, positionY = @positionY, facingDirection = @facingDirection, " +
                "currentHP = @currentHP WHERE id = @playerId;"
            ))
            {
                SqliteDatabase.AddParameter(command, "@stageId", stageId);
                SqliteDatabase.AddParameter(command, "@sceneName", snapshot.SceneName);
                SqliteDatabase.AddParameter(command, "@positionX", snapshot.Position.x);
                SqliteDatabase.AddParameter(command, "@positionY", snapshot.Position.y);
                SqliteDatabase.AddParameter(command, "@facingDirection", snapshot.FacingDirection);
                SqliteDatabase.AddParameter(command, "@currentHP", snapshot.CurrentHealth);
                SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                command.ExecuteNonQuery();
            }

            using (IDbCommand command = database.CreateCommand(
                "UPDATE SaveSlot SET savedAt = CURRENT_TIMESTAMP WHERE id = @saveSlotId;"
            ))
            {
                SqliteDatabase.AddParameter(command, "@saveSlotId", DefaultSaveSlotId);
                command.ExecuteNonQuery();
            }
        }

        private void EnsureDefaultPlayer()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                sceneName = "Level_01_guide";
            }

            string stageId = GetOrCreateStageIdForScene(sceneName) ?? "stage_guide";
            using (IDbCommand command = database.CreateCommand(
                "INSERT OR IGNORE INTO Player (id, displayName, currentStageId, currentSceneName) " +
                "VALUES (@playerId, @displayName, @stageId, @sceneName);"
            ))
            {
                SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                SqliteDatabase.AddParameter(command, "@displayName", "Player");
                SqliteDatabase.AddParameter(command, "@stageId", stageId);
                SqliteDatabase.AddParameter(command, "@sceneName", sceneName);
                command.ExecuteNonQuery();
            }

            using (IDbCommand command = database.CreateCommand(
                "INSERT OR IGNORE INTO SaveSlot (id, playerId, slotIndex, saveName) " +
                "VALUES (@saveSlotId, @playerId, 1, @saveName);"
            ))
            {
                SqliteDatabase.AddParameter(command, "@saveSlotId", DefaultSaveSlotId);
                SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                SqliteDatabase.AddParameter(command, "@saveName", "Slot 1");
                command.ExecuteNonQuery();
            }
        }

        private void EnsureStarterInventory()
        {
            ExecuteNonQuery(
                "UPDATE Player SET displayName = CASE WHEN displayName = 'Player' THEN 'DragonBoy250' ELSE displayName END, " +
                "currentSceneName = CASE WHEN currentSceneName = 'Level_01' THEN 'Level_01_guide' ELSE currentSceneName END, " +
                "avatarPath = 'UI/Currency/gem_green' " +
                "WHERE id = @playerId;",
                command => SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId)
            );

            ExecuteNonQuery(
                "UPDATE Player SET " +
                "exp = CASE WHEN exp < 500000 THEN 500000 ELSE exp END, " +
                "gold = CASE WHEN gold < 50000 THEN 50000 ELSE gold END, premiumCoin = CASE WHEN premiumCoin < 200 THEN 200 ELSE premiumCoin END, " +
                "currentHP = CASE WHEN baseHP < 230 AND currentHP < 230 THEN 230 ELSE currentHP END, " +
                "baseHP = CASE WHEN baseHP < 230 THEN 230 ELSE baseHP END, " +
                "currentKi = CASE WHEN baseKi < 100 AND currentKi < 100 THEN 100 ELSE currentKi END, " +
                "baseKi = CASE WHEN baseKi < 100 THEN 100 ELSE baseKi END, " +
                "baseAtk = CASE WHEN baseAtk < 12 THEN 12 ELSE baseAtk END " +
                "WHERE id = @playerId;",
                command => SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId)
            );

            ExecuteNonQuery(
                "UPDATE Player SET currentHP = baseHP WHERE id = @playerId AND baseHP >= 230 AND currentHP <= 5;",
                command => SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId)
            );

            ExecuteNonQuery(
                "INSERT OR IGNORE INTO Item (id, name, description, iconKey, prefabKey, itemType, slotType, rarity, " +
                "requiredLevel, stackable, maxStack, sellPriceGold, baseBonusDef, maxUpgrade) " +
                "VALUES ('item_cloth_shirt', 'Áo vải 3 lỗ', 'Starter cloth shirt.', NULL, NULL, 'ARMOR', 'BODY', 'NORMAL', 1, 0, 1, 5, 2, 2);",
                null
            );
            ExecuteNonQuery(
                "UPDATE Item SET name = 'Áo chiến binh xanh', description = 'Áo giáp xanh lấy cảm hứng từ chiến binh Saiyan.', " +
                "rarity = 'NORMAL', baseBonusHP = 0, baseBonusKi = 0, baseBonusAtk = 0, baseBonusDef = 8, baseBonusCritPercent = 0, baseBonusDamageReductionPercent = 0, baseBonusCritDamagePercent = 0, baseBonusSpd = 0, maxUpgrade = 0, " +
                "spritePath = 'UI/Items/warrior_armor_blue' WHERE id = 'item_cloth_shirt';",
                null
            );

            ExecuteNonQuery(
                "INSERT OR IGNORE INTO Item (id, name, description, iconKey, prefabKey, itemType, slotType, rarity, " +
                "requiredLevel, stackable, maxStack, sellPriceGold, baseBonusHP, maxUpgrade) " +
                "VALUES ('item_black_cloth_pants', 'Quần vải đen', 'Starter cloth pants.', NULL, NULL, 'ARMOR', 'LEG', 'NORMAL', 1, 0, 1, 5, 30, 0);",
                null
            );
            ExecuteNonQuery(
                "UPDATE Item SET name = 'Quần chiến binh xanh', description = 'Quần võ phục xanh bền chắc.', " +
                "rarity = 'NORMAL', baseBonusHP = 30, baseBonusKi = 0, baseBonusAtk = 0, baseBonusDef = 0, baseBonusCritPercent = 0, baseBonusDamageReductionPercent = 0, baseBonusCritDamagePercent = 0, baseBonusSpd = 0, maxUpgrade = 0, " +
                "spritePath = 'UI/Items/warrior_pants_blue' WHERE id = 'item_black_cloth_pants';",
                null
            );

            ExecuteNonQuery(
                "INSERT OR IGNORE INTO Item (id, name, description, iconKey, prefabKey, itemType, slotType, rarity, " +
                "requiredLevel, stackable, maxStack, sellPriceGold, baseBonusAtk, maxUpgrade) " +
                "VALUES ('item_warrior_orange_glove', 'Găng chiến binh cam', 'Găng tay tập luyện giúp tăng sức đánh.', NULL, NULL, 'WEAPON', 'WEAPON', 'EPIC', 1, 0, 1, 90, 8, 5);",
                null
            );
            ExecuteNonQuery(
                "UPDATE Item SET name = 'Găng chiến binh cam', description = 'Găng tay tập luyện giúp tăng sức đánh.', " +
                "rarity = 'NORMAL', baseBonusHP = 0, baseBonusKi = 0, baseBonusAtk = 4, baseBonusDef = 0, baseBonusCritPercent = 0, baseBonusDamageReductionPercent = 0, baseBonusCritDamagePercent = 0, baseBonusSpd = 0, maxUpgrade = 0, " +
                "spritePath = 'UI/Items/warrior_glove_orange' WHERE id = 'item_warrior_orange_glove';",
                null
            );

            ExecuteNonQuery(
                "UPDATE Item SET name = 'Áo vải dày', description = 'Áo vải dày giúp tăng giáp cơ bản.' WHERE id = 'item_cloth_shirt';",
                null
            );
            ExecuteNonQuery(
                "UPDATE Item SET name = 'Quần vải đen', description = 'Quần vải đen giúp tăng HP cơ bản.' WHERE id = 'item_black_cloth_pants';",
                null
            );
            ExecuteNonQuery(
                "UPDATE Item SET name = 'Găng vải đen', description = 'Găng vải đen giúp tăng tấn công cơ bản.' WHERE id = 'item_warrior_orange_glove';",
                null
            );

            ExecuteNonQuery(
                "INSERT OR IGNORE INTO Item (id, name, description, iconKey, prefabKey, itemType, slotType, rarity, " +
                "requiredLevel, stackable, maxStack, sellPriceGold, baseBonusHP, baseBonusKi) " +
                "VALUES ('item_senzu_bean_lv5', 'Đậu thần cấp 5', 'Vật phẩm debug số lượng: HP, KI +8000.', NULL, NULL, 'CONSUMABLE', NULL, 'RARE', 1, 1, 99, 25, 8000, 8000);",
                null
            );
            ExecuteNonQuery(
                "UPDATE Item SET name = 'Đậu thần cấp 5', description = 'Vật phẩm debug số lượng: HP, KI +8000.', " +
                "rarity = 'RARE', stackable = 1, maxStack = 99, baseBonusHP = 8000, baseBonusKi = 8000, " +
                "spritePath = 'UI/Items/senzu_bean_lv5' WHERE id = 'item_senzu_bean_lv5';",
                null
            );
            ExecuteNonQuery(
                "UPDATE Item SET description = 'Hoi mot luong HP va KI khi su dung.', " +
                "baseBonusHP = 200, baseBonusKi = 100, baseBonusAtk = 0, baseBonusDef = 0, " +
                "baseBonusCritPercent = 0, baseBonusDamageReductionPercent = 0, baseBonusCritDamagePercent = 0, baseBonusSpd = 0 " +
                "WHERE id = 'item_senzu_bean_lv5';",
                null
            );

            ExecuteNonQuery(
                "INSERT OR IGNORE INTO PlayerItem (id, playerId, itemId, quantity, upgradeLevel) " +
                "VALUES ('playeritem_cloth_shirt', @playerId, 'item_cloth_shirt', 1, 2);",
                command => SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId)
            );

            ExecuteNonQuery(
                "INSERT OR IGNORE INTO PlayerItem (id, playerId, itemId, quantity, upgradeLevel) " +
                "VALUES ('playeritem_black_cloth_pants', @playerId, 'item_black_cloth_pants', 1, 0);",
                command => SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId)
            );

            ExecuteNonQuery(
                "INSERT OR IGNORE INTO PlayerItem (id, playerId, itemId, quantity, upgradeLevel) " +
                "VALUES ('playeritem_warrior_orange_glove', @playerId, 'item_warrior_orange_glove', 1, 0);",
                command => SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId)
            );

            ExecuteNonQuery(
                "INSERT OR IGNORE INTO PlayerItem (id, playerId, itemId, quantity, upgradeLevel) " +
                "VALUES ('playeritem_senzu_bean_lv5', @playerId, 'item_senzu_bean_lv5', 10, 0);",
                command => SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId)
            );
            ExecuteNonQuery(
                "UPDATE PlayerItem SET quantity = CASE WHEN quantity < 10 THEN 10 ELSE quantity END WHERE id = 'playeritem_senzu_bean_lv5' AND playerId = @playerId;",
                command => SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId)
            );
        }

        private void EnsureBalanceDefaults()
        {
            ExecuteNonQuery(
                "UPDATE SchemaVersion SET version = CASE WHEN version < 2 THEN 2 ELSE version END WHERE id = 1;",
                null
            );

            ExecuteNonQuery(
                "UPDATE Player SET " +
                "currentHP = CASE WHEN baseHP < @baseHP AND currentHP < @baseHP THEN @baseHP ELSE currentHP END, " +
                "baseHP = CASE WHEN baseHP < @baseHP THEN @baseHP ELSE baseHP END, " +
                "currentKi = CASE WHEN baseKi < @baseKi AND currentKi < @baseKi THEN @baseKi ELSE currentKi END, " +
                "baseKi = CASE WHEN baseKi < @baseKi THEN @baseKi ELSE baseKi END, " +
                "baseAtk = CASE WHEN baseAtk < @baseAtk THEN @baseAtk ELSE baseAtk END, " +
                "baseSpd = CASE WHEN baseSpd < @baseSpd THEN @baseSpd ELSE baseSpd END " +
                "WHERE id = @playerId;",
                command =>
                {
                    SqliteDatabase.AddParameter(command, "@baseHP", GameplayBalanceDefaults.PlayerBaseHealth);
                    SqliteDatabase.AddParameter(command, "@baseKi", GameplayBalanceDefaults.PlayerBaseKi);
                    SqliteDatabase.AddParameter(command, "@baseAtk", GameplayBalanceDefaults.PlayerBaseAttack);
                    SqliteDatabase.AddParameter(command, "@baseSpd", GameplayBalanceDefaults.PlayerBaseSpeed);
                    SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                }
            );

            ExecuteNonQuery(
                "UPDATE Player SET currentHP = baseHP WHERE id = @playerId AND baseHP >= @baseHP AND currentHP <= 5;",
                command =>
                {
                    SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                    SqliteDatabase.AddParameter(command, "@baseHP", GameplayBalanceDefaults.PlayerBaseHealth);
                }
            );

            ExecuteNonQuery(
                "UPDATE Item SET baseBonusDef = CASE WHEN baseBonusDef < 2 THEN 2 ELSE baseBonusDef END, " +
                "maxUpgrade = CASE WHEN maxUpgrade < 2 THEN 2 ELSE maxUpgrade END WHERE id = 'item_cloth_shirt';",
                null
            );

            ExecuteNonQuery(
                "UPDATE Item SET baseBonusHP = CASE WHEN baseBonusHP < 30 THEN 30 ELSE baseBonusHP END " +
                "WHERE id = 'item_black_cloth_pants';",
                null
            );

            ExecuteNonQuery(
                "UPDATE Enemy SET " +
                "baseHP = CASE WHEN baseHP < @hp THEN @hp ELSE baseHP END, " +
                "baseAtk = CASE WHEN baseAtk < @atk THEN @atk ELSE baseAtk END, " +
                "baseSpd = CASE WHEN baseSpd < @spd THEN @spd ELSE baseSpd END " +
                "WHERE id = 'enemy_monster_1';",
                command =>
                {
                    SqliteDatabase.AddParameter(command, "@hp", GameplayBalanceDefaults.NormalEnemyHealth);
                    SqliteDatabase.AddParameter(command, "@atk", GameplayBalanceDefaults.NormalEnemyDamage);
                    SqliteDatabase.AddParameter(command, "@spd", GameplayBalanceDefaults.NormalEnemySpeed);
                }
            );

            ExecuteNonQuery(
                "UPDATE Enemy SET " +
                "baseHP = CASE WHEN baseHP < @hp THEN @hp ELSE baseHP END, " +
                "baseAtk = CASE WHEN baseAtk < @atk THEN @atk ELSE baseAtk END, " +
                "baseSpd = CASE WHEN baseSpd < @spd THEN @spd ELSE baseSpd END, " +
                "goldReward = CASE WHEN goldReward < @gold THEN @gold ELSE goldReward END, " +
                "bossGemReward = CASE WHEN bossGemReward < 1 THEN 1 ELSE bossGemReward END " +
                "WHERE id = 'enemy_boss_act_1';",
                command =>
                {
                    SqliteDatabase.AddParameter(command, "@hp", GameplayBalanceDefaults.BossHealth);
                    SqliteDatabase.AddParameter(command, "@atk", GameplayBalanceDefaults.BossMeleeDamage);
                    SqliteDatabase.AddParameter(command, "@spd", GameplayBalanceDefaults.BossSpeed);
                    SqliteDatabase.AddParameter(command, "@gold", GameplayBalanceDefaults.BossGoldReward);
                }
            );

            ExecuteNonQuery(
                "UPDATE EnemySpawn SET spawnCount = 2, maxAlive = 2 WHERE id = 'spawn_level_02_monster_1';",
                null
            );

            ExecuteNonQuery(
                "UPDATE Skill SET effectValue = CASE WHEN effectValue < @damage THEN @damage ELSE effectValue END " +
                "WHERE id = 'skill_basic_blast';",
                command => SqliteDatabase.AddParameter(command, "@damage", GameplayBalanceDefaults.PlayerBaseAttack)
            );

            ExecuteNonQuery(
                "INSERT OR IGNORE INTO Skill (id, name, description, iconKey, skillType, kiCost, cooldownSec, " +
                "damageMultiplier, effectType, effectValue, effectDurationSec, projectilePrefabKey) " +
                "VALUES ('skill_power_shot', 'Power Shot', 'Half-Ki heavy projectile.', NULL, 'ACTIVE', 50, 1.2, 3.0, 'DAMAGE', 36, 0, NULL);",
                null
            );

            ExecuteNonQuery(
                "UPDATE Skill SET kiCost = 50, cooldownSec = 1.2, damageMultiplier = 3.0, effectValue = 36 " +
                "WHERE id = 'skill_power_shot';",
                null
            );

            EnsureStatUpgradeSkill(
                "stat_hp_root",
                "HP gốc",
                "Tăng HP tối đa cơ bản.",
                "UI/Skills/stat_hp_root",
                20
            );
            EnsureStatUpgradeSkill(
                "stat_ki_root",
                "KI gốc",
                "Tăng KI tối đa cơ bản.",
                "UI/Skills/stat_ki_root",
                20
            );
            EnsureStatUpgradeSkill(
                "stat_atk_root",
                "Sức đánh gốc",
                "Tăng sức đánh cơ bản.",
                "UI/Skills/stat_atk_root",
                1
            );
            EnsureStatUpgradeSkill(
                "stat_def_root",
                "Giáp gốc",
                "Tăng giáp phòng thủ cơ bản.",
                "UI/Skills/stat_def_root",
                1
            );
            EnsureStatUpgradeSkill(
                "stat_crit_root",
                "Crit gốc",
                "Tăng tỷ lệ chí mạng cơ bản.",
                "UI/Skills/stat_crit_root",
                1
            );
        }

        private void EnsureStatUpgradeSkill(string id, string name, string description, string iconKey, int effectValue)
        {
            ExecuteNonQuery(
                "INSERT OR IGNORE INTO Skill (id, name, description, iconKey, skillType, kiCost, cooldownSec, " +
                "damageMultiplier, effectType, effectValue, effectDurationSec, projectilePrefabKey) " +
                "VALUES (@id, @name, @description, @iconKey, 'PASSIVE', 0, 0, 0, 'BUFF', @effectValue, 0, NULL);",
                command =>
                {
                    SqliteDatabase.AddParameter(command, "@id", id);
                    SqliteDatabase.AddParameter(command, "@name", name);
                    SqliteDatabase.AddParameter(command, "@description", description);
                    SqliteDatabase.AddParameter(command, "@iconKey", iconKey);
                    SqliteDatabase.AddParameter(command, "@effectValue", effectValue);
                }
            );

            ExecuteNonQuery(
                "UPDATE Skill SET name = @name, description = @description, iconKey = @iconKey, " +
                "skillType = 'PASSIVE', kiCost = 0, cooldownSec = 0, damageMultiplier = 0, effectType = 'BUFF', " +
                "effectValue = @effectValue, effectDurationSec = 0 WHERE id = @id;",
                command =>
                {
                    SqliteDatabase.AddParameter(command, "@id", id);
                    SqliteDatabase.AddParameter(command, "@name", name);
                    SqliteDatabase.AddParameter(command, "@description", description);
                    SqliteDatabase.AddParameter(command, "@iconKey", iconKey);
                    SqliteDatabase.AddParameter(command, "@effectValue", effectValue);
                }
            );
        }

        private int GetTotalMaxHealth(InventoryViewData inventory)
        {
            if (inventory == null || string.IsNullOrWhiteSpace(inventory.DisplayName))
            {
                return 0;
            }

            int totalMaxHealth = inventory.BaseHP;
            foreach (InventoryItemViewData item in inventory.Items)
            {
                if (!ShouldApplyItemStats(item))
                {
                    continue;
                }

                totalMaxHealth += item.BonusHP;
            }

            return Mathf.Max(totalMaxHealth, 1);
        }

        private bool ShouldApplyItemStats(InventoryItemViewData item)
        {
            return item != null
                && item.IsCarried
                && !string.IsNullOrWhiteSpace(item.SlotType)
                && item.ItemType != "CONSUMABLE";
        }

        private void LoadPlayerInventoryHeader(InventoryViewData inventory)
        {
            using (IDbCommand command = database.CreateCommand(
                "SELECT displayName, level, gold, premiumCoin, bossGem, currentHP, baseHP, currentKi, baseKi, baseAtk, baseDef, " +
                "baseCritPercent, baseDamageReductionPercent, baseCritDamagePercent, baseAttackSpeed, baseSpd, avatarPath, exp " +
                "FROM Player WHERE id = @playerId LIMIT 1;"
            ))
            {
                SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                using (IDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return;
                    }

                    inventory.DisplayName = reader.GetString(0);
                    inventory.Level = reader.GetInt32(1);
                    inventory.Gold = reader.GetInt32(2);
                    inventory.PremiumCoin = reader.GetInt32(3);
                    inventory.BossGem = reader.GetInt32(4);
                    inventory.CurrentHP = reader.GetInt32(5);
                    inventory.BaseHP = reader.GetInt32(6);
                    inventory.CurrentKi = reader.GetInt32(7);
                    inventory.BaseKi = reader.GetInt32(8);
                    inventory.BaseAtk = reader.GetInt32(9);
                    inventory.BaseDef = reader.GetInt32(10);
                    inventory.BaseCritPercent = reader.GetInt32(11);
                    inventory.BaseDamageReductionPercent = reader.GetInt32(12);
                    inventory.BaseCritDamagePercent = reader.GetInt32(13);
                    inventory.BaseAttackSpeed = Convert.ToSingle(reader.GetDouble(14));
                    inventory.BaseSpd = Convert.ToSingle(reader.GetDouble(15));
                    inventory.AvatarPath = reader.IsDBNull(16) ? null : reader.GetString(16);
                    inventory.Exp = reader.GetInt32(17);
                }
            }
        }

        private void LoadPlayerInventoryItems(InventoryViewData inventory)
        {
            using (IDbCommand command = database.CreateCommand(
                "SELECT MIN(pi.id), i.id, i.name, i.itemType, i.slotType, i.rarity, SUM(pi.quantity), pi.upgradeLevel, " +
                "i.baseBonusHP, i.baseBonusKi, i.baseBonusAtk, i.baseBonusDef, " +
                "i.baseBonusCritPercent, i.baseBonusDamageReductionPercent, i.baseBonusCritDamagePercent, i.baseBonusSpd, i.spritePath " +
                ", MAX(pi.isCarried) " +
                "FROM PlayerItem pi INNER JOIN Item i ON i.id = pi.itemId " +
                "WHERE pi.playerId = @playerId AND pi.quantity > 0 " +
                "GROUP BY pi.playerId, pi.itemId, pi.upgradeLevel, COALESCE(pi.dynamicOptions, '') " +
                "ORDER BY MIN(pi.acquiredAt), i.name;"
            ))
            {
                SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        inventory.Items.Add(new InventoryItemViewData
                        {
                            PlayerItemId = reader.GetString(0),
                            ItemId = reader.GetString(1),
                            Name = reader.GetString(2),
                            ItemType = reader.GetString(3),
                            SlotType = reader.IsDBNull(4) ? null : reader.GetString(4),
                            Rarity = reader.GetString(5),
                            Quantity = Convert.ToInt32(reader.GetValue(6)),
                            UpgradeLevel = reader.GetInt32(7),
                            BonusHP = reader.GetInt32(8),
                            BonusKi = reader.GetInt32(9),
                            BonusAtk = reader.GetInt32(10),
                            BonusDef = reader.GetInt32(11),
                            BonusCritPercent = reader.GetInt32(12),
                            BonusDamageReductionPercent = reader.GetInt32(13),
                            BonusCritDamagePercent = reader.GetInt32(14),
                            BonusSpd = Convert.ToSingle(reader.GetDouble(15)),
                            SpritePath = reader.IsDBNull(16) ? null : reader.GetString(16),
                            IsCarried = reader.GetInt32(17) == 1
                        });
                    }
                }
            }
        }

        private void LoadPlayerSkills(InventoryViewData inventory)
        {
            using (IDbCommand command = database.CreateCommand(
                "SELECT s.id, s.name, s.description, s.skillType, COALESCE(ps.skillLevel, 0), s.kiCost, s.cooldownSec, s.damageMultiplier, s.iconKey, s.effectValue " +
                "FROM Skill s " +
                "LEFT JOIN PlayerSkill ps ON s.id = ps.skillId AND ps.playerId = @playerId " +
                "WHERE s.skillType IN ('ACTIVE', 'ULTIMATE') " +
                "ORDER BY s.id;"
            ))
            {
                SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        inventory.CombatSkills.Add(new InventorySkillViewData
                        {
                            SkillId = reader.GetString(0),
                            Name = reader.GetString(1),
                            Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            SkillType = reader.GetString(3),
                            SkillLevel = reader.GetInt32(4),
                            KiCost = reader.GetInt32(5),
                            CooldownSec = Convert.ToSingle(reader.GetDouble(6)),
                            DamageMultiplier = Convert.ToSingle(reader.GetDouble(7)),
                            IconKey = reader.IsDBNull(8) ? null : reader.GetString(8),
                            EffectValue = reader.GetInt32(9)
                        });
                    }
                }
            }
        }

        private void LoadStatUpgradeSkills(InventoryViewData inventory)
        {
            using (IDbCommand command = database.CreateCommand(
                "SELECT id, name, description, iconKey, effectValue " +
                "FROM Skill WHERE id IN ('stat_hp_root', 'stat_ki_root', 'stat_atk_root', 'stat_def_root', 'stat_crit_root') " +
                "ORDER BY CASE id " +
                "WHEN 'stat_hp_root' THEN 0 WHEN 'stat_ki_root' THEN 1 WHEN 'stat_atk_root' THEN 2 " +
                "WHEN 'stat_def_root' THEN 3 WHEN 'stat_crit_root' THEN 4 ELSE 5 END;"
            ))
            using (IDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    inventory.StatUpgrades.Add(new InventoryStatUpgradeViewData
                    {
                        SkillId = reader.GetString(0),
                        StatType = GetStatTypeForUpgradeSkill(reader.GetString(0)),
                        Name = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        IconKey = reader.IsDBNull(3) ? null : reader.GetString(3),
                        EffectValue = reader.GetInt32(4)
                    });
                }
            }

            EnsureStatUpgradeFallbacks(inventory);
        }

        private void EnsureStatUpgradeFallbacks(InventoryViewData inventory)
        {
            AddStatUpgradeFallback(inventory, "stat_hp_root", "HP", "HP gốc", "Tăng HP tối đa cơ bản.", "UI/Skills/stat_hp_root", 20);
            AddStatUpgradeFallback(inventory, "stat_ki_root", "KI", "KI gốc", "Tăng KI tối đa cơ bản.", "UI/Skills/stat_ki_root", 20);
            AddStatUpgradeFallback(inventory, "stat_atk_root", "ATK", "Sức đánh gốc", "Tăng sức đánh cơ bản.", "UI/Skills/stat_atk_root", 1);
            AddStatUpgradeFallback(inventory, "stat_def_root", "DEF", "Giáp gốc", "Tăng giáp phòng thủ cơ bản.", "UI/Skills/stat_def_root", 1);
            AddStatUpgradeFallback(inventory, "stat_crit_root", "CRIT", "Crit gốc", "Tăng tỷ lệ chí mạng cơ bản.", "UI/Skills/stat_crit_root", 1);
        }

        private void AddStatUpgradeFallback(InventoryViewData inventory, string skillId, string statType, string name, string description, string iconKey, int effectValue)
        {
            foreach (InventoryStatUpgradeViewData upgrade in inventory.StatUpgrades)
            {
                if (upgrade.SkillId == skillId)
                {
                    return;
                }
            }

            inventory.StatUpgrades.Add(new InventoryStatUpgradeViewData
            {
                SkillId = skillId,
                StatType = statType,
                Name = name,
                Description = description,
                IconKey = iconKey,
                EffectValue = effectValue
            });
        }

        private string GetStatTypeForUpgradeSkill(string skillId)
        {
            if (skillId == "stat_hp_root") return "HP";
            if (skillId == "stat_ki_root") return "KI";
            if (skillId == "stat_atk_root") return "ATK";
            if (skillId == "stat_def_root") return "DEF";
            if (skillId == "stat_crit_root") return "CRIT";
            return string.Empty;
        }

        public bool UpgradePlayerStat(string statType)
        {
            if (!EnsureReady()) return false;

            int exp = 0;
            int hp = 0;
            int ki = 0;
            int atk = 0;
            int def = 0;
            int crit = 0;

            using (IDbCommand command = database.CreateCommand(
                "SELECT exp, baseHP, baseKi, baseAtk, baseDef, baseCritPercent FROM Player WHERE id = @playerId LIMIT 1;"
            ))
            {
                SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                using (IDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        exp = reader.GetInt32(0);
                        hp = reader.GetInt32(1);
                        ki = reader.GetInt32(2);
                        atk = reader.GetInt32(3);
                        def = reader.GetInt32(4);
                        crit = reader.GetInt32(5);
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            int cost = 0;
            int increment = 0;
            string updateColumn = "";

            if (statType == "HP")
            {
                cost = hp * 10;
                increment = 20;
                updateColumn = "baseHP";
            }
            else if (statType == "KI")
            {
                cost = ki * 10;
                increment = 20;
                updateColumn = "baseKi";
            }
            else if (statType == "ATK")
            {
                cost = atk * 100;
                increment = 1;
                updateColumn = "baseAtk";
            }
            else if (statType == "DEF")
            {
                cost = (def + 1) * 500000;
                increment = 1;
                updateColumn = "baseDef";
            }
            else if (statType == "CRIT")
            {
                cost = (crit + 1) * 50000000;
                increment = 1;
                updateColumn = "baseCritPercent";
            }
            else
            {
                return false;
            }

            if (exp < cost)
            {
                Debug.LogWarning($"Not enough potential points. Required: {cost}, Available: {exp}");
                return false;
            }

            ExecuteNonQuery(
                $"UPDATE Player SET exp = exp - @cost, {updateColumn} = {updateColumn} + @increment WHERE id = @playerId;",
                cmd =>
                {
                    SqliteDatabase.AddParameter(cmd, "@cost", cost);
                    SqliteDatabase.AddParameter(cmd, "@increment", increment);
                    SqliteDatabase.AddParameter(cmd, "@playerId", DefaultPlayerId);
                }
            );

            if (statType == "HP")
            {
                ExecuteNonQuery(
                    "UPDATE Player SET currentHP = currentHP + @increment WHERE id = @playerId;",
                    cmd =>
                    {
                        SqliteDatabase.AddParameter(cmd, "@increment", increment);
                        SqliteDatabase.AddParameter(cmd, "@playerId", DefaultPlayerId);
                    }
                );
            }
            else if (statType == "KI")
            {
                ExecuteNonQuery(
                    "UPDATE Player SET currentKi = currentKi + @increment WHERE id = @playerId;",
                    cmd =>
                    {
                        SqliteDatabase.AddParameter(cmd, "@increment", increment);
                        SqliteDatabase.AddParameter(cmd, "@playerId", DefaultPlayerId);
                    }
                );
            }

            Debug.Log($"Upgraded {statType} by {increment}. Spent {cost} potential.");
            return true;
        }

        public bool LearnOrUpgradeSkill(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId) || !EnsureReady())
            {
                return false;
            }

            int exp = 0;
            int currentSkillLevel = 0;
            using (IDbCommand command = database.CreateCommand(
                "SELECT p.exp, COALESCE(ps.skillLevel, 0) " +
                "FROM Player p CROSS JOIN Skill s " +
                "LEFT JOIN PlayerSkill ps ON ps.playerId = p.id AND ps.skillId = s.id " +
                "WHERE p.id = @playerId AND s.id = @skillId AND s.skillType IN ('ACTIVE', 'ULTIMATE') LIMIT 1;"
            ))
            {
                SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                SqliteDatabase.AddParameter(command, "@skillId", skillId);
                using (IDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return false;
                    }

                    exp = reader.GetInt32(0);
                    currentSkillLevel = reader.GetInt32(1);
                }
            }

            int cost = (currentSkillLevel + 1) * SkillPotentialCostPerLevel;
            if (exp < cost)
            {
                Debug.LogWarning($"Not enough potential points for skill {skillId}. Required: {cost}, Available: {exp}");
                return false;
            }

            ExecuteNonQuery(
                "UPDATE Player SET exp = exp - @cost WHERE id = @playerId;",
                command =>
                {
                    SqliteDatabase.AddParameter(command, "@cost", cost);
                    SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                }
            );

            if (currentSkillLevel <= 0)
            {
                ExecuteNonQuery(
                    "INSERT INTO PlayerSkill (id, playerId, skillId, skillLevel, isEquipped) " +
                    "VALUES (@playerSkillId, @playerId, @skillId, 1, 0);",
                    command =>
                    {
                        SqliteDatabase.AddParameter(command, "@playerSkillId", $"playerskill_{DefaultPlayerId}_{skillId}");
                        SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                        SqliteDatabase.AddParameter(command, "@skillId", skillId);
                    }
                );
                Debug.Log($"Learned skill {skillId}. Spent {cost} potential.");
            }
            else
            {
                ExecuteNonQuery(
                    "UPDATE PlayerSkill SET skillLevel = skillLevel + 1 WHERE playerId = @playerId AND skillId = @skillId;",
                    command =>
                    {
                        SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                        SqliteDatabase.AddParameter(command, "@skillId", skillId);
                    }
                );
                Debug.Log($"Upgraded skill {skillId} to level {currentSkillLevel + 1}. Spent {cost} potential.");
            }

            ReapplyRuntimeStatsToCurrentPlayer();
            return true;
        }

        private void EnsureDatabaseColumns()
        {
            AddColumnIfMissing("Player", "baseCritPercent", "INTEGER NOT NULL DEFAULT 0 CHECK(baseCritPercent >= 0)");
            AddColumnIfMissing("Player", "baseDamageReductionPercent", "INTEGER NOT NULL DEFAULT 0 CHECK(baseDamageReductionPercent >= 0)");
            AddColumnIfMissing("Player", "baseCritDamagePercent", "INTEGER NOT NULL DEFAULT 0 CHECK(baseCritDamagePercent >= 0)");
            AddColumnIfMissing("Player", "baseAttackSpeed", "REAL NOT NULL DEFAULT 1.0 CHECK(baseAttackSpeed > 0)");
            AddColumnIfMissing("Player", "avatarPath", "TEXT");
            AddColumnIfMissing("Item", "baseBonusCritPercent", "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfMissing("Item", "baseBonusDamageReductionPercent", "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfMissing("Item", "baseBonusCritDamagePercent", "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfMissing("Item", "spritePath", "TEXT");
            AddColumnIfMissing("PlayerItem", "isCarried", "INTEGER NOT NULL DEFAULT 1 CHECK(isCarried IN (0,1))");
        }

        public bool SetPlayerItemCarried(string playerItemId, bool isCarried)
        {
            if (string.IsNullOrWhiteSpace(playerItemId) || !EnsureReady())
            {
                return false;
            }

            if (isCarried && GetCarriedItemCountExcluding(playerItemId) >= 10)
            {
                Debug.LogWarning("Cannot carry more than 10 inventory items.");
                return false;
            }

            if (isCarried && HasCarriedSlotTypeConflict(playerItemId))
            {
                Debug.LogWarning("Cannot carry two items with the same slot type.");
                return false;
            }

            ExecuteNonQuery(
                "UPDATE PlayerItem SET isCarried = @isCarried WHERE id = @playerItemId AND playerId = @playerId;",
                command =>
                {
                    SqliteDatabase.AddParameter(command, "@isCarried", isCarried ? 1 : 0);
                    SqliteDatabase.AddParameter(command, "@playerItemId", playerItemId);
                    SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                }
            );

            if (isCarried)
            {
                EquipPlayerItemIfSlotted(playerItemId);
            }
            else
            {
                UnequipPlayerItem(playerItemId);
            }

            ReapplyRuntimeStatsToCurrentPlayer();
            return true;
        }

        private void EquipPlayerItemIfSlotted(string playerItemId)
        {
            string slotType = GetSlotTypeForPlayerItem(playerItemId);
            if (string.IsNullOrWhiteSpace(slotType))
            {
                return;
            }

            ExecuteNonQuery(
                "DELETE FROM PlayerEquipment WHERE playerId = @playerId AND slotType = @slotType;",
                command =>
                {
                    SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                    SqliteDatabase.AddParameter(command, "@slotType", slotType);
                }
            );

            ExecuteNonQuery(
                "INSERT OR REPLACE INTO PlayerEquipment (id, playerId, slotType, playerItemId) " +
                "VALUES (@equipmentId, @playerId, @slotType, @playerItemId);",
                command =>
                {
                    SqliteDatabase.AddParameter(command, "@equipmentId", $"equipment_{DefaultPlayerId}_{slotType}");
                    SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                    SqliteDatabase.AddParameter(command, "@slotType", slotType);
                    SqliteDatabase.AddParameter(command, "@playerItemId", playerItemId);
                }
            );
        }

        private void UnequipPlayerItem(string playerItemId)
        {
            ExecuteNonQuery(
                "DELETE FROM PlayerEquipment WHERE playerId = @playerId AND playerItemId = @playerItemId;",
                command =>
                {
                    SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                    SqliteDatabase.AddParameter(command, "@playerItemId", playerItemId);
                }
            );
        }

        private string GetSlotTypeForPlayerItem(string playerItemId)
        {
            using (IDbCommand command = database.CreateCommand(
                "SELECT i.slotType FROM PlayerItem pi INNER JOIN Item i ON i.id = pi.itemId " +
                "WHERE pi.id = @playerItemId AND pi.playerId = @playerId LIMIT 1;"
            ))
            {
                SqliteDatabase.AddParameter(command, "@playerItemId", playerItemId);
                SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? null : value.ToString();
            }
        }

        public int GetSenzuBeanQuantity()
        {
            if (!EnsureReady())
            {
                return 0;
            }

            using (IDbCommand command = database.CreateCommand(
                "SELECT COALESCE(SUM(quantity), 0) FROM PlayerItem " +
                "WHERE playerId = @playerId AND itemId = 'item_senzu_bean_lv5' AND quantity > 0;"
            ))
            {
                SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        public bool UseSenzuBean(out int restoreHP, out int restoreKi)
        {
            restoreHP = 0;
            restoreKi = 0;

            if (!EnsureReady())
            {
                return false;
            }

            string playerItemId = null;
            int quantity = 0;
            using (IDbCommand command = database.CreateCommand(
                "SELECT pi.id, pi.quantity, i.baseBonusHP, i.baseBonusKi FROM PlayerItem pi " +
                "INNER JOIN Item i ON i.id = pi.itemId " +
                "WHERE pi.playerId = @playerId AND pi.itemId = 'item_senzu_bean_lv5' AND pi.quantity > 0 " +
                "ORDER BY pi.isCarried DESC, pi.acquiredAt, pi.id LIMIT 1;"
            ))
            {
                SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                using (IDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return false;
                    }

                    playerItemId = reader.GetString(0);
                    quantity = reader.GetInt32(1);
                    restoreHP = Mathf.Max(reader.GetInt32(2), 0);
                    restoreKi = Mathf.Max(reader.GetInt32(3), 0);
                }
            }

            if (quantity <= 1)
            {
                ExecuteNonQuery(
                    "DELETE FROM PlayerItem WHERE id = @playerItemId AND playerId = @playerId;",
                    command =>
                    {
                        SqliteDatabase.AddParameter(command, "@playerItemId", playerItemId);
                        SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                    }
                );
            }
            else
            {
                ExecuteNonQuery(
                    "UPDATE PlayerItem SET quantity = quantity - 1 WHERE id = @playerItemId AND playerId = @playerId;",
                    command =>
                    {
                        SqliteDatabase.AddParameter(command, "@playerItemId", playerItemId);
                        SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                    }
                );
            }

            return true;
        }

        public void ReapplyRuntimeStatsToCurrentPlayer()
        {
            PlayerHealth playerHealth = ObjectLookup.Any<PlayerHealth>();
            if (playerHealth == null)
            {
                return;
            }

            ApplyRuntimeStats(playerHealth.gameObject, LoadInventory());
        }

        private int GetCarriedItemCountExcluding(string excludedPlayerItemId)
        {
            using (IDbCommand command = database.CreateCommand(
                "SELECT COUNT(*) FROM PlayerItem WHERE playerId = @playerId AND isCarried = 1 AND id <> @excludedPlayerItemId;"
            ))
            {
                SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                SqliteDatabase.AddParameter(command, "@excludedPlayerItemId", excludedPlayerItemId);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private bool HasCarriedSlotTypeConflict(string playerItemId)
        {
            using (IDbCommand command = database.CreateCommand(
                "SELECT COUNT(*) FROM PlayerItem target " +
                "INNER JOIN Item targetItem ON targetItem.id = target.itemId " +
                "INNER JOIN PlayerItem carried ON carried.playerId = target.playerId AND carried.isCarried = 1 AND carried.id <> target.id " +
                "INNER JOIN Item carriedItem ON carriedItem.id = carried.itemId " +
                "WHERE target.id = @playerItemId AND target.playerId = @playerId " +
                "AND targetItem.slotType IS NOT NULL AND carriedItem.slotType = targetItem.slotType;"
            ))
            {
                SqliteDatabase.AddParameter(command, "@playerItemId", playerItemId);
                SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        private void EnforceCarriedItemLimit()
        {
            List<string> carriedIds = new List<string>();
            using (IDbCommand command = database.CreateCommand(
                "SELECT id FROM PlayerItem WHERE playerId = @playerId AND isCarried = 1 ORDER BY acquiredAt, id;"
            ))
            {
                SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        carriedIds.Add(reader.GetString(0));
                    }
                }
            }

            for (int i = 10; i < carriedIds.Count; i++)
            {
                ExecuteNonQuery(
                    "UPDATE PlayerItem SET isCarried = 0 WHERE id = @playerItemId;",
                    command => SqliteDatabase.AddParameter(command, "@playerItemId", carriedIds[i])
                );
            }

            EnforceUniqueCarriedSlotTypes();
        }

        private void SyncEquipmentWithCarriedItems()
        {
            ExecuteNonQuery(
                "DELETE FROM PlayerEquipment WHERE playerId = @playerId AND playerItemId IN (" +
                "SELECT id FROM PlayerItem WHERE playerId = @playerId AND isCarried = 0" +
                ");",
                command => SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId)
            );

            List<string> carriedSlottedItemIds = new List<string>();
            using (IDbCommand command = database.CreateCommand(
                "SELECT pi.id FROM PlayerItem pi INNER JOIN Item i ON i.id = pi.itemId " +
                "WHERE pi.playerId = @playerId AND pi.isCarried = 1 AND i.slotType IS NOT NULL " +
                "ORDER BY pi.acquiredAt, pi.id;"
            ))
            {
                SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        carriedSlottedItemIds.Add(reader.GetString(0));
                    }
                }
            }

            foreach (string playerItemId in carriedSlottedItemIds)
            {
                EquipPlayerItemIfSlotted(playerItemId);
            }
        }

        private void EnforceUniqueCarriedSlotTypes()
        {
            HashSet<string> seenSlotTypes = new HashSet<string>();
            List<string> duplicatePlayerItemIds = new List<string>();
            using (IDbCommand command = database.CreateCommand(
                "SELECT pi.id, i.slotType FROM PlayerItem pi INNER JOIN Item i ON i.id = pi.itemId " +
                "WHERE pi.playerId = @playerId AND pi.isCarried = 1 AND i.slotType IS NOT NULL " +
                "ORDER BY pi.acquiredAt, pi.id;"
            ))
            {
                SqliteDatabase.AddParameter(command, "@playerId", DefaultPlayerId);
                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string playerItemId = reader.GetString(0);
                        string slotType = reader.GetString(1);
                        if (seenSlotTypes.Add(slotType))
                        {
                            continue;
                        }

                        duplicatePlayerItemIds.Add(playerItemId);
                    }
                }
            }

            foreach (string duplicatePlayerItemId in duplicatePlayerItemIds)
            {
                ExecuteNonQuery(
                    "UPDATE PlayerItem SET isCarried = 0 WHERE id = @playerItemId;",
                    updateCommand => SqliteDatabase.AddParameter(updateCommand, "@playerItemId", duplicatePlayerItemId)
                );
            }
        }

        private void ConsolidateDuplicatePlayerItems()
        {
            List<PlayerItemMergeRow> rows = LoadPlayerItemMergeRows();
            Dictionary<string, List<PlayerItemMergeRow>> groups = new Dictionary<string, List<PlayerItemMergeRow>>();

            foreach (PlayerItemMergeRow row in rows)
            {
                if (!groups.TryGetValue(row.MergeKey, out List<PlayerItemMergeRow> group))
                {
                    group = new List<PlayerItemMergeRow>();
                    groups.Add(row.MergeKey, group);
                }

                group.Add(row);
            }

            foreach (List<PlayerItemMergeRow> group in groups.Values)
            {
                if (group.Count <= 1)
                {
                    continue;
                }

                PlayerItemMergeRow keeper = group[0];
                int totalQuantity = 0;
                bool shouldCarryMergedItem = false;
                foreach (PlayerItemMergeRow row in group)
                {
                    totalQuantity += row.Quantity;
                    shouldCarryMergedItem |= row.IsCarried;
                }

                ExecuteNonQuery(
                    "UPDATE PlayerItem SET quantity = @quantity, isCarried = @isCarried WHERE id = @keeperId;",
                    command =>
                    {
                        SqliteDatabase.AddParameter(command, "@quantity", Mathf.Max(totalQuantity, 1));
                        SqliteDatabase.AddParameter(command, "@isCarried", shouldCarryMergedItem ? 1 : 0);
                        SqliteDatabase.AddParameter(command, "@keeperId", keeper.Id);
                    }
                );

                for (int i = 1; i < group.Count; i++)
                {
                    PlayerItemMergeRow duplicate = group[i];
                    if (duplicate.IsEquipped)
                    {
                        if (keeper.IsEquipped)
                        {
                            ExecuteNonQuery(
                                "DELETE FROM PlayerEquipment WHERE playerItemId = @duplicateId;",
                                command => SqliteDatabase.AddParameter(command, "@duplicateId", duplicate.Id)
                            );
                        }
                        else
                        {
                            ExecuteNonQuery(
                                "UPDATE PlayerEquipment SET playerItemId = @keeperId WHERE playerItemId = @duplicateId;",
                                command =>
                                {
                                    SqliteDatabase.AddParameter(command, "@keeperId", keeper.Id);
                                    SqliteDatabase.AddParameter(command, "@duplicateId", duplicate.Id);
                                }
                            );
                            keeper.IsEquipped = true;
                        }
                    }

                    ExecuteNonQuery(
                        "DELETE FROM PlayerItem WHERE id = @duplicateId;",
                        command => SqliteDatabase.AddParameter(command, "@duplicateId", duplicate.Id)
                    );
                }
            }
        }

        private List<PlayerItemMergeRow> LoadPlayerItemMergeRows()
        {
            List<PlayerItemMergeRow> rows = new List<PlayerItemMergeRow>();
            using (IDbCommand command = database.CreateCommand(
                "SELECT pi.id, pi.playerId, pi.itemId, pi.quantity, pi.upgradeLevel, COALESCE(pi.dynamicOptions, ''), " +
                "CASE WHEN pe.playerItemId IS NULL THEN 0 ELSE 1 END AS isEquipped, pi.isCarried " +
                "FROM PlayerItem pi LEFT JOIN PlayerEquipment pe ON pe.playerItemId = pi.id " +
                "ORDER BY pi.playerId, pi.itemId, pi.upgradeLevel, COALESCE(pi.dynamicOptions, ''), pi.isCarried DESC, isEquipped DESC, pi.acquiredAt, pi.id;"
            ))
            using (IDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    rows.Add(new PlayerItemMergeRow(
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetInt32(3),
                        reader.GetInt32(4),
                        reader.GetString(5),
                        reader.GetInt32(6) == 1,
                        reader.GetInt32(7) == 1
                    ));
                }
            }

            return rows;
        }

        private void EnsurePlayerItemUniqueness()
        {
            database.Execute(
                "CREATE UNIQUE INDEX IF NOT EXISTS idx_playeritem_unique_stack " +
                "ON PlayerItem(playerId, itemId, upgradeLevel, COALESCE(dynamicOptions, ''));"
            );
        }

        private void AddColumnIfMissing(string tableName, string columnName, string columnDefinition)
        {
            if (ColumnExists(tableName, columnName))
            {
                return;
            }

            database.Execute($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};");
        }

        private bool ColumnExists(string tableName, string columnName)
        {
            using (IDbCommand command = database.CreateCommand($"PRAGMA table_info({tableName});"))
            using (IDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void ExecuteNonQuery(string sql, Action<IDbCommand> bind)
        {
            using (IDbCommand command = database.CreateCommand(sql))
            {
                bind?.Invoke(command);
                command.ExecuteNonQuery();
            }
        }

        private sealed class PlayerItemMergeRow
        {
            public PlayerItemMergeRow(
                string id,
                string playerId,
                string itemId,
                int quantity,
                int upgradeLevel,
                string dynamicOptions,
                bool isEquipped,
                bool isCarried
            )
            {
                Id = id;
                Quantity = Mathf.Max(quantity, 1);
                IsEquipped = isEquipped;
                IsCarried = isCarried;
                MergeKey = $"{playerId}\u001F{itemId}\u001F{upgradeLevel}\u001F{dynamicOptions ?? string.Empty}";
            }

            public string Id { get; }
            public int Quantity { get; }
            public bool IsEquipped { get; set; }
            public bool IsCarried { get; }
            public string MergeKey { get; }
        }

        private bool EnsureReady()
        {
            if (!IsReady)
            {
                Initialize();
            }

            return IsReady;
        }

        private string GetOrCreateStageIdForScene(string sceneName)
        {
            if (database == null || !database.IsOpen || string.IsNullOrWhiteSpace(sceneName))
            {
                return null;
            }

            string existingStageId = GetStageIdForScene(sceneName);
            if (!string.IsNullOrWhiteSpace(existingStageId))
            {
                return existingStageId;
            }

            string generatedStageId = $"stage_{SanitizeId(sceneName)}";
            ExecuteNonQuery(
                "INSERT OR IGNORE INTO Stage " +
                "(id, zoneId, stageType, name, description, sceneName, orderIndex, minLevelRequired, rewardExp, rewardGold) " +
                "VALUES (@id, @zoneId, @stageType, @name, @description, @sceneName, @orderIndex, @minLevelRequired, @rewardExp, @rewardGold);",
                command =>
                {
                    SqliteDatabase.AddParameter(command, "@id", generatedStageId);
                    SqliteDatabase.AddParameter(command, "@zoneId", "zone_earth");
                    SqliteDatabase.AddParameter(command, "@stageType", "STORY");
                    SqliteDatabase.AddParameter(command, "@name", sceneName);
                    SqliteDatabase.AddParameter(command, "@description", $"Runtime stage entry for {sceneName}.");
                    SqliteDatabase.AddParameter(command, "@sceneName", sceneName);
                    SqliteDatabase.AddParameter(command, "@orderIndex", 999);
                    SqliteDatabase.AddParameter(command, "@minLevelRequired", 1);
                    SqliteDatabase.AddParameter(command, "@rewardExp", 0);
                    SqliteDatabase.AddParameter(command, "@rewardGold", 0);
                }
            );

            return GetStageIdForScene(sceneName) ?? generatedStageId;
        }

        private static string SanitizeId(string value)
        {
            char[] chars = value.ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                bool valid = (chars[i] >= 'a' && chars[i] <= 'z') || (chars[i] >= '0' && chars[i] <= '9');
                if (!valid)
                {
                    chars[i] = '_';
                }
            }

            return new string(chars).Trim('_');
        }

        private string GetStageIdForScene(string sceneName)
        {
            if (database == null || !database.IsOpen || string.IsNullOrWhiteSpace(sceneName))
            {
                return null;
            }

            using (IDbCommand command = database.CreateCommand(
                "SELECT id FROM Stage WHERE sceneName = @sceneName LIMIT 1;"
            ))
            {
                SqliteDatabase.AddParameter(command, "@sceneName", sceneName);
                object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? null : value.ToString();
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>();
            if (playerHealth != null)
            {
                ApplyLoadedPlayer(playerHealth);
            }
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            if (saveOnSceneChange)
            {
                SaveCurrentPlayer();
            }
        }

        private void LoadSqlAssetsIfNeeded()
        {
            if (schemaSql == null)
            {
                schemaSql = ResourceLoader.Load<TextAsset>(Config.Resources.databaseSchemaPath)
                    ?? Resources.Load<TextAsset>("Database/tiny_dragon_schema");
            }

            if (seedSql == null)
            {
                seedSql = ResourceLoader.Load<TextAsset>(Config.Resources.databaseSeedPath)
                    ?? Resources.Load<TextAsset>("Database/tiny_dragon_seed");
            }

            if (schemaSql == null)
            {
                Debug.LogWarning("Tiny Dragon schema SQL TextAsset is missing.");
            }
        }
    }

}
