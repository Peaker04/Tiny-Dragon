using TinyDragon.Config;
using TinyDragon.Data;
using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public sealed class EnemyGoldDropper : MonoBehaviour
{
    [SerializeField] private TinyDragonRuntimeConfig runtimeConfig;

    private EnemyHealth enemyHealth;
    private bool hasDropped;
    private TinyDragonRuntimeConfig Config => TinyDragonRuntimeConfigProvider.Resolve(runtimeConfig);

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
    }

    private void OnEnable()
    {
        if (enemyHealth == null)
        {
            enemyHealth = GetComponent<EnemyHealth>();
        }

        hasDropped = false;
        if (enemyHealth != null)
        {
            enemyHealth.Died += HandleEnemyDied;
        }
    }

    private void OnDisable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.Died -= HandleEnemyDied;
        }
    }

    private void HandleEnemyDied(EnemyHealth defeatedEnemy)
    {
        if (hasDropped || defeatedEnemy == null)
        {
            return;
        }

        hasDropped = true;
        if (!TinyDragonSaveManager.Instance.TryLoadEnemyBalance(defeatedEnemy.BalanceEnemyId, out EnemyBalanceData balance) || balance.GoldReward <= 0)
        {
            return;
        }

        Vector3 dropPosition = defeatedEnemy.transform.position + new Vector3(Random.Range(-0.2f, 0.2f), 0.15f, 0f);
        GoldPickup.Create(dropPosition, balance.GoldReward, Config.Resources.goldCoinSpritePath);
    }
}
