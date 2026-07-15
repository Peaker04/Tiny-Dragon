using System.Collections.Generic;
using TinyDragon.Data;
using UnityEngine;

namespace TinyDragon.UI
{
    internal static class InventoryPanelFormatting
    {
        public static string BuildItemStat(InventoryItemViewData item)
        {
            List<string> parts = new List<string>();
            if (item.BonusHP != 0) parts.Add($"HP+{item.BonusHP}");
            if (item.BonusKi != 0) parts.Add($"KI+{item.BonusKi}");
            if (item.BonusAtk != 0) parts.Add($"Tấn công+{item.BonusAtk}");
            if (item.BonusDef != 0) parts.Add($"Giáp+{item.BonusDef}");
            if (item.BonusCritPercent != 0) parts.Add($"Chí mạng+{item.BonusCritPercent}%");
            if (item.BonusSpd != 0) parts.Add($"Tốc độ+{item.BonusSpd:0.##}");

            if (parts.Count > 0)
            {
                return string.Join(", ", parts);
            }

            return item.Quantity > 1 ? $"x{item.Quantity}" : item.ItemType;
        }

        public static string BuildItemStatsDescription(InventoryItemViewData item)
        {
            List<string> parts = new List<string>();
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
    }
}
