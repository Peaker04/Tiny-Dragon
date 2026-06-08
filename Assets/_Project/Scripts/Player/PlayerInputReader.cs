using UnityEngine;

public class PlayerInputReader : MonoBehaviour
{
    [SerializeField] private KeyCode attackKey = KeyCode.J;
    [SerializeField] private KeyCode powerShotKey = KeyCode.K;
    [SerializeField] private bool movementInputEnabled = true;
    [SerializeField] private bool jumpInputEnabled = true;
    [SerializeField] private bool attackInputEnabled = true;
    [SerializeField] private bool powerShotInputEnabled = true;
    public bool inventoryInputEnabled = true;

    public float Horizontal { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool AttackPressed { get; private set; }
    public bool PowerShotPressed { get; private set; }

    private System.Collections.Generic.HashSet<string> customEnabledFeatures = new System.Collections.Generic.HashSet<string>();

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

    public void SetInputEnabled(bool movement, bool jump, bool attack, bool powerShot, bool inventory = true)
    {
        movementInputEnabled = movement;
        jumpInputEnabled = jump;
        attackInputEnabled = attack;
        powerShotInputEnabled = powerShot;
        inventoryInputEnabled = inventory;

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
        SetInputEnabled(true, true, true, true, true);
    }

    public void EnableMovement(bool enable) { movementInputEnabled = enable; if (!enable) Horizontal = 0f; }
    public void EnableJump(bool enable) { jumpInputEnabled = enable; if (!enable) JumpPressed = false; }
    public void EnableAttack(bool enable) { attackInputEnabled = enable; if (!enable) AttackPressed = false; }
    public void EnablePowerShot(bool enable) { powerShotInputEnabled = enable; if (!enable) PowerShotPressed = false; }
    public void EnableInventory(bool enable) { inventoryInputEnabled = enable; }
    public void EnableAll() { SetInputEnabled(true, true, true, true, true); }

    public void EnableCustomFeature(string featureName)
    {
        customEnabledFeatures.Add(featureName);
    }

    public void DisableCustomFeature(string featureName)
    {
        customEnabledFeatures.Remove(featureName);
    }

    public bool IsFeatureEnabled(string featureName)
    {
        return customEnabledFeatures.Contains(featureName);
    }

    public void ClearCustomFeatures()
    {
        customEnabledFeatures.Clear();
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
