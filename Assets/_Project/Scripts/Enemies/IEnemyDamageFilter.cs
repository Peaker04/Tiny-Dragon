using TinyDragon.Combat;

public interface IEnemyDamageFilter
{
    int FilterDamage(int incomingDamage, PlayerDamageSource source);
}
