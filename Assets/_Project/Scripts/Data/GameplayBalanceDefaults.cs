namespace TinyDragon.Data
{
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
