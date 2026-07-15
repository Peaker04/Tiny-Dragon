-- ============================================================
-- TINY DRAGON - STARTER SEED DATA
-- Run after tiny_dragon_schema.sql.
-- ============================================================

PRAGMA foreign_keys = ON;

INSERT OR IGNORE INTO Zone (
  id, name, description, orderIndex, minLevelRequired, backgroundKey
) VALUES (
  'zone_earth_start', 'Earth Start', 'Starter area for Tiny-Dragon.', 0, 1, 'DragonBall/Level_01/Backgrounds'
);

INSERT OR IGNORE INTO Stage (
  id, zoneId, stageType, name, description, sceneName, orderIndex,
  minLevelRequired, rewardExp, rewardGold
) VALUES
  (
    'stage_guide',
    'zone_earth_start',
    'GUIDE',
    'Training Guide',
    'Tutorial and first movement area.',
    'Level_01_Origin',
    0,
    1,
    0,
    0
  ),
  (
    'stage_level_02',
    'zone_earth_start',
    'BOSS',
    'First Battle',
    'First combat stage with normal enemies and a boss.',
    'Level_02',
    1,
    1,
    50,
    20
  ),
  (
    'stage_level_03',
    'zone_earth_start',
    'BOSS',
    'Level 03',
    'Level 03 combat route.',
    'Level_03',
    2,
    1,
    75,
    30
  ),
  (
    'stage_vegeta_city',
    'zone_earth_start',
    'FINAL_BOSS',
    'Thanh Pho Vegeta',
    'Vegeta city boss battle.',
    'ThanhPhoVegeta',
    3,
    1,
    150,
    60
  );

INSERT OR IGNORE INTO Enemy (
  id, name, description, iconKey, prefabKey, enemyType, level,
  baseHP, baseAtk, baseDef, baseSpd, expReward, goldReward, bossGemReward
) VALUES
  (
    'enemy_monster_1',
    'Monster 1',
    'Basic melee monster.',
    NULL,
    'Prefabs/Enemies/monster_1_walk',
    'MELEE',
    1,
    36,
    15,
    0,
    1.5,
    5,
    2,
    0
  ),
  (
    'enemy_boss_act_1',
    'Boss Act 1',
    'First stage boss.',
    NULL,
    'Prefabs/Enemies/Boss_Act_1',
    'BOSS',
    3,
    180,
    25,
    1,
    2.0,
    30,
    20,
    1
  );

INSERT OR IGNORE INTO EnemySpawn (
  id, stageId, enemyId, spawnCount, waveNumber, maxAlive, spawnDelay, isBoss, spawnPoint
) VALUES
  (
    'spawn_level_02_monster_1',
    'stage_level_02',
    'enemy_monster_1',
    2,
    1,
    2,
    2.0,
    0,
    NULL
  ),
  (
    'spawn_level_02_boss_act_1',
    'stage_level_02',
    'enemy_boss_act_1',
    1,
    2,
    1,
    0,
    1,
    'BossSpawnPoint'
  );

INSERT OR IGNORE INTO Skill (
  id, name, description, iconKey, skillType, kiCost, cooldownSec,
  damageMultiplier, effectType, effectValue, effectDurationSec, projectilePrefabKey
) VALUES
  (
    'skill_basic_blast',
    'Basic Blast',
    'Default projectile attack.',
    NULL,
    'ACTIVE',
    0,
    0.35,
    1.0,
    'DAMAGE',
    12,
    0,
    NULL
  );

INSERT OR IGNORE INTO Skill (
  id, name, description, iconKey, skillType, kiCost, cooldownSec,
  damageMultiplier, effectType, effectValue, effectDurationSec, projectilePrefabKey
) VALUES
  (
    'skill_power_shot',
    'Power Shot',
    'Half-Ki heavy projectile.',
    NULL,
    'ACTIVE',
    50,
    1.2,
    3.0,
    'DAMAGE',
    36,
    0,
    NULL
  );

INSERT OR IGNORE INTO Item (
  id, name, description, iconKey, prefabKey, itemType, slotType, rarity,
  requiredLevel, stackable, maxStack, sellPriceGold
) VALUES
  (
    'item_boss_gem',
    'Boss Gem',
    'Currency dropped by bosses.',
    NULL,
    NULL,
    'BOSS_GEM',
    NULL,
    'RARE',
    1,
    1,
    999,
    0
  ),
  (
    'item_dragon_ball_1',
    'Dragon Ball 1',
    'The first dragon ball.',
    NULL,
    NULL,
    'DRAGON_BALL',
    NULL,
    'LEGENDARY',
    1,
    0,
    1,
    0
  ),
  (
    'item_cloth_shirt',
    'Áo vải 3 lỗ',
    'Starter cloth shirt.',
    NULL,
    NULL,
    'ARMOR',
    'BODY',
    'NORMAL',
    1,
    0,
    1,
    5
  ),
  (
    'item_black_cloth_pants',
    'Quần vải đen',
    'Starter cloth pants.',
    NULL,
    NULL,
    'ARMOR',
    'LEG',
    'NORMAL',
    1,
    0,
    1,
    5
  );

INSERT OR IGNORE INTO DropRate (
  id, enemyId, itemId, dropChance, minQuantity, maxQuantity, dynamicOptionPool
) VALUES
  (
    'drop_boss_act_1_boss_gem',
    'enemy_boss_act_1',
    'item_boss_gem',
    1.0,
    1,
    1,
    NULL
  );

UPDATE Enemy
SET
  baseHP = CASE WHEN baseHP < 36 THEN 36 ELSE baseHP END,
  baseAtk = CASE WHEN baseAtk < 15 THEN 15 ELSE baseAtk END,
  baseSpd = CASE WHEN baseSpd < 1.5 THEN 1.5 ELSE baseSpd END
WHERE id = 'enemy_monster_1';

UPDATE Enemy
SET
  baseHP = CASE WHEN baseHP < 180 THEN 180 ELSE baseHP END,
  baseAtk = CASE WHEN baseAtk < 25 THEN 25 ELSE baseAtk END,
  baseSpd = CASE WHEN baseSpd < 2.0 THEN 2.0 ELSE baseSpd END,
  goldReward = CASE WHEN goldReward < 20 THEN 20 ELSE goldReward END,
  bossGemReward = CASE WHEN bossGemReward < 1 THEN 1 ELSE bossGemReward END
WHERE id = 'enemy_boss_act_1';

UPDATE EnemySpawn
SET spawnCount = 2, maxAlive = 2
WHERE id = 'spawn_level_02_monster_1';

UPDATE Skill
SET effectValue = CASE WHEN effectValue < 12 THEN 12 ELSE effectValue END
WHERE id = 'skill_basic_blast';

UPDATE Skill
SET kiCost = 50, cooldownSec = 1.2, damageMultiplier = 3.0, effectValue = 36
WHERE id = 'skill_power_shot';

UPDATE Item
SET baseBonusDef = CASE WHEN baseBonusDef < 2 THEN 2 ELSE baseBonusDef END,
    maxUpgrade = CASE WHEN maxUpgrade < 2 THEN 2 ELSE maxUpgrade END
WHERE id = 'item_cloth_shirt';

UPDATE Item
SET baseBonusHP = CASE WHEN baseBonusHP < 30 THEN 30 ELSE baseBonusHP END
WHERE id = 'item_black_cloth_pants';
