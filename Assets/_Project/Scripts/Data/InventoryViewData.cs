using System.Collections.Generic;

namespace TinyDragon.Data
{
    public sealed class InventoryViewData
    {
        public string DisplayName { get; set; }
        public string AvatarPath { get; set; }
        public int Level { get; set; }
        public int Gold { get; set; }
        public int PremiumCoin { get; set; }
        public int BossGem { get; set; }
        public int CurrentHP { get; set; }
        public int BaseHP { get; set; }
        public int CurrentKi { get; set; }
        public int BaseKi { get; set; }
        public int BaseAtk { get; set; }
        public int BaseDef { get; set; }
        public int BaseCritPercent { get; set; }
        public int BaseDamageReductionPercent { get; set; }
        public int BaseCritDamagePercent { get; set; }
        public float BaseAttackSpeed { get; set; }
        public float BaseSpd { get; set; }
        public int Exp { get; set; }
        public List<InventoryItemViewData> Items { get; } = new List<InventoryItemViewData>();
        public List<InventoryStatUpgradeViewData> StatUpgrades { get; } = new List<InventoryStatUpgradeViewData>();
        public List<InventorySkillViewData> CombatSkills { get; } = new List<InventorySkillViewData>();
    }

    public sealed class InventoryStatUpgradeViewData
    {
        public string SkillId { get; set; }
        public string StatType { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string IconKey { get; set; }
        public int EffectValue { get; set; }
    }

    public sealed class InventorySkillViewData
    {
        public string SkillId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string IconKey { get; set; }
        public string SkillType { get; set; }
        public int SkillLevel { get; set; }
        public int KiCost { get; set; }
        public float CooldownSec { get; set; }
        public float DamageMultiplier { get; set; }
        public int EffectValue { get; set; }
    }

    public sealed class InventoryItemViewData
    {
        public string PlayerItemId { get; set; }
        public string ItemId { get; set; }
        public string Name { get; set; }
        public string ItemType { get; set; }
        public string SlotType { get; set; }
        public string Rarity { get; set; }
        public int Quantity { get; set; }
        public int UpgradeLevel { get; set; }
        public int BonusHP { get; set; }
        public int BonusKi { get; set; }
        public int BonusAtk { get; set; }
        public int BonusDef { get; set; }
        public int BonusCritPercent { get; set; }
        public int BonusDamageReductionPercent { get; set; }
        public int BonusCritDamagePercent { get; set; }
        public float BonusSpd { get; set; }
        public string SpritePath { get; set; }
        public bool IsCarried { get; set; }
    }
}
