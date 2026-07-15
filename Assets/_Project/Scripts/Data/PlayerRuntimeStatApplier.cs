using UnityEngine;

namespace TinyDragon.Data
{
    internal readonly struct PlayerRuntimeStats
    {
        private PlayerRuntimeStats(
            int maxHealth,
            int maxKi,
            int attack,
            int defense,
            int damageReduction,
            float speed,
            int currentKi,
            float attackSpeed)
        {
            MaxHealth = maxHealth;
            MaxKi = maxKi;
            Attack = attack;
            Defense = defense;
            DamageReduction = damageReduction;
            Speed = speed;
            CurrentKi = currentKi;
            AttackSpeed = attackSpeed;
        }

        public int MaxHealth { get; }
        public int MaxKi { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int DamageReduction { get; }
        public float Speed { get; }
        public int CurrentKi { get; }
        public float AttackSpeed { get; }

        public static PlayerRuntimeStats FromInventory(InventoryViewData inventory)
        {
            int maxHealth = inventory.BaseHP;
            int maxKi = inventory.BaseKi;
            int attack = inventory.BaseAtk;
            int defense = inventory.BaseDef;
            int damageReduction = inventory.BaseDamageReductionPercent;
            float speed = inventory.BaseSpd;

            foreach (InventoryItemViewData item in inventory.Items)
            {
                if (!ShouldApplyItemStats(item))
                {
                    continue;
                }

                maxHealth += item.BonusHP;
                maxKi += item.BonusKi;
                attack += item.BonusAtk;
                defense += item.BonusDef;
                damageReduction += item.BonusDamageReductionPercent;
                speed += item.BonusSpd;
            }

            return new PlayerRuntimeStats(
                maxHealth,
                maxKi,
                attack,
                defense,
                damageReduction,
                speed,
                inventory.CurrentKi,
                inventory.BaseAttackSpeed);
        }

        private static bool ShouldApplyItemStats(InventoryItemViewData item)
        {
            return item != null
                && item.IsCarried
                && !string.IsNullOrWhiteSpace(item.SlotType)
                && !string.Equals(item.ItemType, "CONSUMABLE", System.StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static class PlayerRuntimeStatApplier
    {
        public static int GetTotalMaxHealth(InventoryViewData inventory)
        {
            if (!HasInventory(inventory))
            {
                return 0;
            }

            return Mathf.Max(PlayerRuntimeStats.FromInventory(inventory).MaxHealth, 1);
        }

        public static void Apply(GameObject playerObject, InventoryViewData inventory)
        {
            if (playerObject == null || !HasInventory(inventory))
            {
                return;
            }

            PlayerRuntimeStats stats = PlayerRuntimeStats.FromInventory(inventory);

            PlayerHealth playerHealth = playerObject.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.ApplyRuntimeStats(stats.MaxHealth, stats.Defense, stats.DamageReduction);
            }

            PlayerMovement movement = playerObject.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                movement.ApplyMoveSpeed(stats.Speed);
            }

            ProjectileShooter projectileShooter = playerObject.GetComponent<ProjectileShooter>();
            if (projectileShooter != null)
            {
                projectileShooter.ApplyProjectileDamage(stats.Attack);
                projectileShooter.ApplyPowerShotDamage(
                    Mathf.RoundToInt(stats.Attack * GameplayBalanceDefaults.PowerShotDamageMultiplier)
                );
            }

            PlayerAttack playerAttack = playerObject.GetComponent<PlayerAttack>();
            if (playerAttack != null)
            {
                playerAttack.ApplyAttackCooldown(1f / Mathf.Max(stats.AttackSpeed, 0.1f));
                playerAttack.ApplyPowerShotTuning(
                    GameplayBalanceDefaults.PowerShotCooldown,
                    GameplayBalanceDefaults.PowerShotManaCostRatio
                );
                playerAttack.RestoreMana(stats.CurrentKi, stats.MaxKi);
            }

            // [Bug#1] Đồng bộ ATK stat từ inventory vào PlayerComboAttack.baseDamage
            // - Nếu không có dòng này, punch/kick luôn gây damage mặc định = 12 (GameplayBalanceDefaults.PlayerBaseAttack)
            // - Dù người chơi mặc item +ATK, combo damage vẫn không tăng lên
            // Luồng: PlayerRuntimeStatApplier.Apply() -> stats.Attack (từ inventory + items bonus)
            //     -> PlayerComboAttack.ApplyBaseDamage(stats.Attack) -> baseDamage = Mathf.Max(attack, 1)
            //     -> sau đó PlayerComboAttack.TryExecutePunch/Kick() dùng baseDamage này để gọi meleeHitbox.DealDamage()
            PlayerComboAttack comboAttack = playerObject.GetComponent<PlayerComboAttack>();
            if (comboAttack != null)
            {
                comboAttack.ApplyBaseDamage(stats.Attack);
            }
        }

        private static bool HasInventory(InventoryViewData inventory)
        {
            return inventory != null && !string.IsNullOrWhiteSpace(inventory.DisplayName);
        }
    }
}
