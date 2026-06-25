using System;
using System.Data;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinyDragon.Data
{
    public sealed class TinyDragonSaveManager : MonoBehaviour
    {
        private const string DefaultPlayerId = "player_default";
        private const string DefaultSaveSlotId = "save_slot_1";

        private static TinyDragonSaveManager instance;

        [SerializeField] private TextAsset schemaSql;
        [SerializeField] private TextAsset seedSql;
        [SerializeField] private string databaseFileName = "tiny_dragon.db";
        [SerializeField] private bool saveOnSceneChange = true;
        [SerializeField] private bool saveOnApplicationQuit = true;

        private SqliteDatabase database;
        private bool isReady;

        public static TinyDragonSaveManager Instance
        {
            get
            {
                EnsureInstance();
                return instance;
            }
        }

        public bool IsReady => isReady;

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

            instance = FindAnyObjectByType<TinyDragonSaveManager>();
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
        }

        public void Initialize()
        {
            if (isReady)
            {
                return;
            }

            LoadSqlAssetsIfNeeded();

            database = new SqliteDatabase(DatabasePath);
            if (!database.Open())
            {
                return;
            }

            database.ExecuteScript(schemaSql != null ? schemaSql.text : string.Empty);
            EnsureDatabaseColumns();
            database.ExecuteScript(seedSql != null ? seedSql.text : string.Empty);
            EnsureDefaultPlayer();
            EnsureStarterInventory();
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

            PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>();
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

            playerHealth.transform.position = new Vector3(
                snapshot.Position.x,
                snapshot.Position.y,
                playerHealth.transform.position.z
            );

            playerHealth.transform.localScale = new Vector3(
                Mathf.Abs(playerHealth.transform.localScale.x) * snapshot.FacingDirection,
                playerHealth.transform.localScale.y,
                playerHealth.transform.localScale.z
            );
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
                totalMaxHealth += item.BonusHP;
                totalMaxKi += item.BonusKi;
                totalAtk += item.BonusAtk;
                totalDefense += item.BonusDef;
                totalDamageReduction += item.BonusDamageReductionPercent;
                totalSpeed += item.BonusSpd;
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
            string stageId = GetStageIdForScene(snapshot.SceneName);

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

            string stageId = GetStageIdForScene(sceneName) ?? "stage_guide";
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
                "gold = CASE WHEN gold < 2000 THEN 2000 ELSE gold END, premiumCoin = CASE WHEN premiumCoin < 20 THEN 20 ELSE premiumCoin END, " +
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
                "UPDATE Item SET baseBonusDef = 2, maxUpgrade = 2, spritePath = 'UI/Currency/coin_stack' WHERE id = 'item_cloth_shirt';",
                null
            );

            ExecuteNonQuery(
                "INSERT OR IGNORE INTO Item (id, name, description, iconKey, prefabKey, itemType, slotType, rarity, " +
                "requiredLevel, stackable, maxStack, sellPriceGold, baseBonusHP, maxUpgrade) " +
                "VALUES ('item_black_cloth_pants', 'Quần vải đen', 'Starter cloth pants.', NULL, NULL, 'ARMOR', 'LEG', 'NORMAL', 1, 0, 1, 5, 30, 0);",
                null
            );
            ExecuteNonQuery(
                "UPDATE Item SET baseBonusHP = 30, spritePath = 'UI/Currency/gem_green' WHERE id = 'item_black_cloth_pants';",
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
                totalMaxHealth += item.BonusHP;
            }

            return Mathf.Max(totalMaxHealth, 1);
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
                "SELECT pi.id, i.id, i.name, i.itemType, i.slotType, i.rarity, pi.quantity, pi.upgradeLevel, " +
                "i.baseBonusHP, i.baseBonusKi, i.baseBonusAtk, i.baseBonusDef, " +
                "i.baseBonusCritPercent, i.baseBonusDamageReductionPercent, i.baseBonusCritDamagePercent, i.baseBonusSpd, i.spritePath " +
                "FROM PlayerItem pi INNER JOIN Item i ON i.id = pi.itemId " +
                "WHERE pi.playerId = @playerId ORDER BY pi.acquiredAt, i.name;"
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
                            Quantity = reader.GetInt32(6),
                            UpgradeLevel = reader.GetInt32(7),
                            BonusHP = reader.GetInt32(8),
                            BonusKi = reader.GetInt32(9),
                            BonusAtk = reader.GetInt32(10),
                            BonusDef = reader.GetInt32(11),
                            BonusCritPercent = reader.GetInt32(12),
                            BonusDamageReductionPercent = reader.GetInt32(13),
                            BonusCritDamagePercent = reader.GetInt32(14),
                            BonusSpd = Convert.ToSingle(reader.GetDouble(15)),
                            SpritePath = reader.IsDBNull(16) ? null : reader.GetString(16)
                        });
                    }
                }
            }
        }

        private void LoadPlayerSkills(InventoryViewData inventory)
        {
            using (IDbCommand command = database.CreateCommand(
                "SELECT s.id, s.name, s.description, s.skillType, COALESCE(ps.skillLevel, 0), s.kiCost, s.cooldownSec, s.damageMultiplier, s.iconKey " +
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
                            IconKey = reader.IsDBNull(8) ? null : reader.GetString(8)
                        });
                    }
                }
            }
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

        private bool EnsureReady()
        {
            if (!isReady)
            {
                Initialize();
            }

            return isReady;
        }

        private string GetStageIdForScene(string sceneName)
        {
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
                schemaSql = Resources.Load<TextAsset>("Database/tiny_dragon_schema");
            }

            if (seedSql == null)
            {
                seedSql = Resources.Load<TextAsset>("Database/tiny_dragon_seed");
            }

            if (schemaSql == null)
            {
                Debug.LogWarning("Tiny Dragon schema SQL TextAsset is missing.");
            }
        }
    }

    public struct EnemyBalanceData
    {
        public EnemyBalanceData(
            string id,
            string displayName,
            int baseHP,
            int baseAtk,
            int baseDef,
            float baseSpd,
            int expReward,
            int goldReward,
            int bossGemReward
        )
        {
            Id = id;
            DisplayName = displayName;
            BaseHP = baseHP;
            BaseAtk = baseAtk;
            BaseDef = baseDef;
            BaseSpd = baseSpd;
            ExpReward = expReward;
            GoldReward = goldReward;
            BossGemReward = bossGemReward;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int BaseHP { get; }
        public int BaseAtk { get; }
        public int BaseDef { get; }
        public float BaseSpd { get; }
        public int ExpReward { get; }
        public int GoldReward { get; }
        public int BossGemReward { get; }
    }

    public static class GameplayBalanceDefaults
    {
        public const int PlayerBaseHealth = 230;
        public const int PlayerBaseKi = 100;
        public const int PlayerBaseAttack = 12;
        public const float PlayerBaseSpeed = 5f;
        public const int PlayerPowerShotDamage = 36;
        public const float PowerShotDamageMultiplier = 3f;
        public const float PowerShotCooldown = 1.2f;
        public const float PowerShotManaCostRatio = 0.5f;

        public const int NormalEnemyHealth = 36;
        public const int NormalEnemyDamage = 15;
        public const float NormalEnemySpeed = 1.5f;
        public const float NormalEnemyAttackCooldown = 1f;
        public const float NormalEnemyRangeAttackCooldown = 2f;

        public const int BossHealth = 180;
        public const int BossMeleeDamage = 25;
        public const int BossEnergyDamage = 20;
        public const float BossSpeed = 2f;
        public const int BossGoldReward = 20;
    }
}
