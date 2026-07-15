-- ============================================================
-- TINY DRAGON - OFFLINE SQLITE DATABASE SCHEMA v1.1
-- Unity 2D offline save + RPG data model
-- ============================================================

PRAGMA foreign_keys = ON;

-- ============================================================
-- 0. SCHEMA VERSION
-- ============================================================
CREATE TABLE IF NOT EXISTS SchemaVersion (
  id          INTEGER PRIMARY KEY CHECK(id = 1),
  version     INTEGER NOT NULL CHECK(version >= 1),
  appliedAt   TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

INSERT OR IGNORE INTO SchemaVersion (id, version) VALUES (1, 2);

-- ============================================================
-- 1. PLAYER + SAVE SLOT
-- ============================================================
CREATE TABLE IF NOT EXISTS Player (
  id               TEXT PRIMARY KEY,
  displayName      TEXT NOT NULL DEFAULT 'Player',
  level            INTEGER NOT NULL DEFAULT 1 CHECK(level >= 1),
  exp              INTEGER NOT NULL DEFAULT 0 CHECK(exp >= 0),
  gold             INTEGER NOT NULL DEFAULT 0 CHECK(gold >= 0),
  premiumCoin      INTEGER NOT NULL DEFAULT 0 CHECK(premiumCoin >= 0),
  bossGem          INTEGER NOT NULL DEFAULT 0 CHECK(bossGem >= 0),
  baseHP           INTEGER NOT NULL DEFAULT 230 CHECK(baseHP > 0),
  currentHP        INTEGER NOT NULL DEFAULT 230 CHECK(currentHP >= 0),
  baseKi           INTEGER NOT NULL DEFAULT 100 CHECK(baseKi >= 0),
  currentKi        INTEGER NOT NULL DEFAULT 100 CHECK(currentKi >= 0),
  baseAtk          INTEGER NOT NULL DEFAULT 12 CHECK(baseAtk >= 0),
  baseDef          INTEGER NOT NULL DEFAULT 0 CHECK(baseDef >= 0),
  baseCritPercent  INTEGER NOT NULL DEFAULT 0 CHECK(baseCritPercent >= 0),
  baseDamageReductionPercent INTEGER NOT NULL DEFAULT 0 CHECK(baseDamageReductionPercent >= 0),
  baseCritDamagePercent INTEGER NOT NULL DEFAULT 0 CHECK(baseCritDamagePercent >= 0),
  baseAttackSpeed   REAL NOT NULL DEFAULT 1.0 CHECK(baseAttackSpeed > 0),
  baseSpd          REAL NOT NULL DEFAULT 5.0 CHECK(baseSpd >= 0),
  currentStageId   TEXT,
  currentSceneName TEXT NOT NULL DEFAULT 'Level_01_Origin',
  positionX        REAL NOT NULL DEFAULT 0,
  positionY        REAL NOT NULL DEFAULT 0,
  facingDirection  INTEGER NOT NULL DEFAULT 1 CHECK(facingDirection IN (-1, 1)),
  totalPlayTimeSec INTEGER NOT NULL DEFAULT 0 CHECK(totalPlayTimeSec >= 0),
  createdAt        TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updatedAt        TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (currentStageId) REFERENCES Stage(id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS SaveSlot (
  id        TEXT PRIMARY KEY,
  playerId  TEXT NOT NULL,
  slotIndex INTEGER NOT NULL CHECK(slotIndex BETWEEN 1 AND 3),
  saveName  TEXT,
  savedAt   TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE(slotIndex),
  FOREIGN KEY (playerId) REFERENCES Player(id) ON DELETE CASCADE
);

-- ============================================================
-- 2. WORLD / STAGE
-- ============================================================
CREATE TABLE IF NOT EXISTS Zone (
  id               TEXT PRIMARY KEY,
  name             TEXT NOT NULL,
  description      TEXT,
  orderIndex       INTEGER NOT NULL CHECK(orderIndex >= 0),
  minLevelRequired INTEGER NOT NULL DEFAULT 1 CHECK(minLevelRequired >= 1),
  backgroundKey    TEXT,
  isActive         INTEGER NOT NULL DEFAULT 1 CHECK(isActive IN (0,1))
);

CREATE TABLE IF NOT EXISTS Stage (
  id                 TEXT PRIMARY KEY,
  zoneId             TEXT NOT NULL,
  stageType          TEXT NOT NULL DEFAULT 'NORMAL'
    CHECK(stageType IN ('NORMAL','GUIDE','BOSS','HIDDEN','FINAL_BOSS')),
  name               TEXT NOT NULL,
  description        TEXT,
  sceneName          TEXT NOT NULL,
  orderIndex         INTEGER NOT NULL CHECK(orderIndex >= 0),
  parentStageId      TEXT,
  requiredItemId     TEXT,
  consumeItemOnEntry INTEGER NOT NULL DEFAULT 0 CHECK(consumeItemOnEntry IN (0,1)),
  minLevelRequired   INTEGER NOT NULL DEFAULT 1 CHECK(minLevelRequired >= 1),
  rewardExp          INTEGER NOT NULL DEFAULT 0 CHECK(rewardExp >= 0),
  rewardGold         INTEGER NOT NULL DEFAULT 0 CHECK(rewardGold >= 0),
  rewardBossGem      INTEGER NOT NULL DEFAULT 0 CHECK(rewardBossGem >= 0),
  bgmKey             TEXT,
  backgroundKey      TEXT,
  isActive           INTEGER NOT NULL DEFAULT 1 CHECK(isActive IN (0,1)),
  UNIQUE(sceneName),
  FOREIGN KEY (zoneId) REFERENCES Zone(id) ON DELETE CASCADE,
  FOREIGN KEY (parentStageId) REFERENCES Stage(id) ON DELETE SET NULL,
  FOREIGN KEY (requiredItemId) REFERENCES Item(id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_stage_zone ON Stage(zoneId, orderIndex);

CREATE TABLE IF NOT EXISTS PlayerProgress (
  id             TEXT PRIMARY KEY,
  playerId       TEXT NOT NULL,
  stageId        TEXT NOT NULL,
  isUnlocked     INTEGER NOT NULL DEFAULT 0 CHECK(isUnlocked IN (0,1)),
  isCleared      INTEGER NOT NULL DEFAULT 0 CHECK(isCleared IN (0,1)),
  starRating     INTEGER NOT NULL DEFAULT 0 CHECK(starRating BETWEEN 0 AND 3),
  attemptCount   INTEGER NOT NULL DEFAULT 0 CHECK(attemptCount >= 0),
  deathCount     INTEGER NOT NULL DEFAULT 0 CHECK(deathCount >= 0),
  bestTimeSec    INTEGER CHECK(bestTimeSec IS NULL OR bestTimeSec >= 0),
  firstClearedAt TEXT,
  lastAttemptAt  TEXT,
  UNIQUE(playerId, stageId),
  FOREIGN KEY (playerId) REFERENCES Player(id) ON DELETE CASCADE,
  FOREIGN KEY (stageId) REFERENCES Stage(id) ON DELETE CASCADE
);

-- ============================================================
-- 3. ITEMS / INVENTORY / EQUIPMENT
-- ============================================================
CREATE TABLE IF NOT EXISTS Item (
  id                TEXT PRIMARY KEY,
  name              TEXT NOT NULL,
  description       TEXT,
  iconKey           TEXT,
  spritePath        TEXT,
  prefabKey         TEXT,
  itemType          TEXT NOT NULL
    CHECK(itemType IN ('WEAPON','ARMOR','CONSUMABLE','MATERIAL','BOSS_GEM','RADAR','DRAGON_BALL','KEY','PART','VIP_GEAR')),
  slotType          TEXT CHECK(slotType IS NULL OR slotType IN ('HEAD','BODY','LEG','BAG','AURA','WEAPON')),
  rarity            TEXT NOT NULL DEFAULT 'NORMAL'
    CHECK(rarity IN ('NORMAL','RARE','EPIC','LEGENDARY','VIP')),
  dragonBallNumber  INTEGER CHECK(dragonBallNumber IS NULL OR dragonBallNumber BETWEEN 1 AND 7),
  requiredLevel     INTEGER NOT NULL DEFAULT 1 CHECK(requiredLevel >= 1),
  requiredKeyItemId TEXT,
  stackable         INTEGER NOT NULL DEFAULT 1 CHECK(stackable IN (0,1)),
  maxStack          INTEGER NOT NULL DEFAULT 999 CHECK(maxStack >= 1),
  sellPriceGold     INTEGER NOT NULL DEFAULT 0 CHECK(sellPriceGold >= 0),
  baseBonusHP       INTEGER NOT NULL DEFAULT 0,
  baseBonusKi       INTEGER NOT NULL DEFAULT 0,
  baseBonusAtk      INTEGER NOT NULL DEFAULT 0,
  baseBonusDef      INTEGER NOT NULL DEFAULT 0,
  baseBonusCritPercent INTEGER NOT NULL DEFAULT 0,
  baseBonusDamageReductionPercent INTEGER NOT NULL DEFAULT 0,
  baseBonusCritDamagePercent INTEGER NOT NULL DEFAULT 0,
  baseBonusSpd      REAL NOT NULL DEFAULT 0,
  maxUpgrade        INTEGER NOT NULL DEFAULT 0 CHECK(maxUpgrade >= 0),
  isActive          INTEGER NOT NULL DEFAULT 1 CHECK(isActive IN (0,1)),
  CHECK((stackable = 1 AND maxStack >= 1) OR (stackable = 0 AND maxStack = 1)),
  FOREIGN KEY (requiredKeyItemId) REFERENCES Item(id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_item_type ON Item(itemType, rarity);

CREATE TABLE IF NOT EXISTS ItemUpgradeCost (
  id               TEXT PRIMARY KEY,
  itemId           TEXT NOT NULL,
  upgradeLevel     INTEGER NOT NULL CHECK(upgradeLevel >= 1),
  costGold         INTEGER NOT NULL DEFAULT 0 CHECK(costGold >= 0),
  costBossGem      INTEGER NOT NULL DEFAULT 0 CHECK(costBossGem >= 0),
  materialItemId   TEXT,
  materialQuantity INTEGER NOT NULL DEFAULT 0 CHECK(materialQuantity >= 0),
  bonusHPAdded     INTEGER NOT NULL DEFAULT 0,
  bonusKiAdded     INTEGER NOT NULL DEFAULT 0,
  bonusAtkAdded    INTEGER NOT NULL DEFAULT 0,
  bonusDefAdded    INTEGER NOT NULL DEFAULT 0,
  bonusSpdAdded    REAL NOT NULL DEFAULT 0,
  UNIQUE(itemId, upgradeLevel),
  FOREIGN KEY (itemId) REFERENCES Item(id) ON DELETE CASCADE,
  FOREIGN KEY (materialItemId) REFERENCES Item(id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS PlayerItem (
  id             TEXT PRIMARY KEY,
  playerId       TEXT NOT NULL,
  itemId         TEXT NOT NULL,
  quantity       INTEGER NOT NULL DEFAULT 1 CHECK(quantity >= 1),
  upgradeLevel   INTEGER NOT NULL DEFAULT 0 CHECK(upgradeLevel >= 0),
  dynamicOptions TEXT,
  acquiredAt     TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updatedAt      TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (playerId) REFERENCES Player(id) ON DELETE CASCADE,
  FOREIGN KEY (itemId) REFERENCES Item(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_playeritem_player ON PlayerItem(playerId);
CREATE INDEX IF NOT EXISTS idx_playeritem_item ON PlayerItem(itemId);

CREATE TABLE IF NOT EXISTS PlayerEquipment (
  id           TEXT PRIMARY KEY,
  playerId     TEXT NOT NULL,
  slotType     TEXT NOT NULL CHECK(slotType IN ('HEAD','BODY','LEG','BAG','AURA','WEAPON')),
  playerItemId TEXT NOT NULL,
  equippedAt   TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE(playerId, slotType),
  UNIQUE(playerItemId),
  FOREIGN KEY (playerId) REFERENCES Player(id) ON DELETE CASCADE,
  FOREIGN KEY (playerItemId) REFERENCES PlayerItem(id) ON DELETE CASCADE
);

-- ============================================================
-- 4. SKILLS
-- ============================================================
CREATE TABLE IF NOT EXISTS Skill (
  id                TEXT PRIMARY KEY,
  name              TEXT NOT NULL,
  description       TEXT,
  iconKey           TEXT,
  skillType         TEXT NOT NULL DEFAULT 'ACTIVE'
    CHECK(skillType IN ('ACTIVE','PASSIVE','ULTIMATE')),
  kiCost            INTEGER NOT NULL DEFAULT 0 CHECK(kiCost >= 0),
  cooldownSec       REAL NOT NULL DEFAULT 0 CHECK(cooldownSec >= 0),
  damageMultiplier  REAL NOT NULL DEFAULT 1.0 CHECK(damageMultiplier >= 0),
  effectType        TEXT CHECK(effectType IS NULL OR effectType IN ('DAMAGE','HEAL','BUFF','DEBUFF','SUMMON')),
  effectValue       INTEGER NOT NULL DEFAULT 0,
  effectDurationSec REAL NOT NULL DEFAULT 0 CHECK(effectDurationSec >= 0),
  projectilePrefabKey TEXT
);

CREATE TABLE IF NOT EXISTS PlayerSkill (
  id         TEXT PRIMARY KEY,
  playerId   TEXT NOT NULL,
  skillId    TEXT NOT NULL,
  skillLevel INTEGER NOT NULL DEFAULT 1 CHECK(skillLevel >= 1),
  isEquipped INTEGER NOT NULL DEFAULT 0 CHECK(isEquipped IN (0,1)),
  slotIndex  INTEGER CHECK(slotIndex IS NULL OR slotIndex BETWEEN 1 AND 4),
  unlockedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE(playerId, skillId),
  UNIQUE(playerId, slotIndex),
  CHECK((isEquipped = 1 AND slotIndex IS NOT NULL) OR (isEquipped = 0)),
  FOREIGN KEY (playerId) REFERENCES Player(id) ON DELETE CASCADE,
  FOREIGN KEY (skillId) REFERENCES Skill(id) ON DELETE CASCADE
);

-- ============================================================
-- 5. ENEMIES / SPAWNS / DROPS
-- ============================================================
CREATE TABLE IF NOT EXISTS Enemy (
  id            TEXT PRIMARY KEY,
  name          TEXT NOT NULL,
  description   TEXT,
  iconKey       TEXT,
  prefabKey     TEXT NOT NULL,
  enemyType     TEXT NOT NULL CHECK(enemyType IN ('MELEE','RANGED','FLYING','MINIBOSS','BOSS')),
  level         INTEGER NOT NULL DEFAULT 1 CHECK(level >= 1),
  baseHP        INTEGER NOT NULL CHECK(baseHP > 0),
  baseAtk       INTEGER NOT NULL CHECK(baseAtk >= 0),
  baseDef       INTEGER NOT NULL DEFAULT 0 CHECK(baseDef >= 0),
  baseSpd       REAL NOT NULL DEFAULT 1.0 CHECK(baseSpd >= 0),
  expReward     INTEGER NOT NULL DEFAULT 0 CHECK(expReward >= 0),
  goldReward    INTEGER NOT NULL DEFAULT 0 CHECK(goldReward >= 0),
  bossGemReward INTEGER NOT NULL DEFAULT 0 CHECK(bossGemReward >= 0)
);

CREATE TABLE IF NOT EXISTS EnemySkill (
  id        TEXT PRIMARY KEY,
  enemyId   TEXT NOT NULL,
  skillId   TEXT NOT NULL,
  useChance REAL NOT NULL DEFAULT 0.3 CHECK(useChance >= 0 AND useChance <= 1),
  UNIQUE(enemyId, skillId),
  FOREIGN KEY (enemyId) REFERENCES Enemy(id) ON DELETE CASCADE,
  FOREIGN KEY (skillId) REFERENCES Skill(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS EnemySpawn (
  id          TEXT PRIMARY KEY,
  stageId     TEXT NOT NULL,
  enemyId     TEXT NOT NULL,
  spawnCount  INTEGER NOT NULL DEFAULT 1 CHECK(spawnCount >= 1),
  waveNumber  INTEGER NOT NULL DEFAULT 1 CHECK(waveNumber >= 1),
  maxAlive    INTEGER NOT NULL DEFAULT 3 CHECK(maxAlive >= 1),
  spawnDelay  REAL NOT NULL DEFAULT 2.0 CHECK(spawnDelay >= 0),
  isBoss      INTEGER NOT NULL DEFAULT 0 CHECK(isBoss IN (0,1)),
  spawnPoint  TEXT,
  FOREIGN KEY (stageId) REFERENCES Stage(id) ON DELETE CASCADE,
  FOREIGN KEY (enemyId) REFERENCES Enemy(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_spawn_stage ON EnemySpawn(stageId, waveNumber);

CREATE TABLE IF NOT EXISTS DropRate (
  id                TEXT PRIMARY KEY,
  enemyId           TEXT NOT NULL,
  itemId            TEXT NOT NULL,
  dropChance        REAL NOT NULL CHECK(dropChance >= 0 AND dropChance <= 1),
  minQuantity       INTEGER NOT NULL DEFAULT 1 CHECK(minQuantity >= 1),
  maxQuantity       INTEGER NOT NULL DEFAULT 1 CHECK(maxQuantity >= 1),
  dynamicOptionPool TEXT,
  CHECK(minQuantity <= maxQuantity),
  FOREIGN KEY (enemyId) REFERENCES Enemy(id) ON DELETE CASCADE,
  FOREIGN KEY (itemId) REFERENCES Item(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_droprate_enemy ON DropRate(enemyId);

-- ============================================================
-- 6. SHOP
-- ============================================================
CREATE TABLE IF NOT EXISTS ShopItem (
  id               TEXT PRIMARY KEY,
  itemId           TEXT NOT NULL,
  category         TEXT NOT NULL DEFAULT 'GENERAL'
    CHECK(category IN ('GENERAL','EQUIPMENT','CONSUMABLE','VIP','LIMITED')),
  priceGold        INTEGER NOT NULL DEFAULT 0 CHECK(priceGold >= 0),
  priceBossGem     INTEGER NOT NULL DEFAULT 0 CHECK(priceBossGem >= 0),
  pricePremiumCoin INTEGER NOT NULL DEFAULT 0 CHECK(pricePremiumCoin >= 0),
  stock            INTEGER CHECK(stock IS NULL OR stock >= 0),
  soldCount        INTEGER NOT NULL DEFAULT 0 CHECK(soldCount >= 0),
  discountPercent  INTEGER NOT NULL DEFAULT 0 CHECK(discountPercent BETWEEN 0 AND 100),
  availableFrom    TEXT,
  availableTo      TEXT,
  isActive         INTEGER NOT NULL DEFAULT 1 CHECK(isActive IN (0,1)),
  FOREIGN KEY (itemId) REFERENCES Item(id) ON DELETE CASCADE
);

-- ============================================================
-- 7. INTRINSIC / PASSIVE ROLLS
-- ============================================================
CREATE TABLE IF NOT EXISTS Intrinsic (
  id              TEXT PRIMARY KEY,
  name            TEXT NOT NULL,
  description     TEXT NOT NULL,
  intrinsicType   TEXT NOT NULL DEFAULT 'COMBAT'
    CHECK(intrinsicType IN ('COMBAT','EXPLORATION','ECONOMIC','DRAGON_BALL')),
  paramMin        INTEGER NOT NULL,
  paramMax        INTEGER NOT NULL,
  unlockCondition TEXT,
  CHECK(paramMin <= paramMax)
);

CREATE TABLE IF NOT EXISTS PlayerIntrinsic (
  id          TEXT PRIMARY KEY,
  playerId    TEXT NOT NULL,
  intrinsicId TEXT NOT NULL,
  rolledParam INTEGER NOT NULL,
  isActive    INTEGER NOT NULL DEFAULT 0 CHECK(isActive IN (0,1)),
  unlockedAt  TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE(playerId, intrinsicId),
  FOREIGN KEY (playerId) REFERENCES Player(id) ON DELETE CASCADE,
  FOREIGN KEY (intrinsicId) REFERENCES Intrinsic(id) ON DELETE CASCADE
);

-- ============================================================
-- 8. DRAGON BALL / WISH
-- ============================================================
CREATE TABLE IF NOT EXISTS PlayerDragonBall (
  id          TEXT PRIMARY KEY,
  playerId    TEXT NOT NULL,
  ballNumber  INTEGER NOT NULL CHECK(ballNumber BETWEEN 1 AND 7),
  collectedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE(playerId, ballNumber),
  FOREIGN KEY (playerId) REFERENCES Player(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS DragonWish (
  id           TEXT PRIMARY KEY,
  name         TEXT NOT NULL,
  description  TEXT,
  wishType     TEXT NOT NULL
    CHECK(wishType IN ('REVIVE','GOLD','PREMIUM_COIN','UNLOCK_SKILL','STAT_BOOST','RARE_ITEM')),
  effectValue  INTEGER NOT NULL DEFAULT 0,
  effectItemId TEXT,
  cooldownDays INTEGER NOT NULL DEFAULT 7 CHECK(cooldownDays >= 0),
  FOREIGN KEY (effectItemId) REFERENCES Item(id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS PlayerWishHistory (
  id        TEXT PRIMARY KEY,
  playerId  TEXT NOT NULL,
  wishId    TEXT NOT NULL,
  wishedAt  TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (playerId) REFERENCES Player(id) ON DELETE CASCADE,
  FOREIGN KEY (wishId) REFERENCES DragonWish(id) ON DELETE CASCADE
);

-- ============================================================
-- 9. QUESTS / ACHIEVEMENTS
-- ============================================================
CREATE TABLE IF NOT EXISTS Quest (
  id                TEXT PRIMARY KEY,
  name              TEXT NOT NULL,
  description       TEXT,
  questType         TEXT NOT NULL DEFAULT 'MAIN'
    CHECK(questType IN ('MAIN','SIDE','DAILY','WEEKLY','EVENT')),
  requireQuestId    TEXT,
  targetType        TEXT NOT NULL
    CHECK(targetType IN ('KILL_ENEMY','CLEAR_STAGE','COLLECT_ITEM','REACH_LEVEL','COLLECT_DRAGON_BALL')),
  targetEnemyId     TEXT,
  targetStageId     TEXT,
  targetItemId      TEXT,
  targetCount       INTEGER NOT NULL DEFAULT 1 CHECK(targetCount >= 1),
  rewardExp         INTEGER NOT NULL DEFAULT 0 CHECK(rewardExp >= 0),
  rewardGold        INTEGER NOT NULL DEFAULT 0 CHECK(rewardGold >= 0),
  rewardPremiumCoin INTEGER NOT NULL DEFAULT 0 CHECK(rewardPremiumCoin >= 0),
  rewardItemId      TEXT,
  rewardItemQty     INTEGER NOT NULL DEFAULT 0 CHECK(rewardItemQty >= 0),
  FOREIGN KEY (requireQuestId) REFERENCES Quest(id) ON DELETE SET NULL,
  FOREIGN KEY (targetEnemyId) REFERENCES Enemy(id) ON DELETE SET NULL,
  FOREIGN KEY (targetStageId) REFERENCES Stage(id) ON DELETE SET NULL,
  FOREIGN KEY (targetItemId) REFERENCES Item(id) ON DELETE SET NULL,
  FOREIGN KEY (rewardItemId) REFERENCES Item(id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS PlayerQuest (
  id           TEXT PRIMARY KEY,
  playerId     TEXT NOT NULL,
  questId      TEXT NOT NULL,
  status       TEXT NOT NULL DEFAULT 'ACTIVE' CHECK(status IN ('ACTIVE','COMPLETED','CLAIMED')),
  currentCount INTEGER NOT NULL DEFAULT 0 CHECK(currentCount >= 0),
  startedAt    TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  completedAt  TEXT,
  claimedAt    TEXT,
  UNIQUE(playerId, questId),
  FOREIGN KEY (playerId) REFERENCES Player(id) ON DELETE CASCADE,
  FOREIGN KEY (questId) REFERENCES Quest(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS Achievement (
  id                TEXT PRIMARY KEY,
  name              TEXT NOT NULL,
  description       TEXT,
  iconKey           TEXT,
  achievementType   TEXT NOT NULL
    CHECK(achievementType IN ('KILL','CLEAR','COLLECT','LEVEL','DRAGON_BALL','GACHA')),
  targetCount       INTEGER NOT NULL DEFAULT 1 CHECK(targetCount >= 1),
  rewardExp         INTEGER NOT NULL DEFAULT 0 CHECK(rewardExp >= 0),
  rewardGold        INTEGER NOT NULL DEFAULT 0 CHECK(rewardGold >= 0),
  rewardPremiumCoin INTEGER NOT NULL DEFAULT 0 CHECK(rewardPremiumCoin >= 0)
);

CREATE TABLE IF NOT EXISTS PlayerAchievement (
  id            TEXT PRIMARY KEY,
  playerId      TEXT NOT NULL,
  achievementId TEXT NOT NULL,
  currentCount  INTEGER NOT NULL DEFAULT 0 CHECK(currentCount >= 0),
  isCompleted   INTEGER NOT NULL DEFAULT 0 CHECK(isCompleted IN (0,1)),
  isClaimed     INTEGER NOT NULL DEFAULT 0 CHECK(isClaimed IN (0,1)),
  completedAt   TEXT,
  UNIQUE(playerId, achievementId),
  FOREIGN KEY (playerId) REFERENCES Player(id) ON DELETE CASCADE,
  FOREIGN KEY (achievementId) REFERENCES Achievement(id) ON DELETE CASCADE
);

-- ============================================================
-- 10. TRIGGERS
-- ============================================================
CREATE TRIGGER IF NOT EXISTS trg_player_updatedAt
AFTER UPDATE ON Player
FOR EACH ROW
WHEN NEW.updatedAt = OLD.updatedAt
BEGIN
  UPDATE Player SET updatedAt = CURRENT_TIMESTAMP WHERE id = OLD.id;
END;

CREATE TRIGGER IF NOT EXISTS trg_playeritem_updatedAt
AFTER UPDATE ON PlayerItem
FOR EACH ROW
WHEN NEW.updatedAt = OLD.updatedAt
BEGIN
  UPDATE PlayerItem SET updatedAt = CURRENT_TIMESTAMP WHERE id = OLD.id;
END;
