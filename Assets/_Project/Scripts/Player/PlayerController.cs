using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private KeyCode attackKey = KeyCode.J;
    [SerializeField] private float attackCooldown = 0.35f;
    [SerializeField] private int projectileDamage = 1;
    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private float projectileLifetime = 2f;
    [SerializeField] private float projectileScale = 1.2f;
    [SerializeField] private Vector2 projectileSpawnOffset = new Vector2(0.6f, 0.15f);
    [SerializeField] private Sprite projectileSprite;
    [SerializeField] private bool projectileFacesRightByDefault = true;
    [SerializeField] private string projectileSortingLayerName = "Default";
    [SerializeField] private int projectileSortingOrder = 100;

    private Rigidbody2D rb;
    private Animator animator;
    private float moveX;
    private bool jumpPressed;
    private bool isGrounded;
    private bool hasIsGroundedParameter;
    private bool hasAttackParameter;
    private float nextAttackTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Tim Animator tren Player truoc, neu khong co thi tim trong object con.
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        hasIsGroundedParameter = HasAnimatorParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        hasAttackParameter = HasAnimatorParameter("Attack", AnimatorControllerParameterType.Trigger);
    }

    private void Update()
    {
        // Doc input trong Update de khong bo lo phim bam giua cac frame vat ly.
        moveX = ReadHorizontalInput();

        if (Input.GetButtonDown("Jump"))
        {
            jumpPressed = true;
        }

        // Bam attackKey de kich hoat animation chuong cua body.
        if (Input.GetKeyDown(attackKey) && Time.time >= nextAttackTime && animator != null && hasAttackParameter)
        {
            animator.SetTrigger("Attack");
            nextAttackTime = Time.time + attackCooldown;
        }

        // Speed = 0 thi idle, Speed > 0.1 thi chuyen sang walk trong Animator.
        if (animator != null)
        {
            animator.SetFloat("Speed", Mathf.Abs(moveX));
        }

        // Lat huong Player theo chieu di chuyen.
        Flip(moveX);
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

        if (movingLeft)
        {
            return -1f;
        }

        return 1f;
    }

    private void Flip(float horizontalInput)
    {
        // Di sang phai thi localScale.x duong.
        if (horizontalInput > 0 && transform.localScale.x < 0)
        {
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
        // Di sang trai thi localScale.x am.
        else if (horizontalInput < 0 && transform.localScale.x > 0)
        {
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
    }

    private void FixedUpdate()
    {
        // Kiem tra Player co dang cham ground layer khong.
        isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            0.2f,
            groundLayer
        );

        rb.linearVelocity = new Vector2(moveX * moveSpeed, rb.linearVelocity.y);

        // Jump chi thuc hien khi da bam nut va Player dang dung tren dat.
        if (jumpPressed && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }

        // Chi set IsGrounded neu Animator co parameter nay, tranh warning moi frame.
        if (animator != null && hasIsGroundedParameter)
        {
            animator.SetBool("IsGrounded", isGrounded);
        }

        jumpPressed = false;
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
    }

    public void ShootProjectile()
    {
        if (projectileSprite == null)
        {
            Debug.LogWarning("Player projectile sprite is missing. Assign 491_0 to Projectile Sprite on PlayerController.", this);
        }

        float facingDirection = transform.localScale.x >= 0f ? 1f : -1f;
        Vector3 spawnOffset = new Vector3(projectileSpawnOffset.x * facingDirection, projectileSpawnOffset.y, 0f);
        Vector3 spawnPosition = transform.position + spawnOffset;
        Vector2 projectileDirection = new Vector2(facingDirection, 0f);

        GameObject projectileObject = new GameObject("Player Projectile");
        projectileObject.transform.position = spawnPosition;

        PlayerProjectile projectile = projectileObject.AddComponent<PlayerProjectile>();
        projectile.Initialize(
            projectileDirection,
            projectileSpeed,
            projectileDamage,
            projectileLifetime,
            projectileSprite,
            projectileScale,
            projectileFacesRightByDefault,
            projectileSortingLayerName,
            projectileSortingOrder
        );
    }
}
