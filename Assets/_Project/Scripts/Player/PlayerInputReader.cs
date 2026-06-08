using UnityEngine;

public class PlayerInputReader : MonoBehaviour
{
    [SerializeField] private KeyCode attackKey = KeyCode.J;
    [SerializeField] private KeyCode powerShotKey = KeyCode.K;
    [SerializeField] private bool movementInputEnabled = true;
    [SerializeField] private bool jumpInputEnabled = true;
    [SerializeField] private bool attackInputEnabled = true;
    [SerializeField] private bool powerShotInputEnabled = true;

    public float Horizontal { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool AttackPressed { get; private set; }
    public bool PowerShotPressed { get; private set; }

    private void Update()
    {
        Horizontal = movementInputEnabled ? ReadHorizontalInput() : 0f;

        if (jumpInputEnabled && (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space)))
        {
            JumpPressed = true;
        }

        if (attackInputEnabled && Input.GetKeyDown(attackKey))
        {
            AttackPressed = true;
        }

        if (powerShotInputEnabled && Input.GetKeyDown(powerShotKey))
        {
            PowerShotPressed = true;
        }
    }

    public void SetInputEnabled(bool movement, bool jump, bool attack, bool powerShot)
    {
        movementInputEnabled = movement;
        jumpInputEnabled = jump;
        attackInputEnabled = attack;
        powerShotInputEnabled = powerShot;

        if (!movementInputEnabled)
        {
            Horizontal = 0f;
        }

        if (!jumpInputEnabled)
        {
            JumpPressed = false;
        }

        if (!attackInputEnabled)
        {
            AttackPressed = false;
        }

        if (!powerShotInputEnabled)
        {
            PowerShotPressed = false;
        }
    }

    public void ResetInputRestrictions()
    {
        SetInputEnabled(true, true, true, true);
    }

    public bool ConsumeJumpPressed()
    {
        bool wasPressed = JumpPressed;
        JumpPressed = false;
        return wasPressed;
    }

    public bool ConsumeAttackPressed()
    {
        bool wasPressed = AttackPressed;
        AttackPressed = false;
        return wasPressed;
    }

    public bool ConsumePowerShotPressed()
    {
        bool wasPressed = PowerShotPressed;
        PowerShotPressed = false;
        return wasPressed;
    }

    private float ReadHorizontalInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        bool movingLeft = Input.GetKey(KeyCode.LeftArrow);
        bool movingRight = Input.GetKey(KeyCode.RightArrow);

        if (movingLeft == movingRight)
        {
            return horizontal;
        }

        return movingLeft ? -1f : 1f;
    }
}
