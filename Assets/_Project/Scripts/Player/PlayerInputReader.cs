using UnityEngine;

public class PlayerInputReader : MonoBehaviour
{
    [SerializeField] private KeyCode attackKey = KeyCode.J;
    [SerializeField] private KeyCode powerShotKey = KeyCode.K;
    [SerializeField] private KeyCode punchKey = KeyCode.L;
    [SerializeField] private KeyCode kickKey = KeyCode.M;
    [SerializeField] private KeyCode inventoryKey = KeyCode.B;
    [SerializeField] private KeyCode pauseKey = KeyCode.P;
    [SerializeField] private KeyCode settingsKey = KeyCode.I;

    [SerializeField] private bool movementInputEnabled = true;
    [SerializeField] private bool jumpInputEnabled = true;
    [SerializeField] private bool attackInputEnabled = true;
    [SerializeField] private bool powerShotInputEnabled = true;
    [SerializeField] private bool punchInputEnabled = true;
    [SerializeField] private bool kickInputEnabled = true;
    [SerializeField] public bool inventoryInputEnabled = true;
    [SerializeField] public bool pauseInputEnabled = true;
    [SerializeField] public bool settingsInputEnabled = true;
    [SerializeField] private float inputBufferTime = 0.2f;

    private float jumpBufferTimer;
    private float attackBufferTimer;
    private float powerShotBufferTimer;
    private float punchBufferTimer;
    private float kickBufferTimer;
    private float inventoryBufferTimer;
    private float pauseBufferTimer;
    private float settingsBufferTimer;

    public float Horizontal { get; private set; }
    public bool JumpPressed => jumpBufferTimer > 0f;
    public bool AttackPressed => attackBufferTimer > 0f;
    public bool PowerShotPressed => powerShotBufferTimer > 0f;
    public bool PunchPressed => punchBufferTimer > 0f;
    public bool KickPressed => kickBufferTimer > 0f;
    public bool InventoryPressed => inventoryBufferTimer > 0f;
    public bool PausePressed => pauseBufferTimer > 0f;
    public bool SettingsPressed => settingsBufferTimer > 0f;

    private void Update()
    {
        TickInputBuffers();

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

        if (inventoryInputEnabled && Input.GetKeyDown(inventoryKey))
        {
            inventoryBufferTimer = inputBufferTime;
        }

        if (pauseInputEnabled && Input.GetKeyDown(pauseKey))
        {
            pauseBufferTimer = inputBufferTime;
        }

        if (settingsInputEnabled && Input.GetKeyDown(settingsKey))
        {
            settingsBufferTimer = inputBufferTime;
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

        ClearDisabledGameplayInputs();
    }

    public void SetInputEnabled(bool movement, bool jump, bool attack, bool powerShot, bool punch, bool kick, bool inventory, bool pause, bool settings)
    {
        SetInputEnabled(movement, jump, attack, powerShot, punch, kick);
        inventoryInputEnabled = inventory;
        pauseInputEnabled = pause;
        settingsInputEnabled = settings;

        if (!inventoryInputEnabled) inventoryBufferTimer = 0f;
        if (!pauseInputEnabled) pauseBufferTimer = 0f;
        if (!settingsInputEnabled) settingsBufferTimer = 0f;
    }

    public void ResetInputRestrictions()
    {
        SetInputEnabled(true, true, true, true, true, true, true, true, true);
    }

    public void EnableAll()
    {
        SetInputEnabled(true, true, true, true, true, true, true, true, true);
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

    public void EnablePunch(bool enabled)
    {
        punchInputEnabled = enabled;
        if (!enabled) punchBufferTimer = 0f;
    }

    public void EnableKick(bool enabled)
    {
        kickInputEnabled = enabled;
        if (!enabled) kickBufferTimer = 0f;
    }

    public void EnableInventory(bool enabled)
    {
        inventoryInputEnabled = enabled;
        if (!enabled) inventoryBufferTimer = 0f;
    }

    public void EnablePause(bool enabled)
    {
        pauseInputEnabled = enabled;
        if (!enabled) pauseBufferTimer = 0f;
    }

    public void EnableSettings(bool enabled)
    {
        settingsInputEnabled = enabled;
        if (!enabled) settingsBufferTimer = 0f;
    }

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
            "Inventory" => inventoryInputEnabled,
            "Pause"     => pauseInputEnabled,
            "Settings"  => settingsInputEnabled,
            _           => true,
        };
    }

    public bool IsFeatureEnabled(PlayerInputFeature feature)
    {
        return feature switch
        {
            PlayerInputFeature.Movement  => movementInputEnabled,
            PlayerInputFeature.Jump      => jumpInputEnabled,
            PlayerInputFeature.Attack    => attackInputEnabled,
            PlayerInputFeature.PowerShot => powerShotInputEnabled,
            PlayerInputFeature.Punch     => punchInputEnabled,
            PlayerInputFeature.Kick      => kickInputEnabled,
            PlayerInputFeature.Inventory => inventoryInputEnabled,
            PlayerInputFeature.Pause     => pauseInputEnabled,
            PlayerInputFeature.Settings  => settingsInputEnabled,
            _                            => true,
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

    public bool ConsumeInventoryPressed()
    {
        bool wasPressed = inventoryBufferTimer > 0f;
        inventoryBufferTimer = 0f;
        return wasPressed;
    }

    public bool ConsumePausePressed()
    {
        bool wasPressed = pauseBufferTimer > 0f;
        pauseBufferTimer = 0f;
        return wasPressed;
    }

    public bool ConsumeSettingsPressed()
    {
        bool wasPressed = settingsBufferTimer > 0f;
        settingsBufferTimer = 0f;
        return wasPressed;
    }

    private void TickInputBuffers()
    {
        if (jumpBufferTimer > 0f) jumpBufferTimer -= Time.deltaTime;
        if (attackBufferTimer > 0f) attackBufferTimer -= Time.deltaTime;
        if (powerShotBufferTimer > 0f) powerShotBufferTimer -= Time.deltaTime;
        if (punchBufferTimer > 0f) punchBufferTimer -= Time.deltaTime;
        if (kickBufferTimer > 0f) kickBufferTimer -= Time.deltaTime;
        if (inventoryBufferTimer > 0f) inventoryBufferTimer -= Time.deltaTime;
        if (pauseBufferTimer > 0f) pauseBufferTimer -= Time.deltaTime;
        if (settingsBufferTimer > 0f) settingsBufferTimer -= Time.deltaTime;
    }

    private void ClearDisabledGameplayInputs()
    {
        if (!movementInputEnabled) Horizontal = 0f;
        if (!jumpInputEnabled) jumpBufferTimer = 0f;
        if (!attackInputEnabled) attackBufferTimer = 0f;
        if (!powerShotInputEnabled) powerShotBufferTimer = 0f;
        if (!punchInputEnabled) punchBufferTimer = 0f;
        if (!kickInputEnabled) kickBufferTimer = 0f;
    }

    // [Bug#3] Đọc input di chuyển ngang, ưu tiên analog (controller / WASD) trước
    // - Input.GetAxisRaw("Horizontal") đã bao gồm: A/D, LeftArrow/RightArrow, và controller stick
    // - Bug cũ: arrow keys ghi đè hoàn toàn axis value = ±1 cứng, làm mất analog input từ controller
    // - Fix: dùng Mathf.Abs(horizontal) >= 0.01f để kiểm tra nếu axis đã có input thì dùng axis luôn
    //     -> arrow keys và controller đều qua axis, không cần xử lý riêng arrow keys
    //     -> nếu axis = 0 (không có input nào), mới dùng arrow keys làm fallback
    // Luồng: Update() -> ReadHorizontalInput() -> Input.GetAxisRaw("Horizontal")
    //     -> nếu axis != 0: trả về giá trị analog mượt mà (từ -1 đến 1)
    //     -> nếu axis == 0: kiểm tra arrow keys, trả về ±1 nếu có
    //     -> nếu không có input gì: trả về 0
    private float ReadHorizontalInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(horizontal) >= 0.01f)
        {
            return horizontal;
        }

        if (Input.GetKey(KeyCode.LeftArrow)) return -1f;
        if (Input.GetKey(KeyCode.RightArrow)) return 1f;
        return 0f;
    }
}
