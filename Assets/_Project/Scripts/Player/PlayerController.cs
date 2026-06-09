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
    private PlayerInputReader inputReader;

    [Header("Tutorial State")]
    [SerializeField] private bool _canMove = true;
    [SerializeField] private bool _canJump = true;
    [SerializeField] private bool _canAttack = true;

    public bool canMove 
    { 
        get => _canMove; 
        set { _canMove = value; UpdateInputReader(); } 
    }
    public bool canJump 
    { 
        get => _canJump; 
        set { _canJump = value; UpdateInputReader(); } 
    }
    public bool canAttack 
    { 
        get => _canAttack; 
        set { _canAttack = value; UpdateInputReader(); } 
    }

    private void Awake()
    {
        if (projectileShooter == null)
        {
            projectileShooter = GetComponent<ProjectileShooter>();
        }
        inputReader = GetComponent<PlayerInputReader>();
    }

    private void Start()
    {
        UpdateInputReader();
    }

    private void UpdateInputReader()
    {
        if (inputReader != null)
        {
            inputReader.EnableMovement(_canMove);
            inputReader.EnableJump(_canJump);
            inputReader.EnableAttack(_canAttack);
            inputReader.EnablePowerShot(_canAttack);
        }
    }

    public void ShootProjectile()
    {
        projectileShooter?.Shoot();
    }
}
