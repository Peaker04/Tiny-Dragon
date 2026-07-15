using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 15f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;

    private Rigidbody2D rb;
    private bool wasGrounded;

    public bool IsGrounded { get; private set; }
    public float HorizontalInput => inputReader != null ? inputReader.Horizontal : 0f;
    public float FacingDirection => transform.localScale.x >= 0f ? 1f : -1f;

    /// <summary>Fired the moment the player leaves the ground via a jump.</summary>
    public event System.Action JumpPerformed;
    public event Action Landed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (inputReader == null)
        {
            inputReader = GetComponent<PlayerInputReader>();
        }

        IsGrounded = groundCheck != null
            && Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        wasGrounded = IsGrounded;
    }

    private void FixedUpdate()
    {
        IsGrounded = groundCheck != null
            && Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (!wasGrounded && IsGrounded)
        {
            Landed?.Invoke();
        }
        wasGrounded = IsGrounded;

        float horizontalInput = HorizontalInput;
        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);

        if (inputReader != null && inputReader.ConsumeJumpPressed() && IsGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            JumpPerformed?.Invoke();
        }

        Flip(horizontalInput);
    }

    public void Stop()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    public void ApplyMoveSpeed(float speed)
    {
        moveSpeed = Mathf.Max(speed, 0.1f);
    }

    public void Face(float facingDirection)
    {
        if (Mathf.Abs(facingDirection) <= 0.01f)
        {
            return;
        }

        transform.localScale = new Vector3(
            Mathf.Abs(transform.localScale.x) * Mathf.Sign(facingDirection),
            transform.localScale.y,
            transform.localScale.z
        );
    }

    private void Flip(float horizontalInput)
    {
        if (Mathf.Abs(horizontalInput) <= 0.01f)
        {
            return;
        }

        Face(horizontalInput);
    }
}
