using UnityEngine;

public class PlayerInputReader : MonoBehaviour
{
    [SerializeField] private KeyCode attackKey = KeyCode.J;
    [SerializeField] private KeyCode powerShotKey = KeyCode.K;
    [SerializeField] private KeyCode punchKey = KeyCode.L;
    [SerializeField] private KeyCode kickKey = KeyCode.M;
    [SerializeField] private bool movementInputEnabled = true;
    [SerializeField] private bool jumpInputEnabled = true;
    [SerializeField] private bool attackInputEnabled = true;
    [SerializeField] private bool powerShotInputEnabled = true;
    [SerializeField] private bool punchInputEnabled = true;
    [SerializeField] private bool kickInputEnabled = true;
    [SerializeField] private float inputBufferTime = 0.2f;

    private float jumpBufferTimer;
    private float attackBufferTimer;
    private float powerShotBufferTimer;
    private float punchBufferTimer;
    private float kickBufferTimer;

    public float Horizontal { get; private set; }
    public bool JumpPressed => jumpBufferTimer > 0f;
    public bool AttackPressed => attackBufferTimer > 0f;
    public bool PowerShotPressed => powerShotBufferTimer > 0f;
    public bool PunchPressed => punchBufferTimer > 0f;
    public bool KickPressed => kickBufferTimer > 0f;

    private void Update()
    {
        if (jumpBufferTimer > 0f) jumpBufferTimer -= Time.deltaTime;
        if (attackBufferTimer > 0f) attackBufferTimer -= Time.deltaTime;
        if (powerShotBufferTimer > 0f) powerShotBufferTimer -= Time.deltaTime;
        if (punchBufferTimer > 0f) punchBufferTimer -= Time.deltaTime;
        if (kickBufferTimer > 0f) kickBufferTimer -= Time.deltaTime;

        Horizontal = movementInputEnabled ? ReadHorizontalInput() : 0f;

        if (jumpInputEnabled && (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W)))
        {
            jumpBufferTimer = inputBufferTime;
        }

        if (attackInputEnabled && Input.GetKeyDown(attackKey))
        {
            attackBufferTimer = inputBufferTime;
        }

        if (powerShotInputEnabled && Input.GetKeyDown(powerShotKey))
        {
            powerShotBufferTimer = inputBufferTime;
        }

        if (punchInputEnabled && Input.GetKeyDown(punchKey))
        {
            punchBufferTimer = inputBufferTime;
        }

        if (kickInputEnabled && Input.GetKeyDown(kickKey))
        {
            kickBufferTimer = inputBufferTime;
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
        if (!jumpInputEnabled) jumpBufferTimer = 0f;
        if (!attackInputEnabled) attackBufferTimer = 0f;
        if (!powerShotInputEnabled) powerShotBufferTimer = 0f;
        if (!punchInputEnabled) punchBufferTimer = 0f;
        if (!kickInputEnabled) kickBufferTimer = 0f;
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
        if (!enabled) jumpBufferTimer = 0f;
    }

    public void EnableAttack(bool enabled)
    {
        attackInputEnabled = enabled;
        if (!enabled) attackBufferTimer = 0f;
    }

    public void EnablePowerShot(bool enabled)
    {
        powerShotInputEnabled = enabled;
        if (!enabled) powerShotBufferTimer = 0f;
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
        bool wasPressed = jumpBufferTimer > 0f;
        jumpBufferTimer = 0f;
        return wasPressed;
    }

    public bool ConsumeAttackPressed()
    {
        bool wasPressed = attackBufferTimer > 0f;
        attackBufferTimer = 0f;
        return wasPressed;
    }

    public bool ConsumePowerShotPressed()
    {
        bool wasPressed = powerShotBufferTimer > 0f;
        powerShotBufferTimer = 0f;
        return wasPressed;
    }

    public bool ConsumePunchPressed()
    {
        bool wasPressed = punchBufferTimer > 0f;
        punchBufferTimer = 0f;
        return wasPressed;
    }

    public bool ConsumeKickPressed()
    {
        bool wasPressed = kickBufferTimer > 0f;
        kickBufferTimer = 0f;
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
