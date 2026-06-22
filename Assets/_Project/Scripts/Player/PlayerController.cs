using UnityEngine;

[RequireComponent(typeof(PlayerInputReader))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerAnimatorDriver))]
[RequireComponent(typeof(PlayerAttack))]
[RequireComponent(typeof(ProjectileShooter))]
[RequireComponent(typeof(PlayerSceneTransition))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private ProjectileShooter projectileShooter;

    private void Awake()
    {
        if (projectileShooter == null)
        {
            projectileShooter = GetComponent<ProjectileShooter>();
        }
    }

    public void ShootProjectile()
    {
        projectileShooter?.Shoot();
    }
}
