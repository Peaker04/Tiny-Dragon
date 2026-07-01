namespace TinyDragon.Data
{
    internal readonly struct PlayerStatUpgradeRule
    {
        public PlayerStatUpgradeRule(string columnName, int cost, int increment, bool increasesCurrentValue)
        {
            ColumnName = columnName;
            Cost = cost;
            Increment = increment;
            IncreasesCurrentValue = increasesCurrentValue;
        }

        public string ColumnName { get; }
        public int Cost { get; }
        public int Increment { get; }
        public bool IncreasesCurrentValue { get; }
    }

    internal static class PlayerStatUpgradeRules
    {
        public static bool TryCreate(
            string statType,
            int hp,
            int ki,
            int atk,
            int def,
            int crit,
            out PlayerStatUpgradeRule rule)
        {
            switch (statType)
            {
                case "HP":
                    rule = new PlayerStatUpgradeRule("baseHP", hp * 10, 20, true);
                    return true;
                case "KI":
                    rule = new PlayerStatUpgradeRule("baseKi", ki * 10, 20, true);
                    return true;
                case "ATK":
                    rule = new PlayerStatUpgradeRule("baseAtk", atk * 100, 1, false);
                    return true;
                case "DEF":
                    rule = new PlayerStatUpgradeRule("baseDef", (def + 1) * 500000, 1, false);
                    return true;
                case "CRIT":
                    rule = new PlayerStatUpgradeRule("baseCritPercent", (crit + 1) * 50000000, 1, false);
                    return true;
                default:
                    rule = default;
                    return false;
            }
        }

        public static string CurrentValueColumnFor(string statType)
        {
            return statType == "HP" ? "currentHP" : statType == "KI" ? "currentKi" : null;
        }
    }
}
