using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;

    private Rigidbody2D rb;
    private Animator animator;
    private float moveX;
    private bool jumpPressed;
    private bool isGrounded;
    private bool hasIsGroundedParameter;

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
    }

    private void Update()
    {
        // Doc input trong Update de khong bo lo phim bam giua cac frame vat ly.
        moveX = ReadHorizontalInput();

        if (Input.GetButtonDown("Jump"))
        {
            jumpPressed = true;
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
}
