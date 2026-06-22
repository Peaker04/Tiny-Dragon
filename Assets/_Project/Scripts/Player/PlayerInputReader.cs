using UnityEngine;

public class PlayerInputReader : MonoBehaviour
{
    [SerializeField] private KeyCode attackKey = KeyCode.J;
    [SerializeField] private KeyCode powerShotKey = KeyCode.K;
    [SerializeField] private KeyCode punchKey = KeyCode.L;
    [SerializeField] private KeyCode kickKey = KeyCode.I;
    [SerializeField] private bool movementInputEnabled = true;
    [SerializeField] private bool jumpInputEnabled = true;
    [SerializeField] private bool attackInputEnabled = true;
    [SerializeField] private bool powerShotInputEnabled = true;
    [SerializeField] private bool punchInputEnabled = true;
    [SerializeField] private bool kickInputEnabled = true;

    public float Horizontal { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool AttackPressed { get; private set; }
    public bool PowerShotPressed { get; private set; }
    public bool PunchPressed { get; private set; }
    public bool KickPressed { get; private set; }

    private void Update()
    {
        Horizontal = movementInputEnabled ? ReadHorizontalInput() : 0f;

        if (jumpInputEnabled && (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W)))
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

        if (punchInputEnabled && Input.GetKeyDown(punchKey))
        {
            PunchPressed = true;
        }

        if (kickInputEnabled && Input.GetKeyDown(kickKey))
        {
            KickPressed = true;
        }
    }

    public void SetInputEnabled(bool movement, bool jump, bool attack, bool powerShot, bool punch, bool kick)
    {
        movementInputEnabled = movement;
        jumpInputEnabled = jump;
        attackInputEnabled = attack;
        powerShotInputEnabled = powerShot;
        punchInputEnabled = punch;
        kickInputEnabled = kick;

        if (!movementInputEnabled) Horizontal = 0f;
        if (!jumpInputEnabled) JumpPressed = false;
        if (!attackInputEnabled) AttackPressed = false;
        if (!powerShotInputEnabled) PowerShotPressed = false;
        if (!punchInputEnabled) PunchPressed = false;
        if (!kickInputEnabled) KickPressed = false;
    }

    public void ResetInputRestrictions()
    {
        SetInputEnabled(true, true, true, true, true, true);
    }

    // --- Feature-specific enable helpers (used by Level03Manager, PauseManager) ---

    public void EnableAll()
    {
        SetInputEnabled(true, true, true, true, true, true);
    }

    public void EnableMovement(bool enabled)
    {
        movementInputEnabled = enabled;
        if (!enabled) Horizontal = 0f;
    }

    public void EnableJump(bool enabled)
    {
        jumpInputEnabled = enabled;
        if (!enabled) JumpPressed = false;
    }

    public void EnableAttack(bool enabled)
    {
        attackInputEnabled = enabled;
        if (!enabled) AttackPressed = false;
    }

    public void EnablePowerShot(bool enabled)
    {
        powerShotInputEnabled = enabled;
        if (!enabled) PowerShotPressed = false;
    }

    /// <summary>
    /// Returns whether a named input feature is currently active.
    /// Supported feature names: "Movement", "Jump", "Attack", "PowerShot", "Punch", "Kick".
    /// Any unrecognised name returns true (fail-open).
    /// </summary>
    public bool IsFeatureEnabled(string featureName)
    {
        return featureName switch
        {
            "Movement"  => movementInputEnabled,
            "Jump"      => jumpInputEnabled,
            "Attack"    => attackInputEnabled,
            "PowerShot" => powerShotInputEnabled,
            "Punch"     => punchInputEnabled,
            "Kick"      => kickInputEnabled,
            _           => true,
        };
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

    public bool ConsumePunchPressed()
    {
        bool wasPressed = PunchPressed;
        PunchPressed = false;
        return wasPressed;
    }

    public bool ConsumeKickPressed()
    {
        bool wasPressed = KickPressed;
        KickPressed = false;
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
