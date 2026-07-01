namespace TinyDragon.Data
{
    public readonly struct EnemyBalanceData
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
}
