using System.Collections.Generic;
using TinyDragon.Data;
using UnityEngine;

namespace TinyDragon.UI
{
    internal static class InventoryPanelFormatting
    {
        public static string BuildItemStat(InventoryItemViewData item)
        {
            if (item.UpgradeLevel > 0)
            {
                return $"Giáp+{item.UpgradeLevel}";
            }

            if (item.BonusHP != 0)
            {
                return $"HP+{item.BonusHP}";
            }

            if (item.BonusAtk != 0)
            {
                return $"Sức đánh+{item.BonusAtk}";
            }

            return item.Quantity > 1 ? $"x{item.Quantity}" : item.ItemType;
        }

        public static string BuildItemStatsDescription(InventoryItemViewData item)
        {
            var parts = new List<string>();
            if (item.BonusHP != 0) parts.Add($"HP +{item.BonusHP}");
            if (item.BonusKi != 0) parts.Add($"KI +{item.BonusKi}");
            if (item.BonusAtk != 0) parts.Add($"Sức đánh +{item.BonusAtk}");
            if (item.BonusDef != 0) parts.Add($"Giáp +{item.BonusDef}");
            if (item.BonusCritPercent != 0) parts.Add($"Chí mạng +{item.BonusCritPercent}%");
            if (item.BonusDamageReductionPercent != 0) parts.Add($"Giảm ST +{item.BonusDamageReductionPercent}%");
            if (item.BonusCritDamagePercent != 0) parts.Add($"Sát thương CM +{item.BonusCritDamagePercent}%");
            if (item.BonusSpd != 0) parts.Add($"Tốc độ +{item.BonusSpd}");

            return parts.Count == 0 ? "Không có thuộc tính cộng thêm." : string.Join("\n", parts);
        }

        public static Color32 GetIconColor(InventoryItemViewData item)
        {
            if (item.SlotType == "LEG")
            {
                return new Color32(49, 61, 78, 255);
            }

            if (item.SlotType == "BODY")
            {
                return new Color32(195, 199, 200, 255);
            }

            return new Color32(118, 132, 146, 255);
        }

        public static string GetUpgradeableBaseStat(int index)
        {
            switch (index)
            {
                case 0:
                    return "HP";
                case 1:
                    return "KI";
                case 2:
                    return "ATK";
                case 3:
                    return "DEF";
                case 4:
                    return "CRIT";
                default:
                    return null;
            }
        }

        public static string BuildSkillDetails(int index, InventoryViewData data)
        {
            if (data == null)
            {
                return string.Empty;
            }

            switch (index)
            {
                case 0:
                    return BuildBaseStatDetails("HP GỐC", "Tăng lượng HP tối đa cơ bản.", data.BaseHP, data.BaseHP * 10, "Tăng +20 HP");
                case 1:
                    return BuildBaseStatDetails("KI GỐC", "Tăng lượng KI tối đa cơ bản.", data.BaseKi, data.BaseKi * 10, "Tăng +20 KI");
                case 2:
                    return BuildBaseStatDetails("SỨC ĐÁNH GỐC", "Tăng sức đánh cơ bản.", data.BaseAtk, data.BaseAtk * 100, "Tăng +1 sức đánh");
                case 3:
                    return BuildBaseStatDetails("GIÁP GỐC", "Tăng giáp phòng thủ cơ bản.", data.BaseDef, (data.BaseDef + 1) * 500000, "Tăng +1 giáp");
                case 4:
                    return BuildBaseStatDetails("CHÍ MẠNG GỐC", "Tăng tỷ lệ chí mạng cơ bản.", data.BaseCritPercent, (data.BaseCritPercent + 1) * 50000000, "Tăng +1% chí mạng", "%");
                default:
                    return BuildCombatSkillDetails(index, data);
            }
        }

        private static string BuildBaseStatDetails(
            string title,
            string description,
            int currentValue,
            int cost,
            string upgradeText,
            string valueSuffix = "")
        {
            return $"[{title}]\n" +
                   $"{description}\n" +
                   $"Cấp hiện tại: {currentValue}{valueSuffix}\n" +
                   $"Chi phí nâng cấp: {cost:N0} tiềm năng ({upgradeText})\n" +
                   $"Bấm lần nữa để xác nhận Nâng Cấp.";
        }

        private static string BuildCombatSkillDetails(int index, InventoryViewData data)
        {
            if (index < 5)
            {
                return string.Empty;
            }

            int skillIndex = index - 5;
            if (skillIndex >= data.CombatSkills.Count)
            {
                return string.Empty;
            }

            InventorySkillViewData skill = data.CombatSkills[skillIndex];
            int cost = (skill.SkillLevel + 1) * 5000;
            return $"[{skill.Name.ToUpper()} - CẤP {skill.SkillLevel}]\n" +
                   $"Mô tả: {skill.Description}\n" +
                   $"KI hao tổn: {skill.KiCost} | Hồi chiêu: {skill.CooldownSec}s\n" +
                   $"Sát thương: {skill.DamageMultiplier * 100}%\n" +
                   $"Chi phí nâng cấp: {cost:N0} tiềm năng\n" +
                   $"Bấm lần nữa để nâng cấp.";
        }
    }
}
